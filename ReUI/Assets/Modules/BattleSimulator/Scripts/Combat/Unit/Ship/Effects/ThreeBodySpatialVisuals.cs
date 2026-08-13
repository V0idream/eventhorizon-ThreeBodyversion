using System.Collections.Generic;
using UnityEngine;

namespace Combat.Component.Ship.Effects
{
    public static class ThreeBodySpatialVisuals
    {
        public static void PlayPortal(Vector2 position, Color color, bool opening) =>
            PlayUniverseTransit(position, color, opening);

        public static void PlayUniverseTransit(Vector2 position, Color color, bool opening)
        {
            var root = new GameObject(opening ? "SmallUniverseExit" : "SmallUniverseEntry");
            root.transform.position = position;
            root.AddComponent<SpatialPulseVisual>().Initialize(color, opening ? 1.15f : 0.9f, opening);
        }

        public static void PlayRiftTeleport(Vector2 from, Vector2 to)
        {
            PlayUniverseTransit(from, new Color(0.28f, 0.92f, 1f, 1f), false);
            PlayUniverseTransit(to, new Color(0.82f, 0.32f, 1f, 1f), true);
        }
    }

    internal sealed class SpatialPulseVisual : MonoBehaviour
    {
        public void Initialize(Color color, float duration, bool opening)
        {
            _color = Color.Lerp(color, new Color(0.35f, 0.9f, 1f, 1f), 0.42f);
            _duration = duration;
            _opening = opening;
            _material = new Material(Shader.Find("Sprites/Default"));

            for (var ring = 0; ring < 7; ring++)
                _rings.Add(CreateRing(ring));

            for (var spoke = 0; spoke < 12; spoke++)
            {
                var child = new GameObject("UniverseSpoke" + spoke);
                child.transform.SetParent(transform, false);
                child.transform.localRotation = Quaternion.Euler(0f, 0f, spoke * 30f);
                var line = child.AddComponent<LineRenderer>();
                line.material = _material;
                line.useWorldSpace = false;
                line.positionCount = 5;
                line.startWidth = 0.085f;
                line.endWidth = 0.018f;
                line.numCapVertices = 2;
                for (var p = 0; p < 5; p++)
                {
                    var distance = 0.18f + p * 0.52f;
                    var bend = (p % 2 == 0 ? -1f : 1f) * 0.07f;
                    line.SetPosition(p, new Vector3(distance, bend, 0f));
                }
                _spokes.Add(line);
            }

            var core = new GameObject("UniverseCore");
            core.transform.SetParent(transform, false);
            _core = core.AddComponent<SpriteRenderer>();
            _core.sprite = CreateDiscSprite();
            _core.color = Color.white;
            _core.sortingOrder = 121;
        }

        private LineRenderer CreateRing(int ring)
        {
            var child = new GameObject("UniverseRing" + ring);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.material = _material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 97;
            line.startWidth = line.endWidth = 0.045f + ring * 0.018f;
            line.numCornerVertices = 3;
            line.sortingOrder = 120 + ring;
            for (var i = 0; i < 97; i++)
            {
                var angle = i * Mathf.PI * 2f / 96f;
                var radius = 0.55f + ring * 0.24f + 0.075f * Mathf.Sin(angle * (5f + ring % 3) + ring * 0.73f);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
            return line;
        }

        private static Sprite CreateDiscSprite()
        {
            if (_discSprite) return _discSprite;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "SmallUniverseEnergyCore";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                var alpha = (byte)(Mathf.Clamp01(1f - distance) * 235f);
                pixels[y * size + x] = new Color32(220, 248, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _discSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 32f);
            return _discSprite;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            var t = Mathf.Clamp01(_time / Mathf.Max(0.01f, _duration));
            var shaped = Mathf.SmoothStep(0f, 1f, t);
            var scale = _opening ? Mathf.Lerp(0.08f, 4.8f, shaped) : Mathf.Lerp(4.8f, 0.08f, shaped);
            transform.localScale = Vector3.one * scale;
            transform.Rotate(0f, 0f, (_opening ? -260f : 260f) * Time.deltaTime);
            var alpha = Mathf.Sin(t * Mathf.PI);
            var pulse = 0.78f + 0.22f * Mathf.Sin(_time * 24f);

            for (var i = 0; i < _rings.Count; i++)
            {
                var c = Color.Lerp(_color, i % 2 == 0 ? Color.white : new Color(0.72f, 0.3f, 1f, 1f), 0.35f);
                c.a = alpha * pulse * (0.92f - i * 0.055f);
                _rings[i].startColor = _rings[i].endColor = c;
                _rings[i].transform.Rotate(0f, 0f, (i % 2 == 0 ? 1f : -1f) * (30f + i * 9f) * Time.deltaTime);
            }

            foreach (var line in _spokes)
            {
                var c = _color;
                c.a = alpha * 0.78f;
                line.startColor = c;
                c.a *= 0.15f;
                line.endColor = c;
            }

            if (_core)
            {
                var c = Color.Lerp(_color, Color.white, 0.8f);
                c.a = alpha * pulse;
                _core.color = c;
                _core.transform.localScale = Vector3.one * (1.5f + pulse * 0.7f);
            }

            if (t >= 1f) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_material) Destroy(_material);
        }

        private static Sprite _discSprite;
        private readonly List<LineRenderer> _rings = new();
        private readonly List<LineRenderer> _spokes = new();
        private Material _material;
        private SpriteRenderer _core;
        private Color _color;
        private float _duration;
        private float _time;
        private bool _opening;
    }
}
