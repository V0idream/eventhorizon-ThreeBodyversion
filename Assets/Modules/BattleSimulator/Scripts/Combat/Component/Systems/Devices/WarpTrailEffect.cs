using System.Collections.Generic;
using Combat.Component.Ship;
using Combat.Component.Unit.Classification;
using Combat.Scene;
using Combat.Unit;
using UnityEngine;

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
            return effect;
        }

        public void Record(Vector2 position)
        {
            if (_points.Count > 0 && Vector2.SqrMagnitude(_points[^1] - position) < 1f) return;
            _points.Add(position);
            _line.positionCount = _points.Count;
            _line.SetPosition(_points.Count - 1, new Vector3(position.x, position.y, 0f));
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

        private IScene _scene;
        private IShip _owner;
        private LineRenderer _line;
        private readonly List<Vector2> _points = new();
    }
}
