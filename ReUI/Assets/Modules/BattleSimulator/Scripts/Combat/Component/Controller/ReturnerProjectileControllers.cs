using Combat.Collision;
using Combat.Collision.Behaviour.Action;
using Combat.Component.Bullet;
using Combat.Component.Body;
using Combat.Component.Ship;
using Combat.Component.Stats;
using Combat.Component.Systems.Devices;
using Combat.Component.Unit;
using Combat.Unit;
using UnityEngine;

namespace Combat.Component.Controller
{
    /// <summary>
    /// "Creed" is emitted from the far side of the selected target and travels
    /// back toward the firing ship.  It is intentionally unguided after the
    /// initial placement so every object on that line can be pierced.
    /// </summary>
    public sealed class CreedProjectileController : IController
    {
        public CreedProjectileController(IBullet bullet, IShip owner, IUnit target, float speed, float damage)
        {
            _bullet = bullet;
            _owner = owner;
            _target = target;
            _speed = Mathf.Max(1f, speed);
            _damage = Mathf.Max(0f, damage);
        }

        public void UpdatePhysics(float elapsedTime)
        {
            if (!_bullet.IsActive())
                return;

            if (!_initialized)
            {
                _initialized = true;
                if (!_owner.IsActive() || !_target.IsActive())
                {
                    _bullet.Vanish();
                    return;
                }

                var ownerPosition = _owner.Body.WorldPosition();
                var targetPosition = _target.Body.WorldPosition();
                var outward = targetPosition - ownerPosition;
                if (outward.sqrMagnitude < 0.0001f)
                    outward = RotationHelpers.Direction(_target.Body.WorldRotation());
                outward.Normalize();

                var offset = Mathf.Max(8f,
                    _target.Body.WorldScale() * 1.35f + _bullet.Body.WorldScale() * 2f);
                var spawnPosition = targetPosition + outward * offset;
                var direction = (ownerPosition - spawnPosition).normalized;
                var rotation = RotationHelpers.Angle(direction);

                MoveWorld(spawnPosition);
                TurnWorld(rotation);
                _bullet.Body.ApplyAcceleration(direction * _speed - _bullet.Body.WorldVelocity());
                _lastPosition = spawnPosition;
                return;
            }

            var currentPosition = _bullet.Body.WorldPosition();
            TryResolveTargetCrossing(_lastPosition, currentPosition, elapsedTime);
            _lastPosition = currentPosition;

            if (!_bullet.IsActive())
                return;

            if (!_owner.IsActive())
                return;

            var vanishRadius = Mathf.Max(1.5f,
                _owner.Body.WorldScale() * 0.45f + _bullet.Body.WorldScale());
            if ((_owner.Body.WorldPosition() - _bullet.Body.WorldPosition()).sqrMagnitude <=
                vanishRadius * vanishRadius)
                _bullet.Vanish();
        }

        public void Dispose()
        {
            CreedDamageAction.Forget(_bullet);
        }

        private void TryResolveTargetCrossing(Vector2 from, Vector2 to, float elapsedTime)
        {
            if (_targetCrossingResolved || !_target.IsActive())
                return;

            if (CreedDamageAction.HasRecordedHit(_bullet, _target))
            {
                _targetCrossingResolved = true;
                return;
            }

            var targetPosition = _target.Body.WorldPosition();
            var segment = to - from;
            var denominator = segment.sqrMagnitude;
            var t = denominator > 0.0001f
                ? Mathf.Clamp01(Vector2.Dot(targetPosition - from, segment) / denominator)
                : 0f;
            var closest = from + segment * t;
            var radius = Mathf.Max(1.5f,
                _target.Body.WorldScale() * 0.55f + _bullet.Body.WorldScale() * 0.5f);
            if ((targetPosition - closest).sqrMagnitude > radius * radius)
                return;

            _targetCrossingResolved = true;
            var targetShip = _target as IShip;
            if (targetShip == null || ShipStats.IsFourDimensionalUnit(targetShip))
                return;

            // Mirror Sea has priority over Creed's swept-hit fallback. This is
            // essential because Creed is itself a piercing weapon; otherwise
            // the anti-tunnelling fix would accidentally bypass the field.
            var systems = targetShip.Systems?.All;
            if (systems != null)
            {
                for (var i = 0; i < systems.Count; ++i)
                {
                    if (systems[i] is MirrorSeaFieldDevice mirror && mirror.IsFieldEnabled)
                    {
                        if (mirror.TryInterceptProjectile(_bullet, closest, elapsedTime))
                            return;
                    }
                }
            }

            if (!CreedDamageAction.RecordGuaranteedHit(_bullet, targetShip))
                return;

            targetShip.Affect(new Impact
            {
                TrueDamage = _damage,
                ShieldDamage = 2_100_000_000f,
                IgnoresShield = true,
            }, _owner);
        }

        private void MoveWorld(Vector2 position)
        {
            _bullet.Body.Move(_bullet.Body.Parent == null
                ? position
                : _bullet.Body.WorldPositionToLocal(position));
        }

        private void TurnWorld(float rotation)
        {
            _bullet.Body.Turn(_bullet.Body.Parent == null
                ? rotation
                : _bullet.Body.WorldRotationToLocal(rotation));
        }

        private readonly IBullet _bullet;
        private readonly IShip _owner;
        private readonly IUnit _target;
        private readonly float _speed;
        private readonly float _damage;
        private Vector2 _lastPosition;
        private bool _targetCrossingResolved;
        private bool _initialized;
    }

    /// <summary>
    /// Physical motion for every Fractal projectile.  The local trajectory
    /// rotates at a fixed angular rate while the whole barrage shares one
    /// translational drift vector.  Travel distance is measured from the actual
    /// world path rather than lifetime, so the 200-unit limit remains exact even
    /// while the trajectory curves.
    /// </summary>
    public sealed class FractalProjectileController : IController
    {
        public FractalProjectileController(IBullet bullet, Vector2 driftDirection,
            float rotationSpeed, float projectileSpeed, float driftSpeed, float maxDistance)
        {
            _bullet = bullet;
            _driftDirection = driftDirection.sqrMagnitude > 0.0001f
                ? driftDirection.normalized
                : Vector2.right;
            _rotationSpeed = rotationSpeed;
            _projectileSpeed = Mathf.Max(0f, projectileSpeed);
            _driftSpeed = Mathf.Max(0f, driftSpeed);
            _maxDistance = Mathf.Max(0.1f, maxDistance);
            _angle = bullet.Body.WorldRotation();
            _lastPosition = bullet.Body.WorldPosition();

            // A freshly split projectile can inherit zero velocity from a parent
            // that has already reached its own travel limit. Assign its velocity
            // immediately instead of waiting for a later fixed-update tick.
            ApplyDesiredVelocity();
        }

        public void UpdatePhysics(float elapsedTime)
        {
            if (!_bullet.IsActive())
                return;

            var position = _bullet.Body.WorldPosition();
            if (!_travelLimitReached)
            {
                var segment = position - _lastPosition;
                var segmentLength = segment.magnitude;
                if (_distance + segmentLength >= _maxDistance)
                {
                    var remaining = Mathf.Max(0f, _maxDistance - _distance);
                    var clampedPosition = segmentLength > 0.0001f
                        ? _lastPosition + segment * (remaining / segmentLength)
                        : _lastPosition;
                    MoveWorld(clampedPosition);
                    _lastPosition = clampedPosition;
                    _distance = _maxDistance;
                    _travelLimitReached = true;
                    _bullet.Body.ApplyAcceleration(-_bullet.Body.WorldVelocity());
                    // Keep the logical node alive so it can seed later fractal
                    // generations, but remove its stationary physical/visual
                    // representation. This preserves all 12 recursion stages
                    // without leaving apparently frozen ammunition on screen.
                    _bullet.Collider.Enabled = false;
                    _bullet.View.Size = 0f;
                    return;
                }

                _distance += segmentLength;
                _lastPosition = position;
            }
            else
            {
                // Reaching the 200-unit limit stops physical travel instead of
                // destroying the logical projectile. Fractal recursion keeps
                // every representative alive for all 12 stages, which is what
                // allows the C6 tree to reach 6 * 2^12 = 24,576 projectiles.
                _bullet.Body.ApplyAcceleration(-_bullet.Body.WorldVelocity());
                return;
            }

            _angle += _rotationSpeed * Mathf.Max(0f, elapsedTime);
            ApplyDesiredVelocity();
            _bullet.Body.Turn(_bullet.Body.Parent == null
                ? _angle
                : _bullet.Body.WorldRotationToLocal(_angle));
        }

        public void Dispose() { }

        private void MoveWorld(Vector2 position)
        {
            _bullet.Body.Move(_bullet.Body.Parent == null
                ? position
                : _bullet.Body.WorldPositionToLocal(position));
        }

        private void ApplyDesiredVelocity()
        {
            var desiredVelocity = RotationHelpers.Direction(_angle) * _projectileSpeed +
                                  _driftDirection * _driftSpeed;
            _bullet.Body.ApplyAcceleration(desiredVelocity - _bullet.Body.WorldVelocity());
        }

        private readonly IBullet _bullet;
        private readonly Vector2 _driftDirection;
        private readonly float _rotationSpeed;
        private readonly float _projectileSpeed;
        private readonly float _driftSpeed;
        private readonly float _maxDistance;
        private Vector2 _lastPosition;
        private float _angle;
        private float _distance;
        private bool _travelLimitReached;
    }
}
