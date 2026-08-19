using Combat.Collision;
using Combat.Collision.Behaviour;
using Combat.Collision.Manager;
using Combat.Component.Body;
using Combat.Component.Collider;
using Combat.Component.Controls;
using Combat.Component.Engine;
using Combat.Component.Features;
using Combat.Component.Platform;
using Combat.Component.Ship;
using Combat.Component.Ship.Effects;
using Combat.Component.Stats;
using Combat.Component.Systems;
using Combat.Component.Triggers;
using Combat.Component.Unit.Classification;
using Combat.Component.View;
using Combat.Factory;
using Combat.Unit;
using Constructor;
using UnityEngine;

namespace Combat.Component.Unit
{
    public class Decoy : UnitBase, IShip
    {
        public Decoy(IShip parent, IBody body, IView view, ICollider collider, float hitPoints, float lifetime,
            bool counterElectron = false, bool visibleHologram = true, EffectFactory effectFactory = null)
            : base(new UnitType(UnitClass.Decoy, parent.Type.Side, parent), body, view, collider, null)
        {
            _lifetime = lifetime;
            _hitPoints = hitPoints;
            _features = new Features.Features(TargetPriority.High, Color.white);
            IsCounterElectron = counterElectron;
            _visibleHologram = visibleHologram;
            _effectFactory = effectFactory;
            _hologramColor = view.Color;
        }

        public bool IsCounterElectron { get; }

        public override ICollisionBehaviour CollisionBehaviour { get { return null; } }

        public override UnitState State
        {
            get { return _elapsedTime < _lifetime ? UnitState.Active : UnitState.Destroyed; }
        }

        public override void OnCollision(Impact impact, IUnit target, CollisionData collisionData)
        {
            if (IsCounterElectron)
            {
                impact.ApplyImpulse(Body);
                if (!_wasHit)
                {
                    _wasHit = true;
                    _timeAfterHit = 0f;
                }
                return;
            }

            Affect(impact, target);
        }

        protected override void OnUpdateView(float elapsedTime)
        {
            if (IsCounterElectron)
            {
                View.Life = 1f;
                var color = _hologramColor;
                color.a = _visibleHologram
                    ? 0.32f + 0.12f * (0.5f + 0.5f * Mathf.Sin(_elapsedTime * 8f))
                    : 0f;
                View.Color = color;

                if (_visibleHologram && _effectFactory != null)
                {
                    _waveCooldown -= elapsedTime;
                    if (_waveCooldown <= 0f)
                    {
                        _waveCooldown = 0.65f;
                        var wave = _effectFactory.CreateEffect("WaveThin");
                        wave.Position = Body.WorldPosition();
                        wave.Size = Mathf.Max(1.5f, Body.WorldScale() * 1.8f);
                        wave.Color = new Color(0.15f, 0.75f, 1f, 0.32f);
                        wave.Run(0.55f, Vector2.zero, 0f);
                    }
                }
                return;
            }

            View.Life = Mathf.Clamp01(1f - _elapsedTime/_lifetime);
        }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            _elapsedTime += elapsedTime;
            if (IsCounterElectron && _wasHit)
            {
                _timeAfterHit += elapsedTime;
                if (_timeAfterHit >= CounterElectronDisappearDelay)
                {
                    Destroy();
                    return;
                }
            }

            if (_elapsedTime >= _lifetime)
            {
                Destroy();
                return;
            }

            Body.ApplyAcceleration(-Body.Velocity*elapsedTime);
            Body.ApplyAngularAcceleration(-Body.AngularVelocity * 0.1f * elapsedTime);
        }

        protected override void OnDispose() {}
        public IControls Controls { get; set; }
        public IStats Stats { get { return null; } }
        public IEngine Engine { get { return null; } }
        public IFeatures Features { get { return _features; } }
        public IShipSystems Systems { get { return null; } }
        public IShipEffects Effects { get { return null; } }
        public IShipSpecification Specification { get { return null; } }

        public int SpawnerId => 0;

        public void Affect(Impact impact, IUnit source)
        {
            if (IsCounterElectron)
            {
                impact.ApplyImpulse(Body);
                if (!_wasHit && (impact.GetTotalDamage(Resistance.Empty) > 0f ||
                                 impact.Effects.Contains(CollisionEffect.Destroy)))
                {
                    _wasHit = true;
                    _timeAfterHit = 0f;
                }
                return;
            }

            impact.ApplyImpulse(Body);
            _hitPoints -= impact.GetTotalDamage(Resistance.Empty);
            if (_hitPoints < 0)
                impact.Effects |= CollisionEffect.Destroy;

            if (impact.Effects.Contains(CollisionEffect.Destroy))
                Destroy();
        }

        public void AddPlatform(IWeaponPlatform platform) {}
        public void AddSystem(ISystem system) {}
        public void AddEffect(IShipEffect shipEffect) {}

        public override void Vanish()
        {
            _elapsedTime = _lifetime;
        }

        private void Destroy()
        {
            _elapsedTime = _lifetime;
            InvokeTriggers(ConditionType.OnDestroy);
        }

		public void Broadcast(string message, Color color) {}

		private float _elapsedTime;
        private float _hitPoints;
        private readonly float _lifetime;
        private readonly IFeatures _features;
        private readonly bool _visibleHologram;
        private readonly EffectFactory _effectFactory;
        private readonly Color _hologramColor;
        private bool _wasHit;
        private float _timeAfterHit;
        private float _waveCooldown;
        private const float CounterElectronDisappearDelay = 5f;
    }
}
