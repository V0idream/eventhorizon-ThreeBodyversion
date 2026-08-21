using System.Collections.Generic;
using Combat.Collision.Manager;
using Combat.Component.Engine;
using Combat.Component.Features;
using Combat.Component.Ship;
using Combat.Component.Ship.Effects;
using Combat.Component.Stats;
using Combat.Component.Systems;
using Combat.Component.Triggers;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Factory;
using Combat.Scene;
using Combat.Unit;
using GameDatabase.Enums;
using UnityEngine;

namespace Combat.Collision.Behaviour.Action
{
    public sealed class CreedDamageAction : ICollisionAction
    {
        public CreedDamageAction(float damage)
        {
            _damage = Mathf.Max(0f, damage);
        }

        public static bool HasRecordedHit(IUnit projectile, IUnit target)
        {
            return projectile != null && target != null &&
                   _hits.TryGetValue(projectile, out var targets) && targets.Contains(target);
        }

        public static bool RecordGuaranteedHit(IUnit projectile, IUnit target)
        {
            return TryRecordHit(projectile, target);
        }

        public static void Forget(IUnit projectile)
        {
            if (projectile != null)
                _hits.Remove(projectile);
        }

        public void Invoke(IUnit self, IUnit target, CollisionData collisionData,
            ref Impact selfImpact, ref Impact targetImpact)
        {
            if (!collisionData.IsNew || target == null)
                return;
            if (target is IShip ship && ShipStats.IsFourDimensionalUnit(ship))
                return;
            if (!TryRecordHit(self, target))
                return;

            _projectile ??= self;

            var impact = CreateImpact();
            targetImpact.TrueDamage += impact.TrueDamage;
            targetImpact.ShieldDamage += impact.ShieldDamage;
            targetImpact.IgnoresShield |= impact.IgnoresShield;
        }

        public void Dispose()
        {
            Forget(_projectile);
        }

        private static bool TryRecordHit(IUnit projectile, IUnit target)
        {
            if (projectile == null || target == null)
                return false;
            if (!_hits.TryGetValue(projectile, out var targets))
            {
                targets = new HashSet<IUnit>();
                _hits.Add(projectile, targets);
            }
            return targets.Add(target);
        }

        private Impact CreateImpact()
        {
            return new Impact
            {
                TrueDamage = _damage,
                ShieldDamage = 2_100_000_000f,
                IgnoresShield = true,
            };
        }

        private readonly float _damage;
        private IUnit _projectile;
        private static readonly Dictionary<IUnit, HashSet<IUnit>> _hits = new();
    }

    public sealed class TidalCollisionAction : ICollisionAction
    {
        public TidalCollisionAction(IShip owner)
        {
            _owner = owner;
        }

        public void Invoke(IUnit self, IUnit target, CollisionData collisionData,
            ref Impact selfImpact, ref Impact targetImpact)
        {
            if (_used || !collisionData.IsNew || target is not IShip ship)
                return;
            _used = true;
            TidalStretchEffect.Apply(ship, _owner);
        }

        public void Dispose() { }
        private readonly IShip _owner;
        private bool _used;
    }

    public sealed class ConceptErasureCollisionAction : ICollisionAction
    {
        public ConceptErasureCollisionAction(IScene scene, IShip owner)
        {
            _scene = scene;
            _owner = owner;
        }

        public void Invoke(IUnit self, IUnit target, CollisionData collisionData,
            ref Impact selfImpact, ref Impact targetImpact)
        {
            if (_used || !collisionData.IsNew)
                return;
            _used = true;
            var position = target?.Body?.WorldPosition() ?? self.Body.WorldPosition();
            ConceptErasureField.Spawn(_scene, _owner, position, 50f, 12f);
            selfImpact.Effects |= CollisionEffect.Destroy;
        }

        public void Dispose() { }
        private readonly IScene _scene;
        private readonly IShip _owner;
        private bool _used;
    }

    public sealed class TidalStretchEffect : IShipEffect
    {
        public static void Apply(IShip ship, IShip source)
        {
            if (ship == null || !ship.IsActive())
                return;

            foreach (var effect in ship.Effects.All)
            {
                if (effect is not TidalStretchEffect tidal)
                    continue;
                tidal._stacks++;
                tidal._remaining = Duration;
                return;
            }

            ship.AddEffect(new TidalStretchEffect(ship, source));
        }

        private TidalStretchEffect(IShip ship, IShip source)
        {
            _ship = ship;
            _source = source;
            _remaining = Duration;
            _cellCount = Mathf.Max(1, ship.Specification?.Stats?.Layout?.CellCount ?? 1);
            CreateVisualClone();
        }

        public bool IsAlive => _remaining > 0f && _ship.IsActive();

        public void UpdatePhysics(IShip ship, float elapsedTime)
        {
            var step = Mathf.Min(Mathf.Max(0f, elapsedTime), Mathf.Max(0f, _remaining));
            _remaining -= step;
            if (step > 0f)
                ship.Affect(new Impact { TrueDamage = 30f * _cellCount * _stacks * step / Duration }, _source);

            if (_remaining <= 0f && !_snapDamageApplied && ship.IsActive())
            {
                _snapDamageApplied = true;
                ship.Affect(new Impact { TrueDamage = 70f * _cellCount * _stacks }, _source);
            }
        }

        public void UpdateView(IShip ship, float elapsedTime)
        {
            if (!_visualClone) return;
            var progress = 1f - Mathf.Clamp01(_remaining / Duration);
            // Ship art faces along local +X, so tidal stretching must extend
            // along the hull rather than making the sprite taller.
            _visualClone.transform.localScale = new Vector3(Mathf.Lerp(1f, 1.3f, progress), 1f, 1f);
        }

        public void Dispose()
        {
            if (_originalRenderer) _originalRenderer.enabled = true;
            if (_visualClone) Object.Destroy(_visualClone);
        }

        private void CreateVisualClone()
        {
            var viewComponent = _ship.View as UnityEngine.Component;
            _originalRenderer = viewComponent != null ? viewComponent.GetComponent<SpriteRenderer>() : null;
            if (!_originalRenderer || !_originalRenderer.sprite)
                return;

            _visualClone = new GameObject("TidalStretchedShip");
            _visualClone.transform.SetParent(_originalRenderer.transform, false);
            var clone = _visualClone.AddComponent<SpriteRenderer>();
            clone.sprite = _originalRenderer.sprite;
            clone.sharedMaterial = _originalRenderer.sharedMaterial;
            clone.color = _originalRenderer.color;
            clone.sortingLayerID = _originalRenderer.sortingLayerID;
            clone.sortingOrder = _originalRenderer.sortingOrder + 1;
            _originalRenderer.enabled = false;
        }

        public IEngineModification EngineModification => null;
        public IFeaturesModification FeaturesModification => null;
        public ISystemsModification SystemsModification => null;
        public IStatsModification StatsModification => null;
        public IUnitAction UnitAction => null;

        private const float Duration = 1f;
        private readonly IShip _ship;
        private readonly IShip _source;
        private readonly int _cellCount;
        private float _remaining;
        private int _stacks = 1;
        private bool _snapDamageApplied;
        private SpriteRenderer _originalRenderer;
        private GameObject _visualClone;
    }

    public sealed class RealityDistortionEffect : IShipEffect, IStatsModification
    {
        public static void Add(IShip ship, IShip source, float amount)
        {
            if (ship == null || amount <= 0f || !ship.IsActive())
                return;

            foreach (var effect in ship.Effects.All)
            {
                if (effect is not RealityDistortionEffect distortion)
                    continue;
                distortion._source = source;
                distortion._distortion = Mathf.Min(100f, distortion._distortion + amount);
                distortion._timeSinceLastIncrease = 0f;
                distortion.TryErase();
                return;
            }

            var created = new RealityDistortionEffect(ship, source, amount);
            ship.AddEffect(created);
            created.TryErase();
        }

        public static void AmplifyIncomingDamage(IShip ship, ref Impact impact)
        {
            if (ship?.Effects == null)
                return;
            foreach (var effect in ship.Effects.All)
            {
                if (effect is not RealityDistortionEffect distortion)
                    continue;
                var multiplier = 1f + 3f * distortion._distortion / 100f;
                impact.KineticDamage *= multiplier;
                impact.EnergyDamage *= multiplier;
                impact.HeatDamage *= multiplier;
                impact.CorrosiveDamage *= multiplier;
                impact.TrueDamage *= multiplier;
                return;
            }
        }

        private RealityDistortionEffect(IShip ship, IShip source, float amount)
        {
            _ship = ship;
            _source = source;
            _distortion = Mathf.Clamp(amount, 0f, 100f);
            _baseViewAlpha = ship?.View != null ? ship.View.Color.a : 1f;
        }

        public bool IsAlive => _ship.IsActive() && _distortion > 0f;
        public bool TryApplyModification(ref Resistance data)
        {
            if (!IsAlive) return false;
            var multiplier = Mathf.Clamp01(1f - _distortion / 100f);
            data.Kinetic *= multiplier;
            data.Energy *= multiplier;
            data.Heat *= multiplier;
            data.Corrosive *= multiplier;
            return true;
        }

        public void UpdatePhysics(IShip ship, float elapsedTime)
        {
            if (!_ship.IsActive() || _distortion <= 0f || elapsedTime <= 0f)
                return;

            _timeSinceLastIncrease += elapsedTime;
            // The erasure field applies distortion in ~0.1 s scans.  A short
            // grace window prevents simultaneous gain/recovery while the ship
            // is still inside the field.  Once distortion is no longer being
            // added, it recovers at exactly one point per second.
            if (_timeSinceLastIncrease < RecoveryGracePeriod)
                return;

            _distortion = Mathf.Max(0f, _distortion - RecoveryRate * elapsedTime);
        }
        public void UpdateView(IShip ship, float elapsedTime)
        {
            var color = ship.View.Color;
            color.a = _baseViewAlpha * Mathf.Clamp01(1f - _distortion / 115f);
            ship.View.Color = color;
        }
        public void Dispose()
        {
            if (_ship?.View == null)
                return;
            var color = _ship.View.Color;
            color.a = _baseViewAlpha;
            _ship.View.Color = color;
        }

        private void TryErase()
        {
            if (_distortion < 100f || !_ship.IsActive()) return;
            ThreeBodySpatialVisuals.PlayUniverseTransit(_ship.Body.WorldPosition(),
                new Color(0.25f, 0.95f, 1f, 1f), false);
            _ship.Vanish();
        }

        public IEngineModification EngineModification => null;
        public IFeaturesModification FeaturesModification => null;
        public ISystemsModification SystemsModification => null;
        public IStatsModification StatsModification => this;
        public IUnitAction UnitAction => null;

        private readonly IShip _ship;
        private IShip _source;
        private float _distortion;
        private float _timeSinceLastIncrease;
        private readonly float _baseViewAlpha;
        private const float RecoveryRate = 1f;
        private const float RecoveryGracePeriod = 0.25f;
    }

    public sealed class ConceptErasureField : MonoBehaviour
    {
        public static void Spawn(IScene scene, IShip owner, Vector2 position, float radius, float lifetime)
        {
            if (scene == null || owner == null) return;
            var root = new GameObject("ConceptErasureField");
            root.transform.position = position;
            root.AddComponent<ConceptErasureField>().Initialize(scene, owner, radius, lifetime);
        }

        private void Initialize(IScene scene, IShip owner, float radius, float lifetime)
        {
            _scene = scene;
            _owner = owner;
            _radius = Mathf.Max(1f, radius);
            _remaining = Mathf.Max(0.1f, lifetime);
            _material = new Material(Shader.Find("Sprites/Default"));
            for (var arm = 0; arm < 8; ++arm)
                _lines.Add(CreateSpiral(arm));
        }

        private LineRenderer CreateSpiral(int arm)
        {
            var child = new GameObject("ErasureSpiral" + arm);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.material = _material;
            line.useWorldSpace = false;
            line.positionCount = 48;
            line.startWidth = 0.28f;
            line.endWidth = 0.035f;
            line.sortingOrder = 125;
            for (var point = 0; point < line.positionCount; ++point)
            {
                var t = point / (line.positionCount - 1f);
                var angle = arm * Mathf.PI * 0.25f + t * Mathf.PI * 4.5f;
                var radius = _radius * (1f - t);
                line.SetPosition(point, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            return line;
        }

        private void Update()
        {
            var elapsed = Time.deltaTime;
            _remaining -= elapsed;
            _scanAccumulator += elapsed;
            transform.Rotate(0f, 0f, 38f * elapsed);
            var pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 5f);
            foreach (var line in _lines)
            {
                var color = new Color(0.3f, 0.95f, 1f, Mathf.Clamp01(_remaining) * pulse);
                line.startColor = color;
                color.a *= 0.05f;
                line.endColor = color;
            }

            if (_scanAccumulator >= 0.1f)
            {
                var step = _scanAccumulator;
                _scanAccumulator = 0f;
                Apply(step);
            }

            if (_remaining <= 0f)
                Destroy(gameObject);
        }

        private void Apply(float elapsed)
        {
            var ships = Combat.Component.Systems.Weapons.InterceptionTargetCoordinator.GetShipCandidates(_scene);
            for (var i = 0; i < ships.Count; ++i)
            {
                var ship = ships[i];
                if (ship == null || !ship.IsActive() || ship == _owner ||
                    !CombatRelations.AreEnemies(_owner.Type, ship.Type))
                    continue;
                var distance = BattlefieldGeometry.Distance(transform.position, ship.Body.WorldPosition());
                if (distance > _radius) continue;

                if ((int)ship.Specification.Info.SizeClass <= (int)SizeClass.Cruiser)
                {
                    ThreeBodySpatialVisuals.PlayUniverseTransit(ship.Body.WorldPosition(),
                        new Color(0.25f, 0.95f, 1f, 1f), false);
                    ship.Vanish();
                    continue;
                }

                float centerRate;
                if (ship.Specification.Info.SizeClass == SizeClass.Battleship)
                    centerRate = 20f;
                else if (ship.Specification.Info.ShipType == ShipType.Flagship &&
                         ship.Specification.Info.SizeClass != SizeClass.TitanP)
                    centerRate = 10f;
                else
                    centerRate = 5f;
                var falloff = Mathf.Clamp01(1f - distance / _radius);
                RealityDistortionEffect.Add(ship, _owner, centerRate * falloff * elapsed);
            }
        }

        private void OnDestroy()
        {
            if (_material) Destroy(_material);
        }

        private readonly List<LineRenderer> _lines = new();
        private IScene _scene;
        private IShip _owner;
        private Material _material;
        private float _radius;
        private float _remaining;
        private float _scanAccumulator;
    }
}
