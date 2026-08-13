using System.Collections.Generic;
using Combat.Component.Ship;
using Combat.Component.Ship.Effects;
using Combat.Component.Triggers;
using Combat.Component.Unit.Classification;
using Combat.Factory;
using Combat.Scene;
using Combat.Unit;
using Constructor;
using GameDatabase.DataModel;
using GameDatabase.Enums;

namespace Combat.Component.Systems.Devices
{
    public sealed class FirewallCollapseDevice : SystemBase, IDevice
    {
        public FirewallCollapseDevice(IShip ship, DeviceStats stats, int keyBinding, IScene scene)
            : base(keyBinding, stats.ControlButtonIcon)
        {
            _ship = ship; _scene = scene; _energy = stats.EnergyConsumption;
            MaxCooldown = stats.Cooldown > 0f ? stats.Cooldown : 75f;
            DeviceClass = stats.DeviceClass;
        }
        public DeviceClass DeviceClass { get; }
        public override float ActivationCost => _energy;
        public override bool CanBeActivated => base.CanBeActivated && _ship.Stats.Energy.Value >= _energy;
        public void Deactivate() { }
        protected override void OnUpdatePhysics(float elapsedTime)
        {
            if (Active && !_pressed && CanBeActivated && _ship.Stats.Energy.TryGet(_energy))
            {
                var group = FirewallCollapseEffect.NewGroup();
                lock (_scene.Ships.LockObject)
                    foreach (var target in _scene.Ships.Items)
                        if (target != null && target.IsActive() && CombatRelations.AreEnemies(_ship.Type, target.Type))
                            target.AddEffect(new FirewallCollapseEffect(target, _ship.Type, 30f, group));
                TimeFromLastUse = 0f;
                InvokeTriggers(ConditionType.OnActivate);
            }
            _pressed = Active;
        }
        protected override void OnUpdateView(float elapsedTime) { }
        protected override void OnDispose() { }
        private readonly IShip _ship;
        private readonly IScene _scene;
        private readonly float _energy;
        private bool _pressed;
    }

    public sealed class EdgeDroneHiveDevice : SystemBase, IDevice
    {
        public EdgeDroneHiveDevice(IShip ship, DeviceStats stats) : base(-1, stats.ControlButtonIcon)
        { _ship = ship; _interval = stats.Cooldown > 0f ? stats.Cooldown : 1.5f; DeviceClass = stats.DeviceClass; }
        public DeviceClass DeviceClass { get; }
        public void Deactivate() { }
        protected override void OnUpdatePhysics(float elapsedTime)
        {
            _drones.RemoveAll(x => x == null || !x.IsActive());
            _timer -= elapsedTime;
            if (_timer > 0f || _drones.Count >= 24 || !_ship.IsActive()) return;
            _timer = _interval;
            var drone = EdgeDroneRuntime.SpawnImmediate(EdgeDroneRuntime.NormalBuildId, _ship,
                _ship.Body.WorldPosition(), DroneBehaviour.Aggressive);
            if (drone != null) _drones.Add(drone);
        }
        protected override void OnUpdateView(float elapsedTime) { }
        protected override void OnDispose() { }
        private readonly IShip _ship;
        private readonly float _interval;
        private readonly List<IShip> _drones = new();
        private float _timer;
    }

    public sealed class EdgeDefenderDevice : SystemBase, IDevice
    {
        public EdgeDefenderDevice(IShip ship, DeviceStats stats) : base(-1, stats.ControlButtonIcon)
        { _ship = ship; DeviceClass = stats.DeviceClass; }
        public DeviceClass DeviceClass { get; }
        public void Deactivate() { }
        protected override void OnUpdatePhysics(float elapsedTime)
        {
            if (_drone != null && _drone.IsActive()) return;
            _timer -= elapsedTime;
            if (_timer > 0f || !_ship.IsActive()) return;
            _drone = EdgeDroneRuntime.SpawnImmediate(EdgeDroneRuntime.DefenseBuildId, _ship,
                _ship.Body.WorldPosition(), DroneBehaviour.Defensive);
            _timer = 5f;
        }
        protected override void OnUpdateView(float elapsedTime) { }
        protected override void OnDispose() { }
        private readonly IShip _ship;
        private IShip _drone;
        private float _timer;
    }
}
