using System.Collections.Generic;
using Combat.Collision;
using Combat.Component.Bullet;
using Combat.Component.Ship;
using Combat.Component.Unit.Classification;
using Combat.Component.Unit;
using Combat.Scene;
using Combat.Component.Systems.Weapons;
using Combat.Unit;
using Constructor;
using Constructor.Model;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;
using UnityEngine;

namespace Combat.Factory
{
    public static class EdgeDroneRuntime
    {
        public const int NormalBuildId = 11010;
        public const int PredatorBuildId = 11011;
        public const int DefenseBuildId = 11012;
        public const int MaxPredatorDrones = 400;
        public const int MaxNormalDronesPerOwner = 48;
        public const int NanoStormInitialPredatorCount = 125;
        public const int DestroyedShipPredatorMultiplier = 5;
        public const float DroneRespawnDelay = 5f;
        public const float DefaultPredatorDamage = 500f;
        public const float NanoStormPredatorDamage = 5000f;

        public static void Configure(IScene scene, ShipFactory factory, IDatabase database)
        {
            _scene = scene;
            _factory = factory;
            _database = database;
            if (_runner) return;
            var gameObject = new GameObject("EdgeDroneRuntime");
            _runner = gameObject.AddComponent<EdgeDroneRuntimeRunner>();
        }

        public static IShip SpawnImmediate(int buildId, IShip owner, Vector2 position,
            DroneBehaviour behaviour = DroneBehaviour.Aggressive,
            float predatorDamage = DefaultPredatorDamage,
            bool ignoreSpawnLimits = false)
        {
            if (_factory == null || _database == null || owner == null || !owner.IsActive()) return null;
            if (!ignoreSpawnLimits && buildId == PredatorBuildId && _runner != null && !_runner.CanSpawnPredator)
                return null;
            if (!ignoreSpawnLimits && buildId == NormalBuildId && _runner != null && !_runner.CanSpawnNormal(owner))
                return null;
            if (!_specifications.TryGetValue(buildId, out var spec))
            {
                var build = _database.GetShipBuild(new ItemId<ShipBuild>(buildId));
                if (build == null || build == ShipBuild.DefaultValue) return null;
                spec = new ShipBuilder(build).Build(_database.ShipSettings);
                _specifications[buildId] = spec;
            }
            var offset = Random.insideUnitCircle * Mathf.Max(1.5f, owner.Body.Scale * 0.5f);
            var defenseDrone = buildId == DefenseBuildId;
            var drone = _factory.CreateDrone(spec, owner, defenseDrone ? 18f : 220f, position + offset,
                Random.Range(0f, 360f), defenseDrone ? DroneBehaviour.Defensive : behaviour, true, spec.CustomAi);
            if (buildId == DefenseBuildId && drone != null)
                _runner?.RegisterDefense(drone, owner);
            else if (buildId == PredatorBuildId && drone != null)
                _runner?.RegisterPredator(drone, predatorDamage);
            else if (buildId == NormalBuildId && drone != null)
                _runner?.RegisterNormal(drone, owner);
            return drone;
        }

        public static void QueueSpawn(int buildId, IShip owner, Vector2 position, int count,
            DroneBehaviour behaviour = DroneBehaviour.Aggressive,
            float predatorDamage = DefaultPredatorDamage)
        {
            if (count <= 0 || owner == null) return;
            var maxQueuedCount = buildId == PredatorBuildId ? MaxPredatorDrones : 200;
            _queue.Enqueue(new SpawnOrder(buildId, owner, position, Mathf.Clamp(count, 1, maxQueuedCount), behaviour,
                predatorDamage));
        }

        public static void NotifyShipDestroyed(IShip victim, IUnit source)
        {
            if (victim == null || source == null || victim.IsActive()) return;
            var attacker = source as IShip ?? source.Type?.Owner;
            if (attacker?.Specification?.Info.Id.Value != 11011) return;
            if (!_convertedVictims.Add(victim)) return;
            var owner = attacker.Type.Owner ?? attacker;
            var size = victim.Specification?.Stats?.Layout?.CellCount ?? 1;
            var count = Mathf.Max(1, Mathf.CeilToInt(size / 50f)) * DestroyedShipPredatorMultiplier;
            QueueSpawn(PredatorBuildId, owner, victim.Body.WorldPosition(), count);
        }

        internal static bool TryDequeue(out SpawnOrder order)
        {
            if (_queue.Count > 0) { order = _queue.Dequeue(); return true; }
            order = default;
            return false;
        }
        internal static IScene Scene => _scene;

        internal readonly struct SpawnOrder
        {
            public SpawnOrder(int buildId, IShip owner, Vector2 position, int count, DroneBehaviour behaviour,
                float predatorDamage)
            {
                BuildId = buildId;
                Owner = owner;
                Position = position;
                Count = count;
                Behaviour = behaviour;
                PredatorDamage = predatorDamage;
            }
            public int BuildId { get; }
            public IShip Owner { get; }
            public Vector2 Position { get; }
            public int Count { get; }
            public DroneBehaviour Behaviour { get; }
            public float PredatorDamage { get; }
        }

        private static readonly Queue<SpawnOrder> _queue = new();
        private static readonly HashSet<IShip> _convertedVictims = new();
        private static readonly Dictionary<int, IShipSpecification> _specifications = new();
        private static IScene _scene;
        private static ShipFactory _factory;
        private static IDatabase _database;
        private static EdgeDroneRuntimeRunner _runner;
    }

    public sealed class EdgeDroneRuntimeRunner : MonoBehaviour
    {
        public void RegisterDefense(IShip drone, IShip protectedShip) =>
            _defenders.Add(new DefenderPair(drone, protectedShip,
                Time.time + Random.Range(0f, DefenderUpdateInterval)));

        public void RegisterNormal(IShip drone, IShip owner)
        {
            if (drone != null && owner != null)
                _normalDrones.Add(new NormalDronePair(drone, owner));
        }

        public bool CanSpawnNormal(IShip owner)
        {
            RemoveDestroyedNormalDrones();
            var count = 0;
            for (var i = 0; i < _normalDrones.Count; ++i)
                if (_normalDrones[i].Owner == owner && ++count >= EdgeDroneRuntime.MaxNormalDronesPerOwner)
                    return false;
            return true;
        }

        public bool CanSpawnPredator
        {
            get
            {
                RemoveDestroyedPredators();
                return _predators.Count < EdgeDroneRuntime.MaxPredatorDrones;
            }
        }

        public void RegisterPredator(IShip drone, float contactDamage)
        {
            if (drone == null) return;
            for (var i = 0; i < _predators.Count; i++)
                if (_predators[i].Drone == drone)
                    return;
            _predators.Add(new PredatorPair(drone, Time.time + Random.Range(0f, 0.25f),
                Mathf.Max(0f, contactDamage)));
        }

        private void Update()
        {
            RemoveDestroyedPredators();
            RemoveDestroyedNormalDrones();
            ProcessRespawns();
            // Ship creation touches Unity objects, physics and renderers and
            // therefore cannot be moved to worker threads safely. Cache the
            // immutable specification and spread object creation over frames
            // with a strict main-thread time budget instead of producing a
            // burst that leaves aggregate CPU/GPU utilisation deceptively low.
            var budget = 4;
            var deadline = Time.realtimeSinceStartupAsDouble + 0.0025;
            while (budget > 0 && Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (_active.Count == 0)
                {
                    if (!EdgeDroneRuntime.TryDequeue(out var order)) break;
                    _active.Enqueue(order);
                }
                var current = _active.Dequeue();
                if (current.Owner != null && current.Owner.IsActive())
                {
                    EdgeDroneRuntime.SpawnImmediate(current.BuildId, current.Owner, current.Position, current.Behaviour,
                        current.PredatorDamage);
                    if (current.Count > 1)
                        _active.Enqueue(new EdgeDroneRuntime.SpawnOrder(current.BuildId, current.Owner,
                            current.Position, current.Count - 1, current.Behaviour, current.PredatorDamage));
                }
                budget--;
            }

            AttackWithPredators();

            var now = Time.time;
            for (var i = _defenders.Count - 1; i >= 0; i--)
            {
                var pair = _defenders[i];
                if (pair.Drone == null || !pair.Drone.IsActive())
                {
                    ScheduleRespawn(EdgeDroneRuntime.DefenseBuildId,
                        ResolveRespawnOwner(pair.Drone, pair.Owner), DroneBehaviour.Defensive);
                    _defenders.RemoveAt(i);
                    continue;
                }
                if (pair.Owner == null || !pair.Owner.IsActive())
                {
                    _defenders.RemoveAt(i);
                    continue;
                }
                if (now < pair.NextUpdate) continue;
                pair.NextUpdate = now + DefenderUpdateInterval;
                Intercept(pair);
            }
        }

        private void RemoveDestroyedNormalDrones()
        {
            for (var i = _normalDrones.Count - 1; i >= 0; --i)
            {
                var pair = _normalDrones[i];
                if (pair.Drone == null || !pair.Drone.IsActive())
                {
                    ScheduleRespawn(EdgeDroneRuntime.NormalBuildId,
                        ResolveRespawnOwner(pair.Drone, pair.Owner), DroneBehaviour.Aggressive);
                    _normalDrones.RemoveAt(i);
                    continue;
                }

                if (pair.Owner == null || !pair.Owner.IsActive())
                    _normalDrones.RemoveAt(i);
            }
        }

        private void RemoveDestroyedPredators()
        {
            for (var i = _predators.Count - 1; i >= 0; i--)
            {
                var pair = _predators[i];
                if (pair.Drone == null || !pair.Drone.IsActive())
                {
                    ScheduleRespawn(EdgeDroneRuntime.PredatorBuildId,
                        ResolveRespawnOwner(pair.Drone, pair.Owner), DroneBehaviour.Aggressive, pair.ContactDamage);
                    _predators.RemoveAt(i);
                    continue;
                }

                if (pair.Owner == null || !pair.Owner.IsActive())
                    _predators.RemoveAt(i);
            }
        }

        private void ScheduleRespawn(int buildId, IShip owner, DroneBehaviour behaviour,
            float predatorDamage = EdgeDroneRuntime.DefaultPredatorDamage)
        {
            if (owner == null || !owner.IsActive())
                return;

            _respawns.Add(new RespawnOrder(buildId, owner, behaviour, predatorDamage,
                Time.time + EdgeDroneRuntime.DroneRespawnDelay));
        }

        private static IShip ResolveRespawnOwner(IShip drone, IShip fallbackOwner) =>
            drone?.Type?.Owner ?? fallbackOwner;

        private void ProcessRespawns()
        {
            if (_respawns.Count == 0)
                return;

            var now = Time.time;
            for (var i = _respawns.Count - 1; i >= 0; --i)
            {
                var order = _respawns[i];
                if (order.Owner == null || !order.Owner.IsActive())
                {
                    _respawns.RemoveAt(i);
                    continue;
                }
                if (now < order.RespawnAt)
                    continue;

                // Respawns deliberately bypass the normal queued-spawn budget
                // and population gates. They replace a previously existing
                // Edge drone one-for-one exactly five seconds after its loss.
                var respawned = EdgeDroneRuntime.SpawnImmediate(order.BuildId, order.Owner,
                    order.Owner.Body.WorldPosition(), order.Behaviour, order.PredatorDamage, true);
                if (respawned != null)
                    _respawns.RemoveAt(i);
            }
        }

        private void AttackWithPredators()
        {
            var scene = EdgeDroneRuntime.Scene;
            if (scene == null) return;
            var now = Time.time;

            for (var i = 0; i < _predators.Count; i++)
            {
                var pair = _predators[i];
                var drone = pair.Drone;
                if (drone == null || !drone.IsActive()) continue;

                // Electronic-shield capture changes the drone owner in place.
                // Drop a stale suicide target immediately if it became the
                // drone's new owner instead of waiting for the periodic target
                // refresh to notice the allegiance change.
                if (pair.Target != null && pair.Target == drone.Type.Owner)
                    pair.Target = null;

                if (pair.Target == null || !pair.Target.IsActive() ||
                    !CombatRelations.AreEnemies(drone.Type, pair.Target.Type) ||
                    !Combat.Component.Ship.Effects.RadarStatus.CanDetect(drone, pair.Target) ||
                    now >= pair.NextTargetUpdate)
                {
                    pair.Target = FindNearestPredatorTarget(scene, drone);
                    pair.NextTargetUpdate = now + 0.35f + Random.Range(0f, 0.12f);
                }

                var target = pair.Target;
                if (target == null || !target.IsActive())
                {
                    drone.Controls.Throttle = 0f;
                    drone.Controls.Course = null;
                    continue;
                }

                // Re-check immediately before applying the manual suicide hit.
                // Capture can occur during the same frame after target
                // acquisition, and this final gate guarantees that neither the
                // new owner nor any new ally can receive predator contact damage.
                if (target == drone.Type.Owner || CombatRelations.AreAllies(drone.Type, target.Type))
                {
                    pair.Target = null;
                    continue;
                }
                var delta = target.Body.WorldPosition() - drone.Body.WorldPosition();
                var distance = delta.magnitude;
                drone.Controls.Course = distance > 0.01f ? RotationHelpers.Angle(delta) : null;
                drone.Controls.Throttle = 1f;
                var desiredVelocity = distance > 0.01f ? delta / distance * 58f : Vector2.zero;
                drone.Body.ApplyAcceleration(desiredVelocity - drone.Body.WorldVelocity());

                var contactDistance = Mathf.Max(0.8f,
                    (drone.Body.WorldScale() + target.Body.WorldScale()) * 0.42f);
                if (distance > contactDistance) continue;

                // Predator drones no longer mount ranged guns. Applying one
                // physical impact at contact removes hundreds of spawned
                // bullets while preserving a clear suicide-attack role.
                var impact = new Impact();
                impact.AddDamage(DamageType.Impact,
                    pair.ContactDamage * (drone.Specification?.Stats?.DamageMultiplier.Value ?? 1f));
                target.Affect(impact, drone);
                drone.Vanish();
            }
        }

        private static IShip FindNearestPredatorTarget(IScene scene, IShip drone)
        {
            IShip nearest = null;
            var nearestDistance = float.PositiveInfinity;
            var candidates = InterceptionTargetCoordinator.GetShipCandidates(scene);
            for (var candidateIndex = 0; candidateIndex < candidates.Count; ++candidateIndex)
            {
                var candidate = candidates[candidateIndex];
                if (candidate == drone || !candidate.IsActive() ||
                    !CombatRelations.AreEnemies(drone.Type, candidate.Type) ||
                    !Combat.Component.Ship.Effects.RadarStatus.CanDetect(drone, candidate))
                    continue;

                var distance = (candidate.Body.WorldPosition() - drone.Body.WorldPosition()).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = candidate;
            }
            return nearest;
        }

        private static void Intercept(DefenderPair pair)
        {
            var scene = EdgeDroneRuntime.Scene;
            if (scene == null) return;
            IUnit nearest = null;
            var shortestImpactTime = 0.85f;
            var ownerPosition = pair.Owner.Body.WorldPosition();
            var candidates = InterceptionTargetCoordinator.GetProjectileCandidates(scene);
            for (var candidateIndex = 0; candidateIndex < candidates.Count; ++candidateIndex)
            {
                var unit = candidates[candidateIndex];
                if (unit is not IBullet || !unit.IsActive() ||
                    !CombatRelations.AreEnemies(pair.Owner.Type, unit.Type)) continue;

                // The installed autonomous laser is the primary defence.
                // Ram only a projectile whose current trajectory is about
                // to reach the protected ship and therefore escaped it.
                var relativePosition = unit.Body.WorldPosition() - ownerPosition;
                var relativeVelocity = unit.Body.WorldVelocity() - pair.Owner.Body.WorldVelocity();
                var speedSquared = relativeVelocity.sqrMagnitude;
                if (speedSquared < 0.01f || Vector2.Dot(relativePosition, relativeVelocity) >= 0f)
                    continue;
                var impactTime = -Vector2.Dot(relativePosition, relativeVelocity) / speedSquared;
                if (impactTime < 0f || impactTime >= shortestImpactTime)
                    continue;
                var closestDistance = (relativePosition + relativeVelocity * impactTime).magnitude;
                if (closestDistance > Mathf.Max(3f, pair.Owner.Body.Scale * 0.8f))
                    continue;
                shortestImpactTime = impactTime;
                nearest = unit;
            }
            if (nearest == null) return;
            var predictedPosition = nearest.Body.WorldPosition() + nearest.Body.WorldVelocity() * shortestImpactTime;
            var direction = (predictedPosition - pair.Drone.Body.WorldPosition()).normalized;
            pair.Drone.Body.ApplyAcceleration(direction * 42f - pair.Drone.Body.WorldVelocity());
            if (Vector2.Distance(nearest.Body.WorldPosition(), pair.Drone.Body.WorldPosition()) <=
                Mathf.Max(1f, nearest.Body.Scale + pair.Drone.Body.Scale))
            {
                nearest.Vanish();
                pair.Drone.Vanish();
            }
        }

        private sealed class DefenderPair
        {
            public DefenderPair(IShip drone, IShip owner, float nextUpdate)
            {
                Drone = drone;
                Owner = owner;
                NextUpdate = nextUpdate;
            }
            public IShip Drone { get; }
            public IShip Owner { get; }
            public float NextUpdate { get; set; }
        }

        private readonly struct NormalDronePair
        {
            public NormalDronePair(IShip drone, IShip owner) { Drone = drone; Owner = owner; }
            public IShip Drone { get; }
            public IShip Owner { get; }
        }

        private sealed class PredatorPair
        {
            public PredatorPair(IShip drone, float nextTargetUpdate, float contactDamage)
            {
                Drone = drone;
                Owner = drone?.Type?.Owner;
                NextTargetUpdate = nextTargetUpdate;
                ContactDamage = contactDamage;
            }
            public IShip Drone { get; }
            public IShip Owner { get; }
            public IShip Target { get; set; }
            public float NextTargetUpdate { get; set; }
            public float ContactDamage { get; }
        }

        private readonly struct RespawnOrder
        {
            public RespawnOrder(int buildId, IShip owner, DroneBehaviour behaviour, float predatorDamage,
                float respawnAt)
            {
                BuildId = buildId;
                Owner = owner;
                Behaviour = behaviour;
                PredatorDamage = predatorDamage;
                RespawnAt = respawnAt;
            }

            public int BuildId { get; }
            public IShip Owner { get; }
            public DroneBehaviour Behaviour { get; }
            public float PredatorDamage { get; }
            public float RespawnAt { get; }
        }

        private readonly Queue<EdgeDroneRuntime.SpawnOrder> _active = new();
        private readonly List<RespawnOrder> _respawns = new();
        private readonly List<NormalDronePair> _normalDrones = new();
        private readonly List<DefenderPair> _defenders = new();
        private readonly List<PredatorPair> _predators = new();
        private const float DefenderUpdateInterval = 0.05f;
    }
}
