using Combat.Component.Features;
using Combat.Component.Ship;
using Combat.Component.Triggers;
using Combat.Component.Unit;
using GameDatabase.DataModel;
using UnityEngine;

namespace Combat.Component.Systems.Devices
{
    public sealed class SpecialEnergyShieldDevice : SystemBase, IDevice, IFeaturesModification
    {
        public SpecialEnergyShieldDevice(IShip ship, DeviceStats stats, int keyBinding,
            EnergyShieldInteractionMode interactionMode)
            : base(keyBinding, stats.ControlButtonIcon)
        {
            _ship = ship;
            _interactionMode = interactionMode;
            _energyCost = Mathf.Max(0f, stats.EnergyConsumption);
            _lifetime = Mathf.Max(0f, stats.Lifetime);
            MaxCooldown = Mathf.Max(0f, stats.Cooldown);
            DeviceClass = stats.DeviceClass;
        }

        public GameDatabase.Enums.DeviceClass DeviceClass { get; }
        public override float ActivationCost => _interactionMode switch
        {
            EnergyShieldInteractionMode.Angel => _ship.Stats.Energy.MaxValue * 0.6f,
            EnergyShieldInteractionMode.Deflection => _energyCost,
            _ => 0f,
        };

        public override bool CanBeActivated
        {
            get
            {
                if (!base.CanBeActivated)
                    return false;
                if (_interactionMode == EnergyShieldInteractionMode.Angel)
                    return _isEnabled || _ship.Stats.Energy.Value >= ActivationCost;
                if (_interactionMode == EnergyShieldInteractionMode.Deflection)
                    return _isEnabled || _ship.Stats.Energy.Value >= ActivationCost;
                return true;
            }
        }

        public override IFeaturesModification FeaturesModification =>
            _interactionMode == EnergyShieldInteractionMode.Deflection ||
            _interactionMode == EnergyShieldInteractionMode.Angel ||
            _interactionMode == EnergyShieldInteractionMode.Subspace
                ? this
                : null;

        public bool TryApplyModification(ref FeaturesData data)
        {
            if (_isEnabled && (_interactionMode == EnergyShieldInteractionMode.Deflection ||
                               _interactionMode == EnergyShieldInteractionMode.Angel ||
                               _interactionMode == EnergyShieldInteractionMode.Subspace))
                data.Invulnerable = true;
            return true;
        }

        public void Deactivate()
        {
            if (!_isEnabled)
                return;

            _isEnabled = false;
            if (_interactionMode == EnergyShieldInteractionMode.Angel)
                TimeFromLastUse = 0f;
            InvokeTriggers(ConditionType.OnDeactivate);
        }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            switch (_interactionMode)
            {
                case EnergyShieldInteractionMode.Deflection:
                    UpdateDeflection();
                    break;
                case EnergyShieldInteractionMode.Angel:
                    UpdateAngel(elapsedTime);
                    break;
                case EnergyShieldInteractionMode.Electronic:
                    UpdateElectronic(elapsedTime);
                    break;
                case EnergyShieldInteractionMode.Subspace:
                    UpdateToggle();
                    UpdateSubspaceDrain(elapsedTime);
                    break;
                default:
                    UpdateToggle();
                    break;
            }

            _wasActive = Active;
        }

        protected override void OnUpdateView(float elapsedTime) { }
        protected override void OnDispose() { }

        private void UpdateAngel(float elapsedTime)
        {
            if (!_isEnabled && Active && !_wasActive && CanBeActivated &&
                _ship.Stats.Energy.TryGet(ActivationCost))
            {
                _isEnabled = true;
                _timeRemaining = _lifetime > 0f ? _lifetime : 20f;
                InvokeTriggers(ConditionType.OnActivate);
            }

            if (!_isEnabled)
                return;

            _timeRemaining -= Mathf.Max(0f, elapsedTime);
            if (_timeRemaining <= 0f)
                Deactivate();
        }

        private void UpdateDeflection()
        {
            if (Active && !_isEnabled && CanBeActivated && _ship.Stats.Energy.TryGet(ActivationCost))
            {
                Enable();
                return;
            }

            if (!Active || !CanBeActivated)
                Deactivate();
        }

        private void UpdateElectronic(float elapsedTime)
        {
            var requiredEnergy = _energyCost * Mathf.Max(0f, elapsedTime);
            if (Active && CanBeActivated && _ship.Stats.Energy.TryGet(requiredEnergy))
                Enable();
            else
                Deactivate();
        }

        private void UpdateToggle()
        {
            if (Active && CanBeActivated)
                Enable();
            else
                Deactivate();
        }

        private void Enable()
        {
            if (_isEnabled)
                return;
            _isEnabled = true;
            InvokeTriggers(ConditionType.OnActivate);
        }

        private void UpdateSubspaceDrain(float elapsedTime)
        {
            if (!_isEnabled)
                return;

            _subspaceSecondTimer += Mathf.Max(0f, elapsedTime);
            while (_subspaceSecondTimer >= 1f && _ship.Stats.IsAlive)
            {
                _subspaceSecondTimer -= 1f;
                _subspaceElapsedSeconds++;
                var damage = _ship.Stats.Armor.MaxValue * _subspaceElapsedSeconds * 0.01f;
                _ship.Stats.Armor.Get(damage);
            }
        }

        private readonly IShip _ship;
        private readonly EnergyShieldInteractionMode _interactionMode;
        private readonly float _energyCost;
        private readonly float _lifetime;
        private bool _isEnabled;
        private bool _wasActive;
        private float _timeRemaining;
        private float _subspaceSecondTimer;
        private int _subspaceElapsedSeconds;
    }
}
