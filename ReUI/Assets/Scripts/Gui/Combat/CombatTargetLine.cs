using Combat.Component.Ship;
using Combat.Component.Unit;
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
                BattlefieldGeometry.Distance(player.Body.WorldPosition(), target.Body.WorldPosition()) > CombatMinimap.GetRadarRange(player))
            {
                _line.enabled = false;
                return;
            }

            var color = HighlightedTargetColor(target);
            color.a = 0.9f;
            _line.startColor = color;
            _line.endColor = new Color(color.r, color.g, color.b, 0.55f);
            _line.SetPosition(0, player.Body.VisualPosition);
            _line.SetPosition(1, BattlefieldGeometry.NearestEquivalent(player.Body.VisualWorldPosition(), target.Body.VisualWorldPosition()));
            _line.enabled = true;
        }

        public static Color TargetColor(IShip ship)
        {
            if (ship is Decoy { IsCounterElectron: true })
                return new Color(0.2f, 0.72f, 1f, 1f);

            return ship.Specification.Stats.ShipModel.SizeClass switch
            {
                SizeClass.Cruiser => new Color(1f, 0.45f, 0.05f),
                SizeClass.Battleship => new Color(1f, 0.45f, 0.05f),
                SizeClass.Titan => new Color(1f, 0.82f, 0.1f),
                SizeClass.Starbase => new Color(0.68f, 0.2f, 1f),
                _ => Color.red
            };
        }

        public static Color HighlightedTargetColor(IShip ship)
        {
            var color = TargetColor(ship);
            Color.RGBToHSV(color, out var hue, out var saturation, out var value);
            var highlighted = Color.HSVToRGB(hue, Mathf.Max(0.92f, saturation), 1f);
            highlighted.a = 1f;
            return highlighted;
        }

        private IScene _scene;
        private LineRenderer _line;
    }
}
