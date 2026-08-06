using Combat.Collision;
using Combat.Collision.Behaviour;
using Combat.Collision.Manager;
using Combat.Component.Body;
using Combat.Component.Collider;
using Combat.Component.DamageHandler;
using Combat.Component.Ship;
using Combat.Component.Triggers;
using Combat.Component.Unit.Classification;
using Combat.Component.View;
using Combat.Component.Bullet;
using Combat.Component.Controller;
using Combat.Component.Ship.Effects.Special;
using Combat.Unit;
using Combat.Unit.Auxiliary;
using UnityEngine;

namespace Combat.Component.Unit
{
    public enum EnergyShieldInteractionMode
    {
        Standard,
        Deflection,
        Angel,
        Subspace,
        Electronic,
    }

    public class EnergyShield : UnitBase, IAuxiliaryUnit
    {
        public EnergyShield(IShip parent, IBody body, IView view, ICollider collider, float defaultOpacity = 0.5f,
            EnergyShieldInteractionMode interactionMode = EnergyShieldInteractionMode.Standard,
            float specialEnergyCost = 0f,
            EnergyShieldVisualController visualController = null)
            : base(new UnitType(UnitClass.Shield, parent.Type.Side, parent), body, view, collider, null)
        {
            _parent = parent;
            _interactionMode = interactionMode;
            _specialEnergyCost = specialEnergyCost;
            _visualController = visualController;
            if (_interactionMode == EnergyShieldInteractionMode.Angel)
            {
                // Friendly projectiles normally do not collide with their
                // owner's ship layer. Put only this shield surface on a
                // neutral collision layer while retaining its real combat
                // side, then refresh the collider's Unity layer.
                Type.CollisionSideOverride = UnitSide.Neutral;
                Collider.Unit = this;
            }
            _defaultColor = _activeColor = view.Color;
            _defaultColor.a *= defaultOpacity;
            Collider.Enabled = _isEnabled = false;
            _visualController?.ApplyCollisionStyle(false);
        }

        public override ICollisionBehaviour CollisionBehaviour { get { return null; } }

        public override UnitState State { get { return _state; } }

        public IDamageHandler DamageHandler { get; set; }

        public override void OnCollision(Impact impact, IUnit target, CollisionData collisionData)
        {
            _timeFromLastHit = 0;
            DamageHandler?.ApplyDamage(impact, target);
        }

        public bool TryHandleSpecialCollision(IUnit target, CollisionData collisionData)
        {
            if (!_isEnabled || _interactionMode == EnergyShieldInteractionMode.Standard || target == null)
                return false;

            switch (_interactionMode)
            {
                case EnergyShieldInteractionMode.Deflection:
                    return HandleDeflection(target, collisionData);
                case EnergyShieldInteractionMode.Angel:
                    return BlockProjectile(target, true);
                case EnergyShieldInteractionMode.Subspace:
                    return BlockProjectile(target, false);
                case EnergyShieldInteractionMode.Electronic:
                    return HandleElectronicCapture(target);
                default:
                    return false;
            }
        }

        public bool BlocksOwnerProjectiles =>
            _isEnabled && _interactionMode == EnergyShieldInteractionMode.Angel;

        public bool Active { get; set; }

        public bool Enabled
        {
            get { return _isEnabled; }
            set
            {
                if (_isEnabled == value)
                    return;

                Collider.Enabled = value;

                _isEnabled = value;
                _visualController?.ApplyCollisionStyle(value);
                InvokeTriggers(_isEnabled ? ConditionType.OnActivate : ConditionType.OnDeactivate);
            }
        }

        public override void Vanish()
        {
            _state = UnitState.Inactive;
        }

        public void Destroy()
        {
            _state = UnitState.Destroyed;
        }

        protected override void OnUpdateView(float elapsedTime)
        {
            var color = Color.Lerp(_disabledColor, _timeFromLastHit < 1.0f ? Color.Lerp(_activeColor, _defaultColor, _timeFromLastHit) : _defaultColor, _power);
            View.Color = color;
        }

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            _timeFromLastHit += elapsedTime;
            _power = Mathf.Lerp(_power, _isEnabled ? 1.0f : 0.0f, 4 * elapsedTime);
        }

        protected override void OnDispose()
        {
            if (DamageHandler != null)
                DamageHandler.Dispose();
        }

        private bool HandleDeflection(IUnit target, CollisionData collisionData)
        {
            if (target is not Combat.Component.Bullet.Bullet laser)
                return false;

            if (CombatRelations.AreAllies(_parent.Type, target.Type))
                return false;

            if (laser.Controller is not BeamController && laser.Controller is not MovingBeamController)
                return false;

            var projectileImpact = new Impact();
            var shieldImpact = new Impact();
            laser.CollisionBehaviour?.Process(laser, this, collisionData, ref projectileImpact, ref shieldImpact);
            var damage = shieldImpact.GetTotalDamageToShield(
                _parent.Specification.Stats.ShieldCorrosiveResistancePercentage);
            var energyCost = Mathf.Max(0f, damage * Mathf.Max(0f, _specialEnergyCost));
            if (!_parent.Stats.Energy.TryGet(energyCost))
            {
                // Let the ordinary energy-shield damage handler resolve this
                // contact. It will absorb as much as the remaining energy can
                // support and collapse the shield if necessary.
                return false;
            }

            var normal = (collisionData.Position - Body.WorldPosition()).normalized;
            if (normal.sqrMagnitude < 0.0001f)
                normal = -target.Body.WorldVelocity().normalized;

            var velocity = target.Body.WorldVelocity();
            var incomingDirection = velocity.sqrMagnitude > 0.001f
                ? velocity.normalized
                : target.Type.Owner != null && target.Type.Owner.IsActive()
                    ? (collisionData.Position - target.Type.Owner.Body.WorldPosition()).normalized
                    : -normal;
            var reflectedDirection = Vector2.Reflect(incomingDirection, normal).normalized;
            var reflectedRotation = RotationHelpers.Angle(reflectedDirection);

            laser.Controller?.Dispose();
            laser.Controller = null;
            if (target.Body.Parent != null)
            {
                target.Body.Move(target.Body.WorldPositionToLocal(collisionData.Position + reflectedDirection * 0.05f));
                target.Body.Turn(target.Body.WorldRotationToLocal(reflectedRotation));
            }
            else
            {
                target.Body.Move(collisionData.Position + reflectedDirection * 0.05f);
                target.Body.Turn(reflectedRotation);
            }

            target.Type.Owner = _parent;
            target.Type.FactionId = _parent.Type.FactionId;
            if (laser.Collider != null)
            {
                laser.Collider.Unit = laser;
                laser.Collider.Source = _parent;
            }

            var reflectedSpeed = Mathf.Max(velocity.magnitude, 180f);
            target.Body.ApplyAcceleration(reflectedDirection * reflectedSpeed - target.Body.Velocity);
            WaterdropHaloEffect.ShowReflection(collisionData.Position, reflectedDirection, Body.WorldScale());
            _timeFromLastHit = 0f;
            return true;
        }

        private bool BlockProjectile(IUnit target, bool includeOwnerProjectiles)
        {
            if (target is not Combat.Component.Bullet.Bullet projectile)
                return true;

            if (CombatRelations.AreAllies(_parent.Type, target.Type))
            {
                // 安乐天使 blocks its own ship's fire, not the unrelated
                // fire of other friendly ships crossing the same area.
                if (!includeOwnerProjectiles || target.Type.Owner != _parent)
                    return true;
            }

            projectile.Vanish();
            _timeFromLastHit = 0f;
            return true;
        }

        private bool HandleElectronicCapture(IUnit target)
        {
            if (target.Type.Class != UnitClass.Missile && target.Type.Class != UnitClass.Drone)
                return true;
            if (CombatRelations.AreAllies(_parent.Type, target.Type))
                return true;
            if (target is IShip ship && ship.Features.ImmuneToEffects)
                return true;
            if (!_parent.Stats.Energy.TryGet(Mathf.Max(0f, _specialEnergyCost)))
            {
                Enabled = false;
                return true;
            }

            target.Type.Owner = _parent;
            target.Type.FactionId = _parent.Type.FactionId;
            if (target.Collider != null)
            {
                target.Collider.Unit = target;
                target.Collider.Source = _parent;
            }
            _timeFromLastHit = 0f;
            return true;
        }

        private float _timeFromLastHit = 100f;
        private float _power;
        private bool _isEnabled;
        private readonly IShip _parent;
        private readonly EnergyShieldVisualController _visualController;
        private readonly EnergyShieldInteractionMode _interactionMode;
        private readonly float _specialEnergyCost;
        private UnitState _state = UnitState.Active;

        private readonly Color _defaultColor;
        private readonly Color _activeColor;
        private static readonly Color _disabledColor = new Color(0, 0, 0, 0);
    }
}
