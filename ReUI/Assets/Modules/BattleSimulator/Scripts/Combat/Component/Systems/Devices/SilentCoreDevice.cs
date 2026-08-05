using Combat.Collision;
using Combat.Component.Features;
using Combat.Component.Ship;
using Combat.Component.Ship.Effects;
using Combat.Component.Triggers;
using Combat.Component.Unit;
using Combat.Effects;
using Combat.Factory;
using Combat.Scene;
using Combat.Unit;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using UnityEngine;

namespace Combat.Component.Systems.Devices
{
    /// <summary>
    /// One-shot battlefield denial device used by Wan Nian Feng Xue. Activating
    /// it immediately jams every active ship, then progressively covers the
    /// carrier in electrical discharges before a Stellar Hydrogen Bomb-scale
    /// self-destruction. The explosion keeps the carrier as its owner so the
    /// standard collision manager rejects allied damage; the carrier itself is
    /// destroyed explicitly after the blast is created.
    /// </summary>
    public sealed class SilentCoreDevice : SystemBase, IDevice, IFeaturesModification, ISystemsModification
    {
        public SilentCoreDevice(
            IShip ship,
            DeviceStats stats,
            int keyBinding,
            IScene scene,
            SpaceObjectFactory spaceObjectFactory,
            EffectFactory effectFactory)
            : base(keyBinding, stats.ControlButtonIcon)
        {
            _ship = ship;
            _scene = scene;
            _spaceObjectFactory = spaceObjectFactory;
            _effectFactory = effectFactory;
            _chargeDuration = Mathf.Max(0.1f, stats.Lifetime);
            _range = Mathf.Max(0.1f, stats.Range);
            _damage = Mathf.Max(0f, stats.Power);
            _energyCost = Mathf.Max(0f, stats.EnergyConsumption);
            _color = stats.Color;
            MaxCooldown = Mathf.Max(0f, stats.Cooldown);
            DeviceClass = stats.DeviceClass;
        }

        public GameDatabase.Enums.DeviceClass DeviceClass { get; }
        public float ActivationTime => _chargeDuration;
        public override float ActivationCost => _energyCost;
        public override bool CanBeActivated => !_spent && base.CanBeActivated &&
                                               _ship.Stats.Energy.Value >= _energyCost;

        public override IFeaturesModification FeaturesModification => this;
        public bool TryApplyModification(ref FeaturesData data)
        {
            if (!_charging)
                return true;

            var progress = Mathf.Clamp01(_elapsed / _chargeDuration);
            var pulse = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_elapsed * Mathf.Lerp(18f, 52f, progress)));
            var intensity = Mathf.Clamp01(0.2f + progress * 0.8f) * pulse;
            var chargeColor = Color.Lerp(new Color(0.02f, 0.55f, 1f, 1f), Color.white, progress * 0.75f);
            data.Color = Color.Lerp(data.Color, chargeColor, Mathf.Clamp01(0.35f + intensity * 0.65f));
            return true;
        }

        public override ISystemsModification SystemsModification => this;
        public bool IsAlive => true;
        public bool CanActivateSystem(ISystem system) => !_charging || ReferenceEquals(system, this);
        public void OnSystemActivated(ISystem system) { }
        public void Deactivate() { }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            if (_charging)
            {
                _elapsed += elapsedTime;
                if (_elapsed >= _chargeDuration)
                    Detonate();
                return;
            }

            if (!Active || !CanBeActivated || !_ship.Stats.Energy.TryGet(_energyCost))
                return;

            _spent = true;
            _charging = true;
            _elapsed = 0f;
            _effectTimer = 0f;
			_pulseTimer = 0f;
            ApplyBattlewideEmp();
			SpawnActivationBurst();
            InvokeTriggers(ConditionType.OnActivate);
        }

        protected override void OnUpdateView(float elapsedTime)
        {
            if (!_charging)
                return;

            _effectTimer -= elapsedTime;
			_pulseTimer -= elapsedTime;
            var progress = Mathf.Clamp01(_elapsed / _chargeDuration);
            var interval = Mathf.Lerp(0.14f, 0.025f, progress);
            while (_effectTimer <= 0f)
            {
                _effectTimer += interval;
                var dischargeCount = 3 + Mathf.FloorToInt(progress * 8f);
                for (var i = 0; i < dischargeCount; ++i)
					SpawnSurfaceDischarge(progress,
						progress > 0.35f && (i == dischargeCount - 1 || (i % 4 == 0 && progress > 0.7f)));
            }

			if (_pulseTimer <= 0f)
			{
				_pulseTimer += Mathf.Lerp(0.18f, 0.055f, progress);
				SpawnCorePulse(progress);
            }
        }

        protected override void OnDispose() { }

        private void ApplyBattlewideEmp()
        {
            if (_scene == null)
                return;

            lock (_scene.Ships.LockObject)
            {
                foreach (var ship in _scene.Ships.Items)
                {
                    if (!ship.IsActive())
                        continue;

                    ship.Stats.Energy.Get(ship.Stats.Energy.MaxValue * InitialEnergyDrainFraction);
                    RadarStatus.ApplyJammed(ship, EmpDuration, EnergyDrainPerSecond);
                }
            }
        }

        private void SpawnSurfaceDischarge(float progress, bool useStrike)
        {
            if (_effectFactory == null || !_ship.IsActive())
                return;

            // Reuse the original lightning-cannon visuals. Animated Lightning
            // provides crawling arcs, while LightningStrike adds short, bright
            // discharges as the core approaches detonation.
            var effectName = useStrike ? "LightningStrike" : "Lightning";
            IEffect effect = _effectFactory.CreateEffect(effectName, _ship.Body);
            if (effect == null)
                return;

            var direction = UnityEngine.Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector2.right;
            effect.Position = direction * UnityEngine.Random.Range(0.05f, Mathf.Lerp(0.42f, 0.82f, progress));
            effect.Size = Mathf.Lerp(0.65f, useStrike ? 2.4f : 1.75f, progress) *
                          Mathf.Sqrt(Mathf.Max(1f, _ship.Body.Scale));
            effect.Rotation = UnityEngine.Random.Range(0f, 360f);
            effect.Color = Color.Lerp(new Color(0.02f, 0.72f, 1f, 1f), Color.white, progress * 0.9f);
            effect.Run(Mathf.Lerp(0.16f, useStrike ? 0.48f : 0.34f, progress), Vector2.zero,
                UnityEngine.Random.Range(-220f, 220f));
        }

		private void SpawnActivationBurst()
		{
			if (_effectFactory == null || !_ship.IsActive())
				return;

			var scale = Mathf.Sqrt(Mathf.Max(1f, _ship.Body.Scale));
			foreach (var effectName in new[] { "FlashAdditive", "EnergyField", "WaveStrong" })
			{
				var effect = _effectFactory.CreateEffect(effectName, _ship.Body);
				if (effect == null)
					continue;
				effect.Position = Vector2.zero;
				effect.Size = (effectName == "FlashAdditive" ? 5f : 2.5f) * scale;
				effect.Color = new Color(0.08f, 0.75f, 1f, 1f);
				effect.Run(effectName == "WaveStrong" ? 0.45f : 0.8f, Vector2.zero, 0f);
			}

			_effectFactory.CreateDisturbance(_ship.Body.WorldPosition(), 10f * scale);
		}

		private void SpawnCorePulse(float progress)
		{
			if (_effectFactory == null || !_ship.IsActive())
				return;

			var scale = Mathf.Sqrt(Mathf.Max(1f, _ship.Body.Scale));
			var field = _effectFactory.CreateEffect("EnergyField", _ship.Body);
			if (field != null)
			{
				field.Position = Vector2.zero;
				field.Size = Mathf.Lerp(1.8f, 4.8f, progress) * scale;
				field.Color = Color.Lerp(new Color(0.02f, 0.62f, 1f, 0.95f), Color.white, progress * 0.75f);
				field.Run(Mathf.Lerp(0.22f, 0.48f, progress), Vector2.zero, 0f);
			}

			var flash = _effectFactory.CreateEffect("FlashAdditive", _ship.Body);
			if (flash != null)
			{
				flash.Position = Vector2.zero;
				flash.Size = Mathf.Lerp(2.2f, 6.5f, progress) * scale;
				flash.Color = Color.Lerp(new Color(0.0f, 0.48f, 1f, 0.82f), Color.white, progress);
				flash.Run(Mathf.Lerp(0.10f, 0.24f, progress), Vector2.zero, 0f);
			}
		}

        private void Detonate()
        {
            _charging = false;
            _spaceObjectFactory.CreateStrongExplosion(
                _ship.Body.WorldPosition(),
                _range,
                DamageType.Heat,
                _damage,
                _ship,
                _color,
                1f,
                Mathf.Sqrt(Mathf.Max(1f, _ship.Body.Weight)));
            TimeFromLastUse = 0f;
            _ship.Affect(new Impact { Effects = CollisionEffect.Destroy }, null);
        }

        private const float EmpDuration = 60f;
        private const float InitialEnergyDrainFraction = 0.20f;
        private const float EnergyDrainPerSecond = 1f;

        private readonly IShip _ship;
        private readonly IScene _scene;
        private readonly SpaceObjectFactory _spaceObjectFactory;
        private readonly EffectFactory _effectFactory;
        private readonly float _chargeDuration;
        private readonly float _range;
        private readonly float _damage;
        private readonly float _energyCost;
        private readonly Color _color;
        private bool _spent;
        private bool _charging;
        private float _elapsed;
        private float _effectTimer;
		private float _pulseTimer;
    }
}
