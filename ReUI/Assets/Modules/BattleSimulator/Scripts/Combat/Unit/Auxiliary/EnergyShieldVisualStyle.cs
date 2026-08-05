using System;
using Combat.Component.Ship;
using Combat.Component.View;
using UnityEngine;

namespace Combat.Component.Unit
{
    public enum EnergyShieldVisualStyle
    {
        Classic = 0,
        Modern = 1,
    }

    /// <summary>
    /// Player-facing shield presentation preference. Modern shields use the
    /// same hull outline for both rendering and collision.
    /// </summary>
    public static class EnergyShieldVisualSettings
    {
        private const string PreferenceKey = "ThreeBody.EnergyShieldVisualStyle";

        public static event Action StyleChanged;

        public static EnergyShieldVisualStyle Style
        {
            get
            {
                var value = PlayerPrefs.GetInt(PreferenceKey, (int)EnergyShieldVisualStyle.Classic);
                return value == (int)EnergyShieldVisualStyle.Modern
                    ? EnergyShieldVisualStyle.Modern
                    : EnergyShieldVisualStyle.Classic;
            }
            set
            {
                var normalized = value == EnergyShieldVisualStyle.Modern
                    ? EnergyShieldVisualStyle.Modern
                    : EnergyShieldVisualStyle.Classic;
                if (Style == normalized)
                    return;

                PlayerPrefs.SetInt(PreferenceKey, (int)normalized);
                PlayerPrefs.Save();
                StyleChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Keeps the original circular shield available while providing a
    /// lightweight ship-hull outline.  The object is parented to the ship body,
    /// so the modern outline follows translation and rotation automatically.
    /// </summary>
    public sealed class EnergyShieldVisualController : MonoBehaviour
    {
        public void Initialize(IShip ship, IView classicView, LineRenderer outline, Color color,
            bool forceClassic = false)
        {
            _ship = ship;
            _classicRenderer = (classicView as UnityEngine.Component)?.GetComponent<SpriteRenderer>();
            _outline = outline;
            _color = color;
            _forceClassic = forceClassic;

            ConfigureOutline();
            ConfigureColliders();
            RebuildOutline();
            ApplyStyle();
            EnergyShieldVisualSettings.StyleChanged += ApplyStyle;
        }

        private void OnDestroy()
        {
            EnergyShieldVisualSettings.StyleChanged -= ApplyStyle;
        }

        private void LateUpdate()
        {
            if (_outline == null || !_outline.enabled)
                return;

            // A pooled ship view can finish assigning its sprite/collider one
            // frame after the shield is created. Retry until an outline exists.
            if (_outline.positionCount < 3)
                RebuildOutline();

            var alpha = _classicRenderer != null ? _classicRenderer.color.a : _color.a;
            var visibleColor = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(alpha * 1.65f));
            _outline.startColor = visibleColor;
            _outline.endColor = visibleColor;
        }

        private void ConfigureOutline()
        {
            if (_outline == null)
                return;

            _outline.useWorldSpace = false;
            _outline.loop = true;
            _outline.widthMultiplier = 0.035f;
            _outline.numCapVertices = 2;
            _outline.numCornerVertices = 2;
            _outline.textureMode = LineTextureMode.Stretch;
            _outline.alignment = LineAlignment.TransformZ;
            if (_classicRenderer != null)
            {
                _outline.sharedMaterial = _classicRenderer.sharedMaterial;
                _outline.sortingLayerID = _classicRenderer.sortingLayerID;
                _outline.sortingOrder = _classicRenderer.sortingOrder + 1;
            }
        }

        private void ApplyStyle()
        {
            var modern = !_forceClassic &&
                         EnergyShieldVisualSettings.Style == EnergyShieldVisualStyle.Modern;
            if (_classicRenderer != null)
                _classicRenderer.enabled = !modern;
            if (_outline != null)
                _outline.enabled = modern;
            if (modern && _outline != null && _outline.positionCount < 3)
                RebuildOutline();
            ApplyCollisionStyle(_shieldEnabled);
        }

        public void ApplyCollisionStyle(bool shieldEnabled)
        {
            _shieldEnabled = shieldEnabled;
            var modern = !_forceClassic &&
                         EnergyShieldVisualSettings.Style == EnergyShieldVisualStyle.Modern;
            if (_circleCollider != null)
                _circleCollider.enabled = shieldEnabled && !modern;
            if (_outlineCollider != null)
                _outlineCollider.enabled = shieldEnabled && modern && _outlineCollider.pathCount > 0;
        }

        private void ConfigureColliders()
        {
            _circleCollider = GetComponent<CircleCollider2D>();
            _outlineCollider = GetComponent<PolygonCollider2D>() ?? gameObject.AddComponent<PolygonCollider2D>();
            if (_circleCollider != null)
            {
                _outlineCollider.isTrigger = _circleCollider.isTrigger;
                _outlineCollider.sharedMaterial = _circleCollider.sharedMaterial;
            }
            _outlineCollider.enabled = false;
        }

        private void RebuildOutline()
        {
            if (_outline == null || _ship == null || _ship.View is not UnityEngine.Component shipView)
                return;

            var polygon = shipView.GetComponent<PolygonCollider2D>() ??
                          shipView.GetComponentInChildren<PolygonCollider2D>(true);
            if (polygon != null && polygon.pathCount > 0)
            {
                var pathIndex = FindLargestPath(polygon);
                var path = polygon.GetPath(pathIndex);
                if (path.Length >= 3)
                {
                    var points = new Vector3[path.Length];
                    for (var i = 0; i < path.Length; i++)
                    {
                        var world = polygon.transform.TransformPoint(path[i]);
                        var local = transform.InverseTransformPoint(world);
                        points[i] = new Vector3(local.x * OutlineScale, local.y * OutlineScale, 0f);
                    }
                    SetOutline(points);
                    return;
                }
            }

            var spriteRenderer = shipView.GetComponent<SpriteRenderer>() ??
                                 shipView.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer == null || spriteRenderer.sprite == null)
                return;

            var bounds = spriteRenderer.sprite.bounds;
            var corners = new[]
            {
                new Vector2(bounds.min.x, bounds.min.y),
                new Vector2(bounds.min.x, bounds.max.y),
                new Vector2(bounds.max.x, bounds.max.y),
                new Vector2(bounds.max.x, bounds.min.y),
            };
            var fallback = new Vector3[corners.Length];
            for (var i = 0; i < corners.Length; i++)
            {
                var world = spriteRenderer.transform.TransformPoint(corners[i]);
                var local = transform.InverseTransformPoint(world);
                fallback[i] = new Vector3(local.x * OutlineScale, local.y * OutlineScale, 0f);
            }
            SetOutline(fallback);
        }

        private void SetOutline(Vector3[] points)
        {
            _outline.positionCount = points.Length;
            _outline.SetPositions(points);
            if (_outlineCollider == null || points.Length < 3)
                return;

            var path = new Vector2[points.Length];
            for (var i = 0; i < points.Length; i++)
                path[i] = new Vector2(points[i].x, points[i].y);
            _outlineCollider.pathCount = 1;
            _outlineCollider.SetPath(0, path);
            ApplyCollisionStyle(_shieldEnabled);
        }

        private static int FindLargestPath(PolygonCollider2D polygon)
        {
            var largestIndex = 0;
            var largestArea = 0f;
            for (var pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
            {
                var path = polygon.GetPath(pathIndex);
                var area = 0f;
                for (var i = 0; i < path.Length; i++)
                {
                    var next = path[(i + 1) % path.Length];
                    area += path[i].x * next.y - next.x * path[i].y;
                }
                area = Mathf.Abs(area);
                if (area <= largestArea)
                    continue;
                largestArea = area;
                largestIndex = pathIndex;
            }
            return largestIndex;
        }

        private const float OutlineScale = 1.08f;
        private IShip _ship;
        private SpriteRenderer _classicRenderer;
        private LineRenderer _outline;
        private CircleCollider2D _circleCollider;
        private PolygonCollider2D _outlineCollider;
        private Color _color;
        private bool _forceClassic;
        private bool _shieldEnabled;
    }
}
