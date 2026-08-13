using Combat.Component.Ship;
using Combat.Component.Body;
using Combat.Component.Ship.Effects;
using Combat.Component.Triggers;
using Combat.Scene;
using Combat.Unit;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using UnityEngine;

namespace Combat.Component.Systems.Devices
{
    public sealed class SmallUniverseEntranceDevice : SystemBase, IDevice
    {
        private const float LongPressThreshold = 0.75f;
        private const float MinimumEnergy = 40000f;

        public SmallUniverseEntranceDevice(IShip ship, DeviceStats stats, int keyBinding, IScene scene)
            : base(keyBinding, stats.ControlButtonIcon)
        {
            _ship = ship;
            _scene = scene;
            DeviceClass = stats.DeviceClass;
            MaxCooldown = stats.Cooldown > 0f ? stats.Cooldown : 120f;
            _range = stats.Range > 0f ? stats.Range : 120f;
        }

        public DeviceClass DeviceClass { get; }
        public override float ActivationCost => MinimumEnergy;
        public override bool CanBeActivated => base.CanBeActivated && _ship != null &&
                                               _ship.Stats.Energy.Value >= MinimumEnergy;
        public void Deactivate() { }

        public void BeginPress()
        {
            _uiPressActive = true;
            _uiPressStart = Time.unscaledTime;
        }

        public void EndPress()
        {
            if (!_uiPressActive) return;
            _uiPressActive = false;
            _queuedLongPress = Time.unscaledTime - _uiPressStart >= LongPressThreshold;
            _activationQueued = true;
        }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            if (_activationQueued)
            {
                _activationQueued = false;
                Activate(_queuedLongPress);
            }

            if (_uiPressActive)
                return;

            if (Active && !_wasPressed && CanBeActivated)
                _pressTime = 0f;

            if (Active && CanBeActivated)
                _pressTime += Mathf.Max(0f, elapsedTime);

            if (!Active && _wasPressed && CanBeActivated)
                Activate(_pressTime >= LongPressThreshold);

            _wasPressed = Active;
        }

        private void Activate(bool longPress)
        {
            if (!CanBeActivated || _ship.Stats.Energy.Value < MinimumEnergy)
                return;

            // Opening a universe consumes every unit of energy currently stored.
            _ship.Stats.Energy.Get(_ship.Stats.Energy.Value);
            TimeFromLastUse = 0f;
            InvokeTriggers(ConditionType.OnActivate);

            if (longPress)
                EnterSelf();
            else
                ExileNearbyShips();

            InvokeTriggers(ConditionType.OnDeactivate);
        }

        private void ExileNearbyShips()
        {
            var center = _ship.Body.WorldPosition();
            lock (_scene.Ships.LockObject)
            {
                foreach (var target in _scene.Ships.Items)
                {
                    if (target == null || target == _ship || !target.IsActive() ||
                        Vector2.Distance(center, target.Body.WorldPosition()) > _range ||
                        SmallUniverseTransitEffect.IsInTransit(target))
                        continue;

                    target.AddEffect(new SmallUniverseTransitEffect(target, _scene,
                        GetTransitDuration(target), true, false, false));
                }
            }
        }

        private void EnterSelf()
        {
            var duration = _ship.Specification.Info.SizeClass == SizeClass.TitanP ? 25f : 40f;

            _ship.Stats.Armor.Get(-_ship.Stats.Armor.RechargeRate * duration);
            _ship.Stats.Energy.Get(-_ship.Stats.Energy.RechargeRate * duration);
            _ship.Stats.Shield.Get(-_ship.Stats.Shield.RechargeRate * duration);

            foreach (var system in _ship.Systems.All)
                if (system is SystemBase systemBase && systemBase != this)
                    systemBase.ReduceCooldown(duration);

            // A long press advances the ship's own clocks by the equivalent
            // duration, but the player must emerge immediately.  Only ships
            // exiled by the short press remain outside normal space.
            var origin = _ship.Body.WorldPosition();
            ThreeBodySpatialVisuals.PlayUniverseTransit(origin, _ship.Features.Color, false);
            var destination = _scene.FindFreePlace(80f, _ship.Type.Side);
            MoveWorld(_ship.Body, destination);
            Stop(_ship.Body);
            ThreeBodySpatialVisuals.PlayUniverseTransit(destination, _ship.Features.Color, true);
            _ship.AddEffect(new TemporaryInvulnerabilityEffect(5f));
        }

        private static void MoveWorld(Combat.Component.Body.IBody body, Vector2 worldPosition) =>
            body.Move(body.Parent == null ? worldPosition : body.WorldPositionToLocal(worldPosition));

        private static void Stop(Combat.Component.Body.IBody body)
        {
            if (body is Combat.Component.Body.RigidBodyAdapter rigidBody)
            {
                rigidBody.Velocity = Vector2.zero;
                rigidBody.AngularVelocity = 0f;
                return;
            }
            body.ApplyAcceleration(-body.Velocity);
            body.ApplyAngularAcceleration(-body.AngularVelocity);
        }

        private static float GetTransitDuration(IShip ship)
        {
            var info = ship.Specification.Info;
            if (info.ShipType == ShipType.Starbase || info.SizeClass == SizeClass.Starbase ||
                info.SizeClass == SizeClass.TitanP)
                return 25f;
            if (info.ShipType == ShipType.Flagship)
                return 40f;
            return 60f;
        }

        protected override void OnUpdateView(float elapsedTime) { }
        protected override void OnDispose() { }

        private readonly IShip _ship;
        private readonly IScene _scene;
        private readonly float _range;
        private bool _wasPressed;
        private float _pressTime;
        private float _uiPressStart;
        private bool _uiPressActive;
        private bool _activationQueued;
        private bool _queuedLongPress;
    }
}
