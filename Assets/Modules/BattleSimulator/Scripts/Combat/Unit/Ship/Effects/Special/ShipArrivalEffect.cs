using System.Collections.Generic;
using Combat.Component.Engine;
using Combat.Component.Features;
using Combat.Component.Ship;
using Combat.Component.Stats;
using Combat.Component.Systems;
using Combat.Component.Triggers;
using Combat.Helpers;
using UnityEngine;

namespace Combat.Component.Ship.Effects.Special
{
    /// <summary>
    /// One-second, collision-free arrival shared by every newly created ship.
    /// A local-UV mask reveals the hull from bow to stern while the physical
    /// body decelerates continuously from its entry velocity.
    /// </summary>
    public sealed class ShipArrivalEffect : IShipEffect, IEngineModification, ISystemsModification
    {
        public ShipArrivalEffect(IShip ship, GameObjectHolder holder)
        {
            _ship = ship;
            _root = holder.Transform;
            _ship.Collider.Enabled = false;
            CreateRevealMaterials();
            CreateBeam();

            _direction = RotationHelpers.Direction(_ship.Body.Rotation);
            _normalSpeed = Mathf.Max(8f, _ship.Engine.MaxVelocity);
            _entrySpeed = Mathf.Max(120f, _normalSpeed * 3.5f);
            SetVelocity(_direction * _entrySpeed);
        }

        public bool IsAlive => !_completed;
        public IEngineModification EngineModification => this;
        public IFeaturesModification FeaturesModification => null;
        public ISystemsModification SystemsModification => this;
        public IStatsModification StatsModification => null;
        public IUnitAction UnitAction => null;

        public bool CanActivateSystem(ISystem system) => _completed;
        public void OnSystemActivated(ISystem system) { }

        public bool TryApplyModification(ref EngineData data)
        {
            if (_completed)
                return false;

            data.Throttle = 0f;
            data.Deceleration = 0f;
            data.Propulsion = 0f;
            data.TurnRate = 0f;
            data.HasCourse = false;
            return true;
        }

        public void UpdatePhysics(IShip ship, float elapsedTime)
        {
            if (_completed)
                return;

            _elapsed = Mathf.Min(Duration, _elapsed + elapsedTime);
            var progress = Mathf.Clamp01(_elapsed / Duration);
            var smooth = progress * progress * (3f - 2f * progress);
            SetVelocity(_direction * Mathf.Lerp(_entrySpeed, _normalSpeed, smooth));

            if (_elapsed < Duration)
                return;

            _completed = true;
            SetVelocity(_direction * _normalSpeed);
            _ship.Collider.Enabled = true;
            RestoreVisuals();
        }

        public void UpdateView(IShip ship, float elapsedTime)
        {
            if (_completed)
                return;

            var reveal = Mathf.Clamp01((_elapsed - BeamLeadTime) / RevealDuration);
            var fade = _elapsed < BeamLeadTime
                ? Mathf.Clamp01(_elapsed / 0.1f)
                : 1f - Mathf.Clamp01((_elapsed - (Duration - 0.65f)) / 0.65f);
            foreach (var item in _materials)
                if (item.RevealMaterial != null)
                    item.RevealMaterial.SetFloat(RevealId, reveal);

            if (_beam != null)
            {
                var color = new Color(0.5f, 0.92f, 1f, Mathf.Clamp01(fade));
                _beam.startColor = color;
                _beam.endColor = new Color(0.12f, 0.62f, 1f, color.a * 0.15f);
            }

            if (_beamGlow != null)
            {
                var glowColor = new Color(0.25f, 0.65f, 1f, Mathf.Clamp01(fade) * 0.2f);
                _beamGlow.startColor = glowColor;
                _beamGlow.endColor = new Color(0.08f, 0.3f, 1f, glowColor.a * 0.12f);
            }
        }

        public void Dispose()
        {
            RestoreVisuals();
        }

        private void SetVelocity(Vector2 velocity)
        {
            if (_ship.Body is Combat.Component.Body.RigidBodyAdapter rigidBody)
                rigidBody.Velocity = velocity;
            else
                _ship.Body.ApplyAcceleration(velocity - _ship.Body.Velocity);
        }

        private void CreateRevealMaterials()
        {
            var shader = Shader.Find("ThreeBody/ShipArrivalReveal");
            if (shader == null)
                return;

            foreach (var renderer in _root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null || renderer.sprite == null)
                    continue;

                var material = new Material(shader)
                {
                    name = "ShipArrivalReveal (Runtime)",
                    mainTexture = renderer.sprite.texture,
                };
                material.SetFloat(RevealId, 0f);
                _materials.Add(new RendererMaterial(renderer, renderer.sharedMaterial, material));
                renderer.sharedMaterial = material;
            }
        }

        private void CreateBeam()
        {
            var mainRenderer = _root.GetComponentInChildren<SpriteRenderer>(true);
            if (mainRenderer == null || mainRenderer.sprite == null)
                return;

            var beamObject = new GameObject("ArrivalBeam");
            beamObject.transform.SetParent(_root, false);
            _beam = beamObject.AddComponent<LineRenderer>();
            _beam.useWorldSpace = false;
            _beam.positionCount = 2;
            var length = Mathf.Max(2f, mainRenderer.sprite.bounds.size.y * 1.45f);
            _beam.SetPosition(0, new Vector3(0f, -length * 0.58f, -0.02f));
            _beam.SetPosition(1, new Vector3(0f, length * 0.58f, -0.02f));
            _beam.widthMultiplier = Mathf.Clamp(length * 0.035f, 0.045f, 0.22f);
            _beam.numCapVertices = 4;
            _beam.sortingLayerID = mainRenderer.sortingLayerID;
            _beam.sortingOrder = mainRenderer.sortingOrder + 8;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
                _beamMaterial = _beam.material = new Material(shader) { name = "ShipArrivalBeam (Runtime)" };

            var glowObject = new GameObject("ArrivalBeamGlow");
            glowObject.transform.SetParent(_root, false);
            _beamGlow = glowObject.AddComponent<LineRenderer>();
            _beamGlow.useWorldSpace = false;
            _beamGlow.positionCount = 2;
            _beamGlow.SetPosition(0, new Vector3(0f, -length * 0.58f, -0.025f));
            _beamGlow.SetPosition(1, new Vector3(0f, length * 0.58f, -0.025f));
            _beamGlow.widthMultiplier = _beam.widthMultiplier * 3.2f;
            _beamGlow.numCapVertices = 6;
            _beamGlow.sortingLayerID = mainRenderer.sortingLayerID;
            _beamGlow.sortingOrder = mainRenderer.sortingOrder + 7;
            if (shader != null)
                _beamGlowMaterial = _beamGlow.material = new Material(shader) { name = "ShipArrivalBeamGlow (Runtime)" };
        }

        private void RestoreVisuals()
        {
            if (_visualsRestored)
                return;
            _visualsRestored = true;

            foreach (var item in _materials)
            {
                if (item.Renderer != null)
                    item.Renderer.sharedMaterial = item.OriginalMaterial;
                if (item.RevealMaterial != null)
                    Object.Destroy(item.RevealMaterial);
            }
            _materials.Clear();

            if (_beam != null)
                Object.Destroy(_beam.gameObject);
            if (_beamMaterial != null)
                Object.Destroy(_beamMaterial);
            if (_beamGlow != null)
                Object.Destroy(_beamGlow.gameObject);
            if (_beamGlowMaterial != null)
                Object.Destroy(_beamGlowMaterial);
            _beam = null;
            _beamMaterial = null;
            _beamGlow = null;
            _beamGlowMaterial = null;
        }

        private readonly struct RendererMaterial
        {
            public RendererMaterial(SpriteRenderer renderer, Material originalMaterial, Material revealMaterial)
            {
                Renderer = renderer;
                OriginalMaterial = originalMaterial;
                RevealMaterial = revealMaterial;
            }

            public SpriteRenderer Renderer { get; }
            public Material OriginalMaterial { get; }
            public Material RevealMaterial { get; }
        }

        private const float Duration = 1.55f;
        private const float BeamLeadTime = 0.22f;
        private const float RevealDuration = 1.12f;
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private readonly IShip _ship;
        private readonly Transform _root;
        private readonly List<RendererMaterial> _materials = new();
        private Vector2 _direction;
        private float _normalSpeed;
        private float _entrySpeed;
        private float _elapsed;
        private bool _completed;
        private bool _visualsRestored;
        private LineRenderer _beam;
        private Material _beamMaterial;
        private LineRenderer _beamGlow;
        private Material _beamGlowMaterial;
    }
}
