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
    /// Keeps ordinary homing guidance, but performs one short-range warp after
    /// the missile has been in flight for two seconds.
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
            if (_warped)
                return;

            _elapsed += elapsedTime;
            if (!_chargeEffectPlayed && _elapsed >= WarpDelay - ChargeEffectLeadTime)
            {
                _chargeEffectPlayed = true;
                SpawnEffect(_unit.Body.WorldPosition(), _unit.Body.WorldScale() * 1.8f,
                    ChargeEffectLeadTime, 540f);
            }

            if (_elapsed < WarpDelay)
                return;

            var target = _homing.Target;
            if (!target.IsActive())
                return;

            WarpToTarget(target);
        }

        private void WarpToTarget(IUnit target)
        {
            var body = _unit.Body;
            var departure = body.WorldPosition();
            var targetPosition = BattlefieldGeometry.NearestEquivalent(departure, target.Body.WorldPosition());
            var approach = BattlefieldGeometry.Delta(departure, targetPosition);
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

            _warped = true;
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
        private bool _warped;

        private const string WarpEffectName = "FlashAdditive";
        private const float WarpDelay = 2f;
        private const float ChargeEffectLeadTime = 0.35f;
        private const float WarpArrivalDistance = 25f;
        private const float WarpEffectLifetime = 0.45f;
    }
}
