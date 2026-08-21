using Combat.Component.Body;
using Combat.Component.Unit;
using Combat.Factory;
using Combat.Scene;
using Combat.Unit;
using GameDatabase.Model;
using UnityEngine;

namespace Combat.Component.Controller
{
    /// <summary>
    /// Keeps ordinary homing guidance, but starts slowly and evaluates one
    /// short-range warp after half a second.
    /// </summary>
    public sealed class WarpMissileController : IController
    {
        public WarpMissileController(IUnit unit, HomingController homing, EffectFactory effectFactory,
            Color color, float cruiseVelocity)
        {
            _unit = unit;
            _homing = homing;
            _effectFactory = effectFactory;
            _color = color;
            _cruiseVelocity = cruiseVelocity;
        }

        public void UpdatePhysics(float elapsedTime)
        {
            _homing.UpdatePhysics(elapsedTime);
            if (_warpAttempted)
                return;

            _elapsed += elapsedTime;
            ClampPreWarpVelocity();
            if (!_chargeEffectPlayed && _elapsed >= WarpDelay - ChargeEffectLeadTime)
            {
                _chargeEffectPlayed = true;
                SpawnEffect(_unit.Body.WorldPosition(), _unit.Body.WorldScale() * 1.8f,
                    ChargeEffectLeadTime, 540f);
            }

            if (_elapsed < WarpDelay)
                return;

            // Evaluate the warp opportunity exactly once. A close target also
            // consumes this opportunity, so the missile cannot warp later if
            // the target subsequently moves away.
            _warpAttempted = true;
            var target = _homing.Target;
            if (!target.IsActive())
            {
                RestoreCruiseVelocity();
                return;
            }

            var departure = _unit.Body.WorldPosition();
            if (BattlefieldGeometry.Distance(departure, target.Body.WorldPosition()) <= WarpSuppressionDistance)
            {
                RestoreCruiseVelocity();
                return;
            }

            WarpToTarget(target);
        }

        public void Retarget(IUnit target)
        {
            _homing.Retarget(target);
        }

        private void ClampPreWarpVelocity()
        {
            var body = _unit.Body;
            var direction = body.Velocity;
            if (direction.sqrMagnitude < 0.0001f)
                direction = RotationHelpers.Direction(body.WorldRotation());
            else
                direction.Normalize();

            var desired = direction * (_cruiseVelocity * PreWarpSpeedMultiplier);
            body.ApplyAcceleration(desired - body.Velocity);
        }

        private void RestoreCruiseVelocity()
        {
            var body = _unit.Body;
            var direction = body.Velocity;
            if (direction.sqrMagnitude < 0.0001f)
                direction = RotationHelpers.Direction(body.WorldRotation());
            else
                direction.Normalize();
            body.ApplyAcceleration(direction * _cruiseVelocity - body.Velocity);
        }

        private void WarpToTarget(IUnit target)
        {
            var body = _unit.Body;
            var departure = body.WorldPosition();
            var delta = BattlefieldGeometry.Delta(departure, target.Body.WorldPosition());
            var targetPosition = departure + delta;
            var approach = delta;
            if (approach.sqrMagnitude < 0.0001f)
                approach = RotationHelpers.Direction(body.WorldRotation());
            else
                approach.Normalize();

            // Arrive on the current approach side of the target. This keeps
            // the post-warp trajectory stable while guaranteeing ~25 units of
            // separation instead of teleporting directly into the collider.
            var arrival = targetPosition - approach * WarpArrivalDistance;

            SpawnEffect(departure, body.WorldScale() * 2.4f, WarpEffectLifetime, 720f);
            SpawnEffect(arrival, body.WorldScale() * 2.8f, WarpEffectLifetime, -720f);

            body.Move(arrival);
            var finalDirection = targetPosition - arrival;
            if (finalDirection.sqrMagnitude > 0.0001f)
            {
                finalDirection.Normalize();
                body.Turn(RotationHelpers.Angle(finalDirection));
                body.ApplyAcceleration(finalDirection * _cruiseVelocity - body.Velocity);
            }

        }

        private void SpawnEffect(Vector2 position, float size, float lifetime, float rotationSpeed)
        {
            var effect = _effectFactory.CreateEffect(WarpEffectName);
            effect.Color = _color;
            effect.Position = position;
            effect.Size = Mathf.Max(0.8f, size);
            effect.Run(lifetime, Vector2.zero, rotationSpeed);
        }

        public void Dispose()
        {
            _homing.Dispose();
        }

        private readonly IUnit _unit;
        private readonly HomingController _homing;
        private readonly EffectFactory _effectFactory;
        private readonly Color _color;
        private readonly float _cruiseVelocity;
        private float _elapsed;
        private bool _chargeEffectPlayed;
        private bool _warpAttempted;

        private const string WarpEffectName = "FlashAdditive";
        private const float WarpDelay = 0.5f;
        private const float ChargeEffectLeadTime = 0.2f;
        private const float PreWarpSpeedMultiplier = 0.1f;
        private const float WarpArrivalDistance = 25f;
        private const float WarpSuppressionDistance = 30f;
        private const float WarpEffectLifetime = 0.45f;
    }
}
