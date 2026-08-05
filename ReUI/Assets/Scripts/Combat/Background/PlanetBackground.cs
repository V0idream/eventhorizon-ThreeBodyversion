using System;
using Game.Exploration;
using Services.Resources;
using UnityEngine;
using Zenject;

namespace Combat.Background
{
    public class PlanetBackground : MonoBehaviour
    {
        [SerializeField] private float _size = 50f;
        [SerializeField] private Material _gasPlanetMaterial;
        [SerializeField] private Material _barrenPlanetMaterial;
        [SerializeField] private Material _infectedPlanetMaterial;

        [Inject]
        public void Initialize(IResourceLocator resourceLocator, Planet planet)
        {
            _planet = planet;
			_meshRenderer = gameObject.GetComponent<MeshRenderer>();
			if (_meshRenderer == null)
				_meshRenderer = gameObject.AddComponent<MeshRenderer>();

            // The old code made both sides of the mesh equal to
            // size * screenAspect.  On wide displays that produced a square
            // planetary surface in the middle of the camera, leaving the
            // camera clear colour visible down both sides of exploration.
            // Keep a unit mesh and scale it to the actual camera viewport
            // instead, so the planet always covers the complete view.
            Primitives.CreatePlane(gameObject.GetMesh(), 1f, 1f, 8);
            UpdateViewSize();

            switch (planet.Type)
            {
                case PlanetType.Gas:
                    InitializeMaterial(_gasPlanetMaterial, Color.Lerp(_planet.Color, Color.black, 0.75f));
                    break;
                case PlanetType.Infected:
                    InitializeMaterial(_infectedPlanetMaterial, Color.Lerp(_planet.Color, Color.black, 0.3f));
                    break;
                case PlanetType.Barren:
                case PlanetType.Terran:
                    InitializeMaterial(_barrenPlanetMaterial, Color.Lerp(_planet.Color, Color.black, 0.3f));
                    break;
                default:
                    throw new ArgumentException("PlanetBackground: Wrong planet type - " + planet.Type);
            }
        }

        private void InitializeMaterial(Material source, Color color)
        {
			if (_materialCopy != null)
				Destroy(_materialCopy);

			_meshRenderer.material = source;
			_materialCopy = _meshRenderer.material;
			_materialCopy.color = color;
			_baseMainTextureScale = _materialCopy.mainTextureScale;
        }

        private void LateUpdate()
        {
            UpdateViewSize();
			UpdateMaterial();
        }

        private void UpdateViewSize()
        {
            var camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                _height = _size * Screen.width / Mathf.Max(1, Screen.height);
                _width = _height * Screen.width / Mathf.Max(1, Screen.height);
            }
            else
            {
                _height = 2f * camera.orthographicSize;
                _width = _height * camera.aspect;
            }

            _width *= BackgroundOverscan;
            _height *= BackgroundOverscan;
            transform.localScale = new Vector3(_width, _height, 1f);
        }

        private void UpdateMaterial()
        {
			if (_materialCopy == null)
				return;

			// A hostile hive station is instantiated only when the player enters its
			// activation radius. The combat camera then expands to include it. The
			// old offset divided by the changing viewport size, so the infected
			// ground jumped and stretched exactly when the station appeared.
			var camera = UnityEngine.Camera.main;
			var cameraPosition = camera != null ? camera.transform.position : transform.position;
			var worldTileSize = Mathf.Max(1f, _size);
			_materialCopy.mainTextureScale = Vector2.Scale(
				_baseMainTextureScale,
				new Vector2(_width / worldTileSize, _height / worldTileSize));

			var offset = Repeat01(new Vector2(
				cameraPosition.x / worldTileSize,
				cameraPosition.y / worldTileSize));
			_materialCopy.mainTextureOffset = offset;

			if (_planet.Type == PlanetType.Gas)
			{
				_materialCopy.SetTextureOffset("_DecalTex", Repeat01(offset * 2f));
				_materialCopy.SetTextureOffset("_CloudsTex", Repeat01(offset * 3f));
			}
        }

		private static Vector2 Repeat01(Vector2 value)
		{
			value.x -= Mathf.Floor(value.x);
			value.y -= Mathf.Floor(value.y);
			return value;
		}

		private void OnDestroy()
		{
			if (_meshRenderer != null)
				_meshRenderer.material = null;
			if (_materialCopy != null)
				Destroy(_materialCopy);
		}

        private Planet _planet;
		private MeshRenderer _meshRenderer;
		private Material _materialCopy;
		private Vector2 _baseMainTextureScale = Vector2.one;
        private float _width;
        private float _height;

        private const float BackgroundOverscan = 1.05f;
    }
}
