using Combat.Component.Ship;
using Combat.Scene;
using Combat.Unit;
using GameDatabase.Enums;
using UnityEngine;

namespace Gui.Combat
{
    public sealed class CombatTargetLine : MonoBehaviour
    {
        public void Initialize(IScene scene)
        {
            _scene = scene;
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.widthMultiplier = 0.07f;
            _line.numCapVertices = 2;
            _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.sortingOrder = 18;
            _line.enabled = false;
        }

        private void LateUpdate()
        {
            var player = _scene?.PlayerShip;
            var target = _scene?.LockedEnemyShip;
            if (!player.IsActive() || !target.IsActive() ||
                Vector2.Distance(player.Body.Position, target.Body.Position) > CombatMinimap.GetRadarRange(player))
            {
                _line.enabled = false;
                return;
            }

            var color = TargetColor(target);
            color.a = 0.72f;
            _line.startColor = color;
            _line.endColor = new Color(color.r, color.g, color.b, 0.25f);
            _line.SetPosition(0, player.Body.VisualPosition);
            _line.SetPosition(1, target.Body.VisualPosition);
            _line.enabled = true;
        }

        public static Color TargetColor(IShip ship)
        {
            return ship.Specification.Stats.ShipModel.SizeClass switch
            {
                SizeClass.Cruiser => new Color(1f, 0.45f, 0.05f),
                SizeClass.Battleship => new Color(1f, 0.45f, 0.05f),
                SizeClass.Titan => new Color(1f, 0.82f, 0.1f),
                SizeClass.Starbase => new Color(0.15f, 0.55f, 1f),
                _ => Color.red
            };
        }

        private IScene _scene;
        private LineRenderer _line;
    }
}
