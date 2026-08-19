using Combat.Component.Body;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Scene;
using Combat.Unit;
using UnityEngine;

namespace Combat.Component.Controller
{
    public class MagneticController : IController
    {
        public MagneticController(IUnit unit, float maxVelocity, float acceleration, float maxRange, bool lookForward, bool smartAim, IScene scene)
        {
            _unit = unit;
            _scene = scene;
            _maxVelocity = maxVelocity;
            _acceleration = acceleration;
            _maxRange = maxRange;
            _lookForward = lookForward;
            _smartAim = smartAim;
        }

        public void Dispose() { }

        public void UpdatePhysics(float elapsedTime)
        {
            _timeFromLastUpdate += elapsedTime;

            if (_timeFromLastUpdate > _targetUpdateCooldown)
            {
                _target = FindTarget();
                _timeFromLastUpdate = 0;
            }

            UpdateVelocity(elapsedTime);
            
            if (_lookForward)
                UpdateRotation();
        }

        private IUnit FindTarget()
        {
            if (_unit.Type.Side == UnitSide.Player)
            {
                var locked = _scene.LockedEnemyShip;
                return locked.IsActive() && CombatRelations.AreEnemies(_unit.Type, locked.Type) ? locked : null;
            }

            if (_unit.Type.Side == UnitSide.Enemy)
            {
                var player = _scene.PlayerShip;
                if (player.IsActive() && CombatRelations.AreEnemies(_unit.Type, player.Type))
                    return player;
            }

            return _scene.Ships.GetEnemyForMissile(_unit, 0f, _maxRange * 1.3f, _lookForward ? 90f : 360f, false, false);
        }

        private void UpdateRotation()
        {
            if (_unit.Body.Parent == null)
            {
                var rotation = RotationHelpers.Angle(_unit.Body.Velocity);
                var requiredAngularVelocity = UnityEngine.Mathf.DeltaAngle(_unit.Body.Rotation, rotation) * 10;
            
                var acceleration = requiredAngularVelocity - _unit.Body.AngularVelocity;
                _unit.Body.ApplyAngularAcceleration(acceleration);
            }
            else
            {
                // If parent is present, we can not move, so just rotate towards the target
                if (_target == null || !_target.IsActive()) return;

                var origin = _unit.Body.WorldPosition();
                var nearestTargetPosition = BattlefieldGeometry.NearestEquivalent(origin, _target.Body.WorldPosition());
                if (!_smartAim || !Geometry.GetTargetPosition(nearestTargetPosition, _target.Body.Velocity,
                        origin,
                        _maxVelocity, out var targetPosition, out _))
                {
                    targetPosition = nearestTargetPosition;
                }
                
                var direction = targetPosition - origin;
                var target = RotationHelpers.Angle(direction);
                var rotation = _unit.Body.WorldRotation();
                var delta = Mathf.DeltaAngle(rotation, target);
                if(!Mathf.Approximately(delta, 0))
                    _unit.Body.Turn(_unit.Body.Rotation + delta);
            }
        }

        private void UpdateVelocity(float deltaTime)
        {
            if (_target == null || !_target.IsActive() || _unit.Body.Parent != null) return;
            
            var position = _unit.Body.WorldPosition();
            var velocity = _unit.Body.Velocity;
            var nearestTargetPosition = BattlefieldGeometry.NearestEquivalent(position, _target.Body.WorldPosition());

            if (!_smartAim || !Geometry.GetTargetPosition(nearestTargetPosition, _target.Body.Velocity,
                    position,
                    _maxVelocity, out var targetPosition, out _))
            {
                var targetVelocity = _target.Body.Velocity;
                targetPosition = nearestTargetPosition;
                var timeToTarget = (targetPosition - position).magnitude / _maxVelocity;
                targetPosition += targetVelocity * timeToTarget;
            }

            var requiredVelocity = (targetPosition - position).normalized * _maxVelocity;
            _unit.Body.ApplyAcceleration((requiredVelocity - velocity).normalized * _acceleration * deltaTime);
        }

        private float _timeFromLastUpdate = _targetUpdateCooldown;
        private IUnit _target;
        private readonly bool _lookForward;
        private readonly bool _smartAim;
        private readonly IUnit _unit;
        private readonly IScene _scene;
        private readonly float _maxVelocity;
        private readonly float _acceleration;
        private readonly float _maxRange;
        private const float _targetUpdateCooldown = 0.25f;
    }
}
