using GameDatabase.Enums;
using GameDatabase.Extensions;
using UnityEngine;

namespace Combat.Component.View
{
    [RequireComponent(typeof(LineRenderer))]
    public class LaserView : BaseView
    {
        [SerializeField] private float _alphaScale = 1.0f;
        [SerializeField] private float _thickness = 1.0f;
        [SerializeField] private float _borderSize = 0.2f;
        [SerializeField] private Color _startColor = Color.white;
        [SerializeField] private Color _endColor = Color.white;
        [SerializeField] private Color _baseColor = Color.white;
        [SerializeField] private ColorMode _colorMode = ColorMode.TakeFromOwner;
        
        public void Initialize(Color baseColor, ColorMode colorMode)
        {
            _baseColor = baseColor;
            _colorMode = colorMode;
            if (_coreRenderer != null && _lineRenderer != null)
                _coreRenderer.sharedMaterial = _lineRenderer.sharedMaterial;
        }

		public override void Dispose() {}

        public float BorderSize { get { return _borderSize; } set { _borderSize = value; } }
        public float Thickness { get { return _thickness; } set { _thickness = value; } }

        protected override void OnGameObjectCreated()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.positionCount = 4;
            _lineRenderer.enabled = false;

            var core = new GameObject("Native HDR Core");
            core.layer = gameObject.layer;
            core.transform.SetParent(transform, false);
            _coreRenderer = core.AddComponent<LineRenderer>();
            _coreRenderer.useWorldSpace = _lineRenderer.useWorldSpace;
            _coreRenderer.alignment = _lineRenderer.alignment;
            _coreRenderer.textureMode = _lineRenderer.textureMode;
            _coreRenderer.numCapVertices = _lineRenderer.numCapVertices;
            _coreRenderer.numCornerVertices = _lineRenderer.numCornerVertices;
            _coreRenderer.sortingLayerID = _lineRenderer.sortingLayerID;
            _coreRenderer.sortingOrder = _lineRenderer.sortingOrder + 1;
            _coreRenderer.sharedMaterial = _lineRenderer.sharedMaterial;
            _coreRenderer.positionCount = 4;
            _coreRenderer.enabled = false;
        }

        protected override void OnGameObjectDestroyed() {}

        protected override void UpdateLife(float life)
        {
            Opacity = 1f - (1f - Life)*(1f - Life);
        }

        protected override void UpdatePosition(Vector2 position) {}
        protected override void UpdateRotation(float rotation) {}

        protected override void UpdateSize(float size)
        {
			var scale = transform.localScale.z;
            UpdateLine(size/scale, _thickness*scale);
        }

        protected override void UpdateColor(Color color)
        {
            if (!_lineRenderer) return;

            color = _colorMode.Apply(_baseColor, color);
            color.a *= _alphaScale;
            _lineRenderer.startColor = color * _startColor;
            _lineRenderer.endColor = color * _endColor;
            SetHdrIntensity(_lineRenderer, 500f);
            if (_coreRenderer != null)
            {
                _coreRenderer.startColor = color * _startColor;
                _coreRenderer.endColor = color * _endColor;
                SetHdrIntensity(_coreRenderer, 1200f);
            }
        }

        private void UpdateLine(float size, float thikness)
        {
            if (!_lineRenderer) return;

            if (size < 2*_borderSize)
            {
                _lineRenderer.enabled = false;
                if (_coreRenderer != null) _coreRenderer.enabled = false;
                return;
            }

            _lineRenderer.enabled = true;
            if (_coreRenderer != null) _coreRenderer.enabled = NativeHdrContent.IsActive;
            _lineRenderer.startWidth = thikness;
            _lineRenderer.endWidth = thikness;
            _lineRenderer.SetPosition(0, Vector3.zero);
            _lineRenderer.SetPosition(1, new Vector3(_borderSize,0,0));
            _lineRenderer.SetPosition(2, new Vector3(size - _borderSize,0,0));
            _lineRenderer.SetPosition(3, new Vector3(size, 0, 0));
            if (_coreRenderer != null)
            {
                // A narrow, very bright core inside a broader glow produces
                // genuine HDR contrast without a full-screen bloom/filter.
                var coreThickness = thikness * 0.34f;
                _coreRenderer.startWidth = coreThickness;
                _coreRenderer.endWidth = coreThickness;
                _coreRenderer.SetPosition(0, Vector3.zero);
                _coreRenderer.SetPosition(1, new Vector3(_borderSize, 0, 0));
                _coreRenderer.SetPosition(2, new Vector3(size - _borderSize, 0, 0));
                _coreRenderer.SetPosition(3, new Vector3(size, 0, 0));
            }
        }

        private LineRenderer _lineRenderer;
        private LineRenderer _coreRenderer;

        private static readonly int HdrIntensityId = Shader.PropertyToID("_HdrIntensity");

        private static void SetHdrIntensity(Renderer renderer, float targetNits)
        {
            if (renderer == null) return;
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetFloat(HdrIntensityId, NativeHdrContent.IntensityForNits(targetNits));
            renderer.SetPropertyBlock(properties);
        }
    }
}
