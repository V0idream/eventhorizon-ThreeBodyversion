using Combat.Component.Ship;
using Combat.Component.Unit;
using Combat.Factory;
using Combat.Unit;
using GameDatabase.DataModel;

namespace Combat.Component.Systems.Devices
{
    /// <summary>
    /// Passive Edge World counter-electron package.  Each installed component
    /// creates one persistent false radar target when combat starts.
    /// </summary>
    public sealed class CounterElectronDevice : SystemBase, IDevice
    {
        public CounterElectronDevice(IShip ship, DeviceStats stats, SpaceObjectFactory factory)
            : base(-1, stats.ControlButtonIcon)
        {
            DeviceClass = stats.DeviceClass;
            _ship = ship;
            _factory = factory;
        }

        public GameDatabase.Enums.DeviceClass DeviceClass { get; }
        public override float ActivationCost => 0f;
        public override bool CanBeActivated => false;

        public void Deactivate() { }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            if (_spawned || !_ship.IsActive())
                return;

            _spawned = true;
            _decoy = _factory.CreateCounterElectronDecoy(_ship);
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
        private IUnit _decoy;
        private bool _spawned;
    }
}
