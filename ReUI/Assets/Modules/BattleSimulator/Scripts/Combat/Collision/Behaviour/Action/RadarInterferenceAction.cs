using Combat.Collision.Manager;
using Combat.Component.Ship;
using Combat.Component.Ship.Effects;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using GameDatabase.Enums;

namespace Combat.Collision.Behaviour.Action
{
    public sealed class RadarInterferenceAction : ICollisionAction
    {
        public RadarInterferenceAction(float duration, float initialEnergyDrainFraction, float energyDrainPerSecond,
            BulletImpactType impactType)
        {
            _duration = duration;
            _initialEnergyDrainFraction = initialEnergyDrainFraction;
            _energyDrainPerSecond = energyDrainPerSecond;
            _impactType = impactType;
        }

        public void Invoke(IUnit self, IUnit target, CollisionData collisionData, ref Impact selfImpact, ref Impact targetImpact)
        {
            if (!collisionData.IsNew || !_isAlive || target is not IShip ship)
                return;

            // EMP is an offensive disruption of the struck ship's own radar
            // and weapon-control systems. It must never feed back into the
            // firing ship (or its allies) just because an attached/raycast
            // beam reaches us through a special collision path.
            var owner = self?.Type?.Owner;
            if (owner != null && (ship == owner || CombatRelations.AreAllies(owner.Type, ship.Type)))
                return;

            if (RadarStatus.TryApplyEmpJammed(ship, _duration, _energyDrainPerSecond, owner))
                targetImpact.EnergyDrain += ship.Stats.Energy.MaxValue * _initialEnergyDrainFraction;
            _isAlive = _impactType == BulletImpactType.HitAllTargets;
        }

        public void Dispose() { }

        private bool _isAlive = true;
        private readonly float _duration;
        private readonly float _initialEnergyDrainFraction;
        private readonly float _energyDrainPerSecond;
        private readonly BulletImpactType _impactType;
    }
}
