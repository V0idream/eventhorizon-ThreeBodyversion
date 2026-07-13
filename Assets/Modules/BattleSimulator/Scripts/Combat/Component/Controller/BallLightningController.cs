using System;
using Combat.Collision;
using Combat.Component.Body;
using Combat.Component.Ship;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Effects;
using Combat.Factory;
using Combat.Helpers;
using Combat.Scene;
using Combat.Unit;
using GameDatabase.Enums;
using UnityEngine;

namespace Combat.Component.Controller
{
    /// <summary>
    /// Controller for the Starship Earth macro-electron.  The projectile travels
    /// normally until it reaches an enemy or its range limit, then becomes a
    /// stationary ball-lightning source for one second before releasing the
    /// colour-scaled radial discharge.
    /// </summary>
    public sealed class BallLightningController : IController
    {
        public BallLightningController(Combat.Component.Bullet.Bullet bullet, IScene scene, EffectFactory effectFactory,
            IShip owner, float range)
        {
            _bullet = bullet;
            _scene = scene;
            _effectFactory = effectFactory;
            _owner = owner;
            _range = Mathf.Max(1f, range);
        }

        public bool IsArmed => _armed;

        public void ReceiveDamage(float damage)
        {
            if (damage > 0f)
                _receivedDamage += damage;
            Arm();
        }

        public void Arm()
        {
            if (_armed || !_bullet.IsActive())
                return;

            _armed = true;
            _armTimer = 1f;
            _tickTimer = 0f;
            _origin = _bullet.Body.WorldPosition();
            _bullet.Body.ApplyAcceleration(-_bullet.Body.Velocity);
            if (_bullet.Collider != null)
                _bullet.Collider.Enabled = false;
        }

        public void UpdatePhysics(float elapsedTime)
        {
            if (!_bullet.IsActive())
                return;

            if (!_initialized)
            {
                _origin = _bullet.Body.WorldPosition();
                _initialized = true;
            }

            if (!_armed)
            {
                if (Vector2.Distance(_origin, _bullet.Body.WorldPosition()) >= _range)
                    Arm();
                return;
            }

            _armTimer -= elapsedTime;
            if (_armTimer > 0f)
                return;

            _tickTimer -= elapsedTime;
            if (!_discharged)
            {
                Discharge();
                _discharged = true;
                _tickTimer = 0.5f;
            }
            else if (_tickTimer <= 0f)
            {
                Discharge();
                _tickTimer = 0.5f;
            }

            _duration -= elapsedTime;
            if (_duration <= 0f)
                _bullet.Detonate();
        }

        private void Discharge()
        {
            var tier = Mathf.Clamp(Mathf.FloorToInt(_receivedDamage / 200f), 0, 6);
            var damage = 50f * Mathf.Pow(1.5f, tier);
            var color = TierColors[tier];

            if (!_durationInitialized)
            {
                _duration = 8f * Mathf.Pow(1.2f, tier);
                _durationInitialized = true;
            }

            var sourcePosition = _bullet.Body.WorldPosition();
            var units = _scene.Units.Items;
            for (var i = 0; i < units.Count; ++i)
            {
                if (!(units[i] is IShip target) || !target.IsActive() || target == _owner)
                    continue;
                if (!CombatRelations.AreEnemies(_owner.Type, target.Type))
                    continue;

                var targetPosition = target.Body.WorldPosition();
                var distance = Vector2.Distance(sourcePosition, targetPosition);
                if (distance > 30f)
                    continue;

                target.Affect(new Impact { EnergyDamage = damage }, _owner);
                target.Body.ApplyAcceleration(-target.Body.Velocity * 0.7f);

                var lightning = _effectFactory.CreateEffect("Lightning", target.Body);
                if (lightning == null || !lightning.IsAlive)
                    continue;
                lightning.Position = Vector2.zero;
                lightning.Rotation = RotationHelpers.Angle(sourcePosition - targetPosition);
                lightning.Size = Mathf.Max(1f, distance);
                lightning.Color = color;
                lightning.Run(0.48f, Vector2.zero, 0f);
            }
        }

        public void Dispose() { }

        private static readonly Color[] TierColors =
        {
            new Color(1f, 0.08f, 0.08f, 0.95f),
            new Color(1f, 0.46f, 0.05f, 0.95f),
            new Color(1f, 0.9f, 0.05f, 0.95f),
            new Color(0.15f, 1f, 0.25f, 0.95f),
            new Color(0.15f, 0.55f, 1f, 0.95f),
            new Color(0.25f, 0.2f, 1f, 0.95f),
            new Color(0.75f, 0.25f, 1f, 0.95f),
        };

        private readonly Combat.Component.Bullet.Bullet _bullet;
        private readonly IScene _scene;
        private readonly EffectFactory _effectFactory;
        private readonly IShip _owner;
        private readonly float _range;
        private Vector2 _origin;
        private float _receivedDamage;
        private float _armTimer;
        private float _tickTimer;
        private float _duration;
        private bool _initialized;
        private bool _armed;
        private bool _discharged;
        private bool _durationInitialized;
    }
}
