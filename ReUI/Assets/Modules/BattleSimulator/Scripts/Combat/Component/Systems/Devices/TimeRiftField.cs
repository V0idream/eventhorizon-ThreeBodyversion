using System.Collections.Generic;
using Combat.Collision;
using Combat.Component.Body;
using Combat.Component.Ship;
using Combat.Component.Ship.Effects;
using Combat.Component.Unit.Classification;
using Combat.Scene;
using Combat.Unit;
using UnityEngine;

namespace Combat.Component.Systems.Devices
{
    /// <summary>
    /// A procedural field whose quad is exactly the size of the complete
    /// toroidal battle area. Rendering is one draw call and collision uses the
    /// same world-space equations, so Colossal maps do not require thousands
    /// of LineRenderer objects.
    /// </summary>
    public sealed class TimeRiftField : MonoBehaviour
    {
        public static void Spawn(IScene scene, IShip owner, int count, float lifetime)
        {
            var root = new GameObject("TimeRiftField.WorldCoverage");
            var field = root.AddComponent<TimeRiftField>();
            field.Initialize(scene, owner, count, Mathf.Max(3f, lifetime));
            ActiveFields.Add(field);
        }

        public static void ClearAll()
        {
            for (var i = ActiveFields.Count - 1; i >= 0; --i)
                if (ActiveFields[i]) Destroy(ActiveFields[i].gameObject);
            ActiveFields.Clear();
        }

        private void Initialize(IScene scene, IShip owner, int count, float lifetime)
        {
            _scene = scene;
            _owner = owner;
            _remaining = lifetime;
            _fieldOrigin = scene.ViewPoint;
            _visualCenter = scene.ViewPoint;
            _spacing = Mathf.Clamp(30f - Mathf.Max(1, count) * 0.75f, 18f, 26f);
            CreateWorldCoveringVisual();
        }

        private void CreateWorldCoveringVisual()
        {
            var shader = Shader.Find("ThreeBody/TimeRiftField");
            if (!shader)
            {
                Debug.LogError("ThreeBody/TimeRiftField shader is missing");
                return;
            }

            _material = new Material(shader);
            _material.SetFloat(SpacingId, _spacing);
            // Keep the procedural equations anchored to the activation point.
            // The mesh may follow the active toroidal map cell, but the rifts
            // themselves must never move with the player or camera.
            _material.SetVector(FieldCenterId, _fieldOrigin);

            _mesh = new Mesh { name = "TimeRiftFullBattlefieldQuad" };
            var halfWidth = _scene.Settings.AreaWidth * 0.5f;
            var halfHeight = _scene.Settings.AreaHeight * 0.5f;
            _mesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight), new Vector3(-halfWidth, halfHeight),
                new Vector3(halfWidth, halfHeight), new Vector3(halfWidth, -halfHeight),
            };
            _mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _mesh.RecalculateBounds();

            transform.position = _visualCenter;
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.sortingOrder = 38;
        }

        private void Update()
        {
            _remaining -= Time.deltaTime;
            KeepVisualOverBattlefield();
            UpdateRetriggerCooldowns(Time.deltaTime);
            if (_remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            ProcessCollisions();
        }

        private void ProcessCollisions()
        {
            lock (_scene.Ships.LockObject)
            {
                foreach (var ship in _scene.Ships.Items)
                {
                    if (ship == null || !ship.IsActive() || ship == _owner ||
                        !CombatRelations.AreEnemies(_owner.Type, ship.Type) ||
                        _retriggerCooldowns.ContainsKey(ship))
                        continue;

                    var position = ship.Body.WorldPosition();
                    var collisionWidth = Mathf.Max(1.5f, ship.Body.WorldScale() * 0.45f);
                    if (RiftDistance(position) > collisionWidth) continue;

                    _retriggerCooldowns[ship] = RetriggerDelay;
                    var destination = FindRiftFreePosition();
                    ship.Stats.Armor.Get(ship.Stats.Armor.Value * 0.50f);
                    ship.Affect(new Impact { EnergyDamage = 500f }, _owner);
                    MoveWorld(ship.Body, destination);
                    Stop(ship.Body);
                    foreach (var system in ship.Systems.All)
                        if (system is IDevice && system is SystemBase systemBase)
                            systemBase.ResetCooldown();
                    ThreeBodySpatialVisuals.PlayRiftTeleport(position, destination);
                }
            }
        }

        private float RiftDistance(Vector2 worldPosition)
        {
            var p = worldPosition - _fieldOrigin;
            var horizontal = PeriodicDistance(p.y + Mathf.Sin(p.x * 0.19f) * 1.35f, _spacing);
            var vertical = PeriodicDistance(p.x + Mathf.Sin(p.y * 0.17f + 1.7f) * 1.35f, _spacing);
            var diagonalA = PeriodicDistance((p.x + p.y) * 0.7071068f +
                                             Mathf.Sin((p.x - p.y) * 0.11f) * 1.65f,
                _spacing * 1.37f);
            var diagonalB = PeriodicDistance((p.x - p.y) * 0.7071068f +
                                             Mathf.Sin((p.x + p.y) * 0.13f + 2.4f) * 1.65f,
                _spacing * 1.61f);
            return Mathf.Min(horizontal, vertical, diagonalA, diagonalB);
        }

        private static float PeriodicDistance(float value, float spacing)
        {
            var wrapped = Mathf.Repeat(value + spacing * 0.5f, spacing) - spacing * 0.5f;
            return Mathf.Abs(wrapped);
        }

        private Vector2 FindRiftFreePosition()
        {
            var currentCenter = _scene.ViewPoint;
            var halfWidth = _scene.Settings.AreaWidth * 0.5f;
            var halfHeight = _scene.Settings.AreaHeight * 0.5f;
            var bestCandidate = currentCenter;
            var bestDistance = -1f;
            for (var attempt = 0; attempt < 120; attempt++)
            {
                var candidate = new Vector2(Random.Range(currentCenter.x - halfWidth, currentCenter.x + halfWidth),
                    Random.Range(currentCenter.y - halfHeight, currentCenter.y + halfHeight));
                var distance = RiftDistance(candidate);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestCandidate = candidate;
                }
                if (distance >= 4.5f) return candidate;
            }
            return bestCandidate;
        }

        private void KeepVisualOverBattlefield()
        {
            // Scene units are wrapped into the battlefield-sized toroidal cell
            // around ViewPoint. Move only the covering quad into that cell;
            // FieldCenter remains immutable, so neither visual nor collision
            // equations shift when the player crosses an edge.
            _visualCenter = _scene.ViewPoint;
            transform.position = _visualCenter;
        }

        private void UpdateRetriggerCooldowns(float elapsedTime)
        {
            _expiredShips.Clear();
            _updatedCooldowns.Clear();
            foreach (var entry in _retriggerCooldowns)
            {
                var remaining = entry.Value - elapsedTime;
                if (entry.Key == null || !entry.Key.IsActive() || remaining <= 0f)
                    _expiredShips.Add(entry.Key);
                else
                    _updatedCooldowns.Add(new CooldownUpdate(entry.Key, remaining));
            }
            for (var i = 0; i < _updatedCooldowns.Count; i++)
                _retriggerCooldowns[_updatedCooldowns[i].Ship] = _updatedCooldowns[i].Remaining;
            for (var i = 0; i < _expiredShips.Count; i++)
                _retriggerCooldowns.Remove(_expiredShips[i]);
        }

        private static void Stop(IBody body)
        {
            if (body is RigidBodyAdapter rigidBody)
            {
                rigidBody.Velocity = Vector2.zero;
                rigidBody.AngularVelocity = 0f;
            }
            else
            {
                body.ApplyAcceleration(-body.Velocity);
                body.ApplyAngularAcceleration(-body.AngularVelocity);
            }
        }

        private static void MoveWorld(IBody body, Vector2 worldPosition) =>
            body.Move(body.Parent == null ? worldPosition : body.WorldPositionToLocal(worldPosition));

        private void OnDestroy()
        {
            ActiveFields.Remove(this);
            if (_material) Destroy(_material);
            if (_mesh) Destroy(_mesh);
        }

        private readonly struct CooldownUpdate
        {
            public CooldownUpdate(IShip ship, float remaining) { Ship = ship; Remaining = remaining; }
            public IShip Ship { get; }
            public float Remaining { get; }
        }

        private static readonly int SpacingId = Shader.PropertyToID("_Spacing");
        private static readonly int FieldCenterId = Shader.PropertyToID("_FieldCenter");
        private static readonly List<TimeRiftField> ActiveFields = new();
        private readonly Dictionary<IShip, float> _retriggerCooldowns = new();
        private readonly List<IShip> _expiredShips = new();
        private readonly List<CooldownUpdate> _updatedCooldowns = new();
        private IScene _scene;
        private IShip _owner;
        private Material _material;
        private Mesh _mesh;
        private Vector2 _fieldOrigin;
        private Vector2 _visualCenter;
        private float _spacing;
        private float _remaining;
        private const float RetriggerDelay = 1.25f;
    }
}
