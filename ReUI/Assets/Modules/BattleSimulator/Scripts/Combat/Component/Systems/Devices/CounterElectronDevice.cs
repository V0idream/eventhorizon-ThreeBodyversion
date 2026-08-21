using Combat.Component.Ship;
using Combat.Component.Unit;
using Combat.Component.Triggers;
using Combat.Factory;
using Combat.Unit;
using GameDatabase.DataModel;

namespace Combat.Component.Systems.Devices
{
    /// <summary>
    /// Active Edge World counter-electron package. Each activation creates one
    /// false target; AI-controlled enemy copies activate automatically.
    /// </summary>
    public sealed class CounterElectronDevice : SystemBase, IDevice
    {
        public CounterElectronDevice(IShip ship, DeviceStats stats, int keyBinding,
            SpaceObjectFactory factory, bool autoActivate)
            : base(keyBinding, stats.ControlButtonIcon)
        {
            DeviceClass = stats.DeviceClass;
            _ship = ship;
            _factory = factory;
            _autoActivate = autoActivate;
        }

        public GameDatabase.Enums.DeviceClass DeviceClass { get; }
        public override float ActivationCost => 0f;
        public override bool CanBeActivated => base.CanBeActivated && _ship.IsActive() &&
                                               (_decoy == null || !_decoy.IsActive());

        public void Deactivate() { }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            if (!_ship.IsActive()) return;

            var requested = _autoActivate || (Active && !_pressed);
            if (requested && CanBeActivated)
            {
                _decoy = _factory.CreateCounterElectronDecoy(_ship);
                TimeFromLastUse = 0f;
                InvokeTriggers(ConditionType.OnActivate);
            }

            _pressed = Active;
        }

        protected override void OnUpdateView(float elapsedTime) { }
        protected override void OnDispose()
        {
            if (_decoy != null && _decoy.IsActive())
                _decoy.Vanish();
            _decoy = null;
        }

        private readonly IShip _ship;
        private readonly SpaceObjectFactory _factory;
        private readonly bool _autoActivate;
        private IUnit _decoy;
        private bool _pressed;
    }
}
