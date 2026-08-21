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
using Combat.Scene;
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
        Stasis,
        MirrorSea,
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
                case EnergyShieldInteractionMode.Stasis:
                    return HandleStasis(target);
                case EnergyShieldInteractionMode.MirrorSea:
                    return HandleMirrorSea(target, collisionData);
                default:
                    return false;
            }
        }

        public bool BlocksOwnerProjectiles =>
            _isEnabled && _interactionMode == EnergyShieldInteractionMode.Angel;

        // Mirror Sea has absolute interception priority. A projectile/beam's
        // own pass-through, piercing or ignore-shield flags do not override it.
        public bool BlocksPiercingProjectiles =>
            _isEnabled && _interactionMode == EnergyShieldInteractionMode.MirrorSea;

        public bool IgnoresNonDroneShipCollisions =>
            _interactionMode == EnergyShieldInteractionMode.Electronic;

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

        private bool HandleStasis(IUnit target)
        {
            if (CombatRelations.AreAllies(_parent.Type, target.Type))
                return true;

            if (target is IBullet projectile)
            {
                Combat.Collision.Behaviour.Action.ProjectileStasisStatus.Apply(projectile, 1.25f);
                _timeFromLastHit = 0f;
                return true;
            }

            if (target is IShip ship && target.Type.Class == UnitClass.Drone)
            {
                if (!ship.Features.ImmuneToEffects)
                    Combat.Component.Ship.Effects.StasisEffect.Apply(ship, 1.25f);
                _timeFromLastHit = 0f;
            }

            return true;
        }

        private bool HandleMirrorSea(IUnit target, CollisionData collisionData)
        {
            if (target == null || CombatRelations.AreAllies(_parent.Type, target.Type))
                return true;

            if (target is not Combat.Component.Bullet.IBullet projectile)
                return true;

            var attacker = target.GetOwnerShip();
            if (attacker == null || !attacker.IsActive() || projectile.CollisionBehaviour == null)
            {
                projectile.Vanish();
                return true;
            }

            if (target.Type.Class == UnitClass.Missile)
                return ReflectMissile(projectile, attacker, collisionData);

            // Reattribute the reflected hit before resolving it so statistics,
            // kill credit, effects and AI ownership all belong to the shield's
            // ship rather than to the original attacker.
            target.Type.Owner = _parent;
            target.Type.FactionId = _parent.Type.FactionId;
            if (target.Collider != null)
            {
                target.Collider.Unit = target;
                target.Collider.Source = _parent;
            }

            var angle = Mathf.Repeat((target.GetHashCode() & 0x7fffffff) * 137.507764f, 360f);
            var radial = RotationHelpers.Direction(angle);
            var radius = Mathf.Max(5f, attacker.Body.WorldScale() * 0.8f + target.Body.WorldScale());
            var reflectionPoint = attacker.Body.WorldPosition() + radial * radius;
            MirrorSeaReflectionVisual.Show(attacker.Body.WorldPosition(), radius, _parent.View.Color);

            var reflectedData = CollisionData.FromObjects(target, attacker, reflectionPoint, true,
                Mathf.Max(Time.fixedDeltaTime, collisionData.TimeInterval));
            var projectileImpact = new Impact();
            var reflectedImpact = new Impact();
            projectile.CollisionBehaviour.Process(target, attacker, reflectedData,
                ref projectileImpact, ref reflectedImpact);
            attacker.OnCollision(reflectedImpact, target, reflectedData);
            projectile.Vanish();
            _timeFromLastHit = 0f;
            return true;
        }

        private bool ReflectMissile(Combat.Component.Bullet.IBullet projectile, IShip attacker,
            CollisionData collisionData)
        {
            // Missile explosions and child-warhead triggers are tied to the
            // projectile's own lifecycle.  The old Mirror Sea path resolved
            // only CollisionBehaviour and then Vanish()ed the missile, which
            // bypassed OnCollision/OnDetonate and made explosive missiles look
            // "reflected" while doing no proper missile damage.  Keep the live
            // missile instead and send it back under the shield owner's side.
            projectile.Type.Owner = _parent;
            projectile.Type.FactionOverride = null;
            projectile.Type.FactionId = _parent.Type.FactionId;
            projectile.GuidanceTarget = attacker;
            if (projectile.Collider != null)
            {
                projectile.Collider.Source = _parent;
                // Reassign Unit so CommonCollider immediately refreshes the
                // Unity physics layer to the reflected projectile's new side.
                projectile.Collider.Unit = projectile;
            }

            if (projectile is Combat.Component.Bullet.Bullet concreteProjectile)
            {
                if (concreteProjectile.Controller is HomingController homing)
                    homing.Retarget(attacker);
                else if (concreteProjectile.Controller is WarpMissileController warp)
                    warp.Retarget(attacker);
            }

            var contact = collisionData.Position;
            var shieldCenter = Body.WorldPosition();
            var outward = BattlefieldGeometry.Delta(shieldCenter, contact);
            var currentVelocity = projectile.Body.WorldVelocity();
            if (outward.sqrMagnitude < 0.0001f)
                outward = currentVelocity.sqrMagnitude > 0.0001f
                    ? -currentVelocity.normalized
                    : Vector2.right;
            else
                outward.Normalize();

            var returnDirection = BattlefieldGeometry.Delta(contact, attacker.Body.WorldPosition());
            if (returnDirection.sqrMagnitude < 0.0001f)
                returnDirection = Vector2.Reflect(
                    currentVelocity.sqrMagnitude > 0.0001f ? currentVelocity.normalized : -outward,
                    outward);
            if (returnDirection.sqrMagnitude < 0.0001f)
                returnDirection = outward;
            else
                returnDirection.Normalize();

            // Move just outside the trigger volume before changing velocity so
            // the same physics step cannot immediately report another hostile
            // Mirror Sea contact for the reflected missile.
            var clearance = Mathf.Max(0.75f, projectile.Body.WorldScale() * 0.75f);
            projectile.Body.Move(contact + outward * clearance);
            projectile.Body.Turn(RotationHelpers.Angle(returnDirection));
            var reflectedSpeed = Mathf.Max(25f, currentVelocity.magnitude);
            projectile.Body.ApplyAcceleration(returnDirection * reflectedSpeed - projectile.Body.Velocity);

            MirrorSeaReflectionVisual.Show(shieldCenter,
                Mathf.Max(3f, Body.WorldScale() * 0.95f), _parent.View.Color);
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

    internal sealed class MirrorSeaReflectionVisual : MonoBehaviour
    {
        public static void Show(Vector2 center, float radius, Color color)
        {
            var root = new GameObject("MirrorSeaReflectionRing");
            root.transform.position = center;
            var effect = root.AddComponent<MirrorSeaReflectionVisual>();
            effect._remaining = Lifetime;
            effect._material = new Material(Shader.Find("Sprites/Default"));
            effect._line = root.AddComponent<LineRenderer>();
            effect._line.material = effect._material;
            effect._line.loop = true;
            effect._line.useWorldSpace = false;
            effect._line.positionCount = Segments;
            effect._line.startWidth = 0.18f;
            effect._line.endWidth = 0.18f;
            effect._line.sortingOrder = 140;
            effect._baseColor = color;
            for (var i = 0; i < Segments; ++i)
            {
                var a = 2f * Mathf.PI * i / Segments;
                effect._line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        private void Update()
        {
            _remaining -= Time.deltaTime;
            var ratio = Mathf.Clamp01(_remaining / Lifetime);
            var color = _baseColor;
            color.a = ratio;
            _line.startColor = color;
            _line.endColor = color;
            transform.Rotate(0f, 0f, 180f * Time.deltaTime);
            if (_remaining <= 0f)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        private const int Segments = 48;
        private const float Lifetime = 0.4f;
        private LineRenderer _line;
        private Material _material;
        private Color _baseColor;
        private float _remaining;
    }
}
