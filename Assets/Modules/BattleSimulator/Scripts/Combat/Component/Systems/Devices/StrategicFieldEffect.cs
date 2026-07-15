using System.Collections.Generic;
using System.Linq;
using Combat.Collision;
using Combat.Component.Body;
using Combat.Component.Ship;
using Combat.Component.Stats;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Scene;
using Combat.Unit;
using UnityEngine;

namespace Combat.Component.Systems.Devices
{
    public sealed class StrategicFieldEffect : MonoBehaviour
    {
        public enum FieldKind { DualVectorFoil, BlackHole, DarkDomain }

        public static StrategicFieldEffect Create(IScene scene, IShip owner, Vector2 position, FieldKind kind)
        {
            var gameObject = new GameObject("StrategicField_" + kind);
            gameObject.transform.position = position;
            var effect = gameObject.AddComponent<StrategicFieldEffect>();
            effect._scene = scene;
            effect._owner = owner;
            effect._kind = kind;
            effect._position = position;
            effect._radius = kind == FieldKind.DarkDomain || kind == FieldKind.BlackHole ? 10f : 1f;
            effect._lifetime = kind == FieldKind.BlackHole ? 5f : float.PositiveInfinity;
            effect.CreateVisual();
            ActiveFields.Add(effect);
            return effect;
        }

        private void FixedUpdate()
        {
            var elapsed = Time.fixedDeltaTime;
            _age += elapsed;
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (_kind == FieldKind.DualVectorFoil && !IsBlockedByDarkDomain(_position, _radius))
                _radius += 150f * elapsed;

            if (_kind == FieldKind.BlackHole)
            {
                foreach (var field in ActiveFields.ToArray())
                {
                    if (field == null || field == this || field._kind != FieldKind.DualVectorFoil) continue;
                    if (Vector2.Distance(_position, field._position) <= _radius + field._radius)
                        Destroy(field.gameObject);
                }
            }

            lock (_scene.Units.LockObject)
            {
                foreach (var unit in _scene.Units.Items.ToArray())
                {
                    if (unit == null || !unit.IsActive()) continue;
                    var delta = _position - unit.Body.WorldPosition();
                    if (delta.sqrMagnitude > _radius * _radius) continue;

                    if (_kind == FieldKind.BlackHole)
                    {
                        if (unit.Type.Class != UnitClass.Ship && unit.Type.Class != UnitClass.Drone)
                        {
                            unit.Vanish();
                            continue;
                        }
                        unit.Body.ApplyAcceleration(delta.normalized * 45f * elapsed);
                        if (unit is IShip blackHoleTarget)
                            blackHoleTarget.Affect(new Impact { TrueDamage = 500f * elapsed }, _owner);
                    }
                    else if (_kind == FieldKind.DualVectorFoil)
                    {
                        if (unit is IShip foilTarget)
                        {
                            foilTarget.Affect(new Impact { TrueDamage = 2100000000f * elapsed }, _owner);
                            var noise = Random.value > 0.5f ? 0.18f : 0.42f;
                            foilTarget.View.Color = new Color(noise, noise, noise, 0.65f);
                        }
                    }
                    else if (_kind == FieldKind.DarkDomain)
                    {
                        if (ShipStats.IsFourDimensionalUnit(unit)) continue;
                        if (unit.Type.Class == UnitClass.Missile || unit.Type.Class == UnitClass.EnergyBolt)
                        {
                            unit.Vanish();
                            continue;
                        }
                        if (unit is IShip ship && !ship.Systems.All.OfType<WarpDrive>().Any(drive => drive.IsWarping))
                        {
                            var max = Mathf.Max(0.5f, ship.Engine.MaxVelocity * 0.1f);
                            if (unit.Body.Velocity.sqrMagnitude > max * max)
                                unit.Body.ApplyAcceleration(unit.Body.Velocity.normalized * max - unit.Body.Velocity);
                        }
                    }
                }
            }
            UpdateVisual();
        }

        private void CreateVisual()
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = 64;
            _line.widthMultiplier = _kind == FieldKind.DualVectorFoil ? 1.3f : 0.7f;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.sortingOrder = 25;
            _line.startColor = _line.endColor = _kind switch
            {
                FieldKind.DualVectorFoil => new Color(0.7f, 0.45f, 1f, 0.7f),
                FieldKind.BlackHole => new Color(0.35f, 0.1f, 0.8f, 0.9f),
                _ => new Color(0.01f, 0.01f, 0.02f, 0.95f)
            };
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_line == null) return;
            for (var i = 0; i < _line.positionCount; ++i)
            {
                var angle = i * Mathf.PI * 2f / _line.positionCount;
                _line.SetPosition(i, _position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _radius);
            }
        }

        public static bool TryBlockRay(Vector2 origin, Vector2 direction, float maxRange, out float distance)
        {
            distance = maxRange;
            var blocked = false;
            var normalized = direction.normalized;
            foreach (var field in ActiveFields.Where(item => item != null &&
                         (item._kind == FieldKind.BlackHole || item._kind == FieldKind.DarkDomain)))
            {
                var toCenter = field._position - origin;
                var along = Mathf.Clamp(Vector2.Dot(toCenter, normalized), 0f, maxRange);
                var closest = origin + normalized * along;
                if (Vector2.Distance(closest, field._position) > field._radius) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, along - field._radius));
                blocked = true;
            }
            return blocked;
        }

        public static bool IsBlockedByDarkDomain(Vector2 position, float radius)
        {
            if (WarpTrailEffect.IsInsideAnyTrail(position, radius)) return true;
            return ActiveFields.Any(item => item != null && item._kind == FieldKind.DarkDomain &&
                                            Vector2.Distance(item._position, position) <= item._radius + radius);
        }

        public static void ClearAll()
        {
            foreach (var field in ActiveFields.ToArray())
                if (field != null) Destroy(field.gameObject);
            ActiveFields.Clear();
        }

        private void OnDestroy() => ActiveFields.Remove(this);

        private static readonly List<StrategicFieldEffect> ActiveFields = new();
        private IScene _scene;
        private IShip _owner;
        private FieldKind _kind;
        private Vector2 _position;
        private float _radius;
        private float _lifetime;
        private float _age;
        private LineRenderer _line;
    }
}
