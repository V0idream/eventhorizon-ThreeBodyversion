using Combat.Collision.Manager;
using Combat.Component.Bullet;
using Combat.Component.Ship;
using Combat.Component.Unit;
using Combat.Unit;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using UnityEngine;

namespace Combat.Component.Systems.Devices
{
    /// <summary>
    /// Mirror Sea uses a separate 0..100 reserve in addition to ship energy.
    /// Active time consumes five reserve points per second; inactive time
    /// restores one.  The regular device cooldown UI is driven from that reserve.
    /// </summary>
    public sealed class MirrorSeaFieldDevice : SystemBase, IDevice
    {
        public MirrorSeaFieldDevice(IShip ship, DeviceStats stats, int keyBinding, EnergyShield shield)
            : base(keyBinding, stats.ControlButtonIcon)
        {
            _ship = ship;
            _shield = shield;
            _energyPerSecond = Mathf.Max(0f, stats.EnergyConsumption);
            _blue = stats.Color;
            _red = new Color(1f, 0.12f, 0.08f, _blue.a > 0f ? _blue.a : 1f);
            DeviceClass = stats.DeviceClass;
            _shield.Enabled = false;
        }

        public DeviceClass DeviceClass { get; }
        public bool IsFieldEnabled => _shield != null && _shield.IsActive() && _shield.Enabled;
        public override float Cooldown => Mathf.Clamp01(1f - _reserve / MaxReserve);
        public override bool CanBeActivated =>
            base.CanBeActivated && _reserve > 0.001f &&
            (_shield.Enabled || _ship.Stats.Energy.Value > 0.001f);

        public void Deactivate()
        {
            _shield.Enabled = false;
        }

        /// <summary>
        /// Swept/high-speed projectiles can cross the target between discrete
        /// trigger callbacks. This fallback routes them through the exact same
        /// Mirror Sea reflection path instead of allowing a piercing weapon to
        /// bypass the field merely because physics tunnelling occurred.
        /// </summary>
        public bool TryInterceptProjectile(IBullet projectile, Vector2 contactPoint, float elapsedTime)
        {
            if (!IsFieldEnabled || projectile == null || !projectile.IsActive())
                return false;

            var collision = CollisionData.FromObjects(projectile, _shield, contactPoint, true,
                Mathf.Max(Time.fixedDeltaTime, elapsedTime));
            return _shield.TryHandleSpecialCollision(projectile, collision);
        }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            var step = Mathf.Max(0f, elapsedTime);
            if (Active && _reserve > 0f)
            {
                var energy = _energyPerSecond * step;
                if (_ship.Stats.Energy.TryGet(energy))
                {
                    _shield.Enabled = true;
                    _reserve = Mathf.Max(0f, _reserve - ActiveDrainPerSecond * step);
                    if (_reserve <= 0f)
                        _shield.Enabled = false;
                    return;
                }
            }

            _shield.Enabled = false;
            _reserve = Mathf.Min(MaxReserve, _reserve + RecoveryPerSecond * step);
        }

        protected override void OnUpdateView(float elapsedTime)
        {
            if (_shield?.View == null)
                return;
            var ratio = Mathf.Clamp01(_reserve / MaxReserve);
            var color = Color.Lerp(_red, _blue, ratio);
            color.a = Mathf.Max(0.22f, color.a);
            _shield.View.Color = color;
        }

        protected override void OnDispose()
        {
            if (_shield != null && _shield.IsActive())
                _shield.Destroy();
        }

        private const float MaxReserve = 100f;
        private const float ActiveDrainPerSecond = 5f;
        private const float RecoveryPerSecond = 1f;
        private readonly IShip _ship;
        private readonly EnergyShield _shield;
        private readonly float _energyPerSecond;
        private readonly Color _blue;
        private readonly Color _red;
        private float _reserve = MaxReserve;
    }

    public sealed class UniverseRestartDevice : SystemBase, IDevice
    {
        public UniverseRestartDevice(IShip ship, DeviceStats stats, int keyBinding)
            : base(keyBinding, stats.ControlButtonIcon)
        {
            _ship = ship;
            DeviceClass = stats.DeviceClass;
        }

        public DeviceClass DeviceClass { get; }
        public override bool CanBeActivated => base.CanBeActivated && !_used;
        public override float Cooldown => _used ? 1f : 0f;

        public void Deactivate() { }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            if (Active && !_wasActive && !_used && CanBeActivated)
            {
                _used = true;
                UniverseRestartRuntime.Request(_ship);
            }
            _wasActive = Active;
        }

        protected override void OnUpdateView(float elapsedTime) { }
        protected override void OnDispose() { }

        private readonly IShip _ship;
        private bool _used;
        private bool _wasActive;
    }

    /// <summary>
    /// Decouples the device assembly from CombatManager.  CombatManager consumes
    /// the request on the next tick, where fleet-model removal/restoration and
    /// the ship-selection UI are available without a Zenject dependency cycle.
    /// </summary>
    public static class UniverseRestartRuntime
    {
        public static void Request(IShip source)
        {
            if (source != null && _pending == null)
                _pending = source;
        }

        public static bool TryConsume(out IShip source)
        {
            source = _pending;
            _pending = null;
            return source != null;
        }

        private static IShip _pending;
    }
}
