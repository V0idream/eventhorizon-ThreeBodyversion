using Combat.Component.Body;
using Combat.Component.Bullet;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Scene;
using Combat.Unit;
using UnityEngine;

namespace Combat.Component.Controller
{
    public class HomingController : IController
    {
        public HomingController(IUnit unit, float maxVelocity, float maxAngularVelocity, float acceleration,
            float maxRange, bool smartAim, IScene scene, IUnit preferredTarget = null)
        {
            _unit = unit;
            _scene = scene;
            _maxVelocity = maxVelocity;
            _maxAngularVelocity = maxAngularVelocity;
            _acceleration = acceleration;
            _maxRange = maxRange;
            _smartAim = smartAim;
            _preferredTarget = preferredTarget;
            _target = IsValidTarget(preferredTarget) ? preferredTarget : null;
            UpdateGuidanceTarget();
        }

        public IUnit Target => _target;

        public void Dispose() { }

        public void UpdatePhysics(float elapsedTime)
        {
            _timeFromLastUpdate += elapsedTime;

            if (_timeFromLastUpdate > TargetUpdateCooldown)
            {
                _target = FindTarget();
                UpdateGuidanceTarget();
                _timeFromLastUpdate = 0f;
            }

            UpdateVelocity(elapsedTime);
            UpdateRotation(elapsedTime);
        }

        private IUnit FindTarget()
        {
            if (_unit.Type.Side == UnitSide.Player)
            {
                // Prefer the player's current explicit lock. If it disappears
                // during a split, retain the last valid target inherited from
                // the parent warhead instead of making every child unguided.
                var locked = _scene.LockedTarget;
                if (IsValidTarget(locked))
                {
                    _preferredTarget = locked;
                    return locked;
                }

                return IsValidTarget(_preferredTarget) ? _preferredTarget : null;
            }

            if (_unit.Type.Side == UnitSide.Enemy)
            {
                var player = _scene.PlayerShip;
                if (IsValidTarget(player))
                    return player;
            }

            if (IsValidTarget(_preferredTarget))
                return _preferredTarget;

            return _scene.Ships.GetEnemyForMissile(_unit, 0f, _maxRange * 1.3f, 90f, false, false);
        }

        private bool IsValidTarget(IUnit target)
        {
            return target.IsActive() && CombatRelations.AreEnemies(_unit.Type, target.Type);
        }

        private void UpdateGuidanceTarget()
        {
            if (_unit is IBullet bullet)
                bullet.GuidanceTarget = _target;
        }

        private void UpdateVelocity(float deltaTime)
        {
            if (_unit.Body.Parent != null) return;

            var forward = RotationHelpers.Direction(_unit.Body.Rotation);
            var velocity = _unit.Body.Velocity;
            var forwardVelocity = Vector2.Dot(velocity, forward);
            if (forwardVelocity >= _maxVelocity)
                return;

            var requiredVelocity = Mathf.Max(forwardVelocity, _maxVelocity) * forward;
            var dir = (requiredVelocity - velocity).normalized;

            _unit.Body.ApplyAcceleration(dir * _acceleration * deltaTime);
        }

        private void UpdateRotation(float elapsedTime)
        {
            var requiredAngularVelocity = 0f;
            if (_target.IsActive())
            {
                var origin = _unit.Body.WorldPosition();
                var nearestTargetPosition = BattlefieldGeometry.NearestEquivalent(origin, _target.Body.WorldPosition());
                if (!_smartAim || !Geometry.GetTargetPosition(nearestTargetPosition, _target.Body.Velocity,
                        origin, _maxVelocity, out var targetPosition, out _))
                    targetPosition = nearestTargetPosition;

                var direction = targetPosition - origin;
                var target = RotationHelpers.Angle(direction);
                var rotation = _unit.Body.WorldRotation();
                var delta = Mathf.DeltaAngle(rotation, target);
                requiredAngularVelocity = delta > 5f
                    ? _maxAngularVelocity
                    : delta < -5f
                        ? -_maxAngularVelocity
                        : 0f;
            }

            if (_unit.Body.Parent == null)
            {
                _unit.Body.ApplyAngularAcceleration(requiredAngularVelocity - _unit.Body.AngularVelocity);
            }
            else
            {
                _computedVelocity += (requiredAngularVelocity - _computedVelocity) * Mathf.Deg2Rad /
                                     (0.2f + _unit.Body.Weight * 2f);
                var turn = _unit.Body.Rotation + _computedVelocity * elapsedTime;
                if (!Mathf.Approximately(turn, 0f))
                    _unit.Body.Turn(turn);
            }
        }

        private float _timeFromLastUpdate = TargetUpdateCooldown;
        private float _computedVelocity;
        private readonly bool _smartAim;
        private IUnit _target;
        private IUnit _preferredTarget;
        private readonly IUnit _unit;
        private readonly IScene _scene;
        private readonly float _maxVelocity;
        private readonly float _maxAngularVelocity;
        private readonly float _acceleration;
        private readonly float _maxRange;
        private const float TargetUpdateCooldown = 0.25f;
    }
}
