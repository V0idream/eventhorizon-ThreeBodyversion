using System.Collections.Generic;
using Combat.Component.Ship;
using Combat.Component.Unit.Classification;
using Combat.Scene;
using Combat.Unit;
using UnityEngine;
using System.Linq;

namespace Combat.Component.Systems.Devices
{
    public sealed class WarpTrailEffect : MonoBehaviour
    {
        public static WarpTrailEffect Create(IScene scene, IShip owner)
        {
            var go = new GameObject("Preview5WarpTrail");
            var effect = go.AddComponent<WarpTrailEffect>();
            effect._scene = scene;
            effect._owner = owner;
            effect._line = go.AddComponent<LineRenderer>();
            effect._line.useWorldSpace = true;
            effect._line.widthMultiplier = 5f;
            effect._line.numCapVertices = 6;
            effect._line.numCornerVertices = 6;
            effect._line.material = new Material(Shader.Find("Sprites/Default"));
            effect._line.startColor = new Color(0.01f, 0.01f, 0.015f, 0.72f);
            effect._line.endColor = new Color(0.08f, 0.08f, 0.1f, 0.45f);
            effect._line.sortingOrder = 20;
            effect.CreateFog();
            ActiveTrails.Add(effect);
            return effect;
        }

        public void Record(Vector2 position)
        {
            if (_points.Count > 0 && Vector2.SqrMagnitude(_points[^1] - position) < 1f) return;
            _points.Add(position);
            _line.positionCount = _points.Count;
            _line.SetPosition(_points.Count - 1, new Vector3(position.x, position.y, 0f));
            if (_fog != null)
            {
                var parameters = new ParticleSystem.EmitParams
                {
                    position = position + Random.insideUnitCircle * 2f,
                    startColor = new Color(0.01f, 0.01f, 0.015f, 0.55f),
                    startSize = Random.Range(4f, 7f),
                    startLifetime = 3600f
                };
                _fog.Emit(parameters, 2);
            }
        }

        private void FixedUpdate()
        {
            if (_scene == null || _points.Count < 2) return;
            lock (_scene.Units.LockObject)
            {
                foreach (var unit in _scene.Units.Items)
                {
                    if (!unit.IsActive() || unit == _owner || !InsideTrail(unit.Body.Position)) continue;
                    if (unit.Type.Class == UnitClass.Ship || unit.Type.Class == UnitClass.Drone)
                        unit.Body.ApplyAcceleration(-unit.Body.Velocity * 0.9f);
                    else if (unit.Type.Class == UnitClass.Missile || unit.Type.Class == UnitClass.EnergyBolt)
                        unit.Vanish();
                }
            }
        }

        private bool InsideTrail(Vector2 position)
        {
            for (var i = 1; i < _points.Count; i++)
            {
                var a = _points[i - 1];
                var b = _points[i];
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(position - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
                if (Vector2.SqrMagnitude(position - (a + ab * t)) <= 9f) return true;
            }
            return false;
        }

        public static bool TryBlockRay(Vector2 origin, Vector2 direction, float maxRange, out float blockDistance)
        {
            blockDistance = maxRange;
            var rayEnd = origin + direction.normalized * maxRange;
            var blocked = false;
            foreach (var trail in ActiveTrails.Where(item => item != null && item._points.Count > 1))
            {
                for (var i = 1; i < trail._points.Count; i++)
                {
                    if (!ClosestSegmentParameters(origin, rayEnd, trail._points[i - 1], trail._points[i], out var rayT, out var distance))
                        continue;
                    if (distance > TrailRadius)
                        continue;

                    blockDistance = Mathf.Min(blockDistance, Mathf.Max(0f, rayT * maxRange - TrailRadius));
                    blocked = true;
                }
            }
            return blocked;
        }

        private static bool ClosestSegmentParameters(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2, out float firstT, out float distance)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            var a = Vector2.Dot(d1, d1);
            var e = Vector2.Dot(d2, d2);
            var f = Vector2.Dot(d2, r);
            float s;
            float t;

            if (a <= 0.0001f && e <= 0.0001f)
            {
                firstT = 0f;
                distance = Vector2.Distance(p1, p2);
                return true;
            }
            if (a <= 0.0001f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                var c = Vector2.Dot(d1, r);
                if (e <= 0.0001f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    var b = Vector2.Dot(d1, d2);
                    var denominator = a * e - b * b;
                    s = denominator != 0f ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            firstT = s;
            distance = Vector2.Distance(p1 + d1 * s, p2 + d2 * t);
            return true;
        }

        private void CreateFog()
        {
            _fog = gameObject.AddComponent<ParticleSystem>();
            var main = _fog.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = 3600f;
            main.maxParticles = 5000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _fog.emission;
            emission.enabled = false;
            var shape = _fog.shape;
            shape.enabled = false;
            var renderer = _fog.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 19;
            _fog.Play();
        }

        private void OnDestroy()
        {
            ActiveTrails.Remove(this);
        }

        private IScene _scene;
        private IShip _owner;
        private LineRenderer _line;
        private ParticleSystem _fog;
        private readonly List<Vector2> _points = new();
        private const float TrailRadius = 3f;
        private static readonly List<WarpTrailEffect> ActiveTrails = new();
    }
}
