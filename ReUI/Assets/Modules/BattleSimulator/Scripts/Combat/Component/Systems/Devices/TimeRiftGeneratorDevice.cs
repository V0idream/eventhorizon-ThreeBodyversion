using Combat.Component.Ship;
using Combat.Component.Triggers;
using Combat.Scene;
using GameDatabase.DataModel;
using GameDatabase.Enums;

namespace Combat.Component.Systems.Devices
{
    public sealed class TimeRiftGeneratorDevice : SystemBase, IDevice
    {
        public TimeRiftGeneratorDevice(IShip ship, DeviceStats stats, int keyBinding, IScene scene)
            : base(keyBinding, stats.ControlButtonIcon)
        {
            _ship = ship;
            _scene = scene;
            DeviceClass = stats.DeviceClass;
            MaxCooldown = stats.Cooldown;
            _energyCost = stats.EnergyConsumption;
            _lifetime = stats.Lifetime > 0f ? stats.Lifetime : 20f;
            _riftCount = stats.Size > 0f ? UnityEngine.Mathf.RoundToInt(stats.Size) : 7;
        }

        public DeviceClass DeviceClass { get; }
        public override float ActivationCost => _energyCost;
        public override bool CanBeActivated => base.CanBeActivated && _ship != null &&
                                               _ship.Stats.Energy.Value >= _energyCost;
        public void Deactivate() { }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            if (Active && !_wasPressed && CanBeActivated && _ship.Stats.Energy.TryGet(_energyCost))
            {
                TimeRiftField.Spawn(_scene, _ship, _riftCount, _lifetime);
                TimeFromLastUse = 0f;
                InvokeTriggers(ConditionType.OnActivate);
            }
            _wasPressed = Active;
        }

        protected override void OnUpdateView(float elapsedTime) { }
        protected override void OnDispose() { }

        private readonly IShip _ship;
        private readonly IScene _scene;
        private readonly float _energyCost;
        private readonly float _lifetime;
        private readonly int _riftCount;
        private bool _wasPressed;
    }
}
