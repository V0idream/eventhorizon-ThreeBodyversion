using Combat.Component.Bullet;
using Combat.Component.Controller;
using Combat.Component.Platform;
using Combat.Component.Ship;
using Combat.Component.Triggers;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Scene;
using Combat.Unit;
using GameDatabase.DataModel;
using UnityEngine;

namespace Combat.Component.Systems.Weapons
{
    public sealed class AutoPointDefenseLaser : WeaponBase
    {
        public AutoPointDefenseLaser(IWeaponPlatform platform, WeaponStats weaponStats, Factory.IBulletFactory bulletFactory,
            int keyBinding, IScene scene, IShip owner, bool interceptAllProjectiles = false,
            bool preferProjectilesEvenIfReserved = false)
            : base(platform, weaponStats, bulletFactory, keyBinding)
        {
            _scene = scene;
            _owner = owner;
            _protectedShip = interceptAllProjectiles && owner.Type.Owner is IShip mothership
                ? mothership
                : owner;
            _energyConsumption = bulletFactory.Stats.EnergyCost;
            _interceptAllProjectiles = interceptAllProjectiles;
            _preferProjectilesEvenIfReserved = preferProjectilesEvenIfReserved;
        }

        public override bool CanBeActivated => false;
        public override float Cooldown => 0f;
        public override IBullet ActiveBullet => HasActiveBullet ? _activeBullet : null;

        protected override void OnUpdateView(float elapsedTime) {}

        protected override void OnUpdatePhysics(float elapsedTime)
        {
            if (Combat.Component.Ship.Effects.SmallUniverseTransitEffect.IsInTransit(_owner))
            {
                SetReservedTarget(null);
                SetTarget(null);
                _currentTarget = null;
                if (HasActiveBullet)
                {
                    _activeBullet.Vanish();
                    TimeFromLastUse = 0f;
                    InvokeTriggers(ConditionType.OnDeactivate);
                }
                return;
            }

            var target = FindTarget();
            if (!IsTargetInsideWeaponRange(target))
                target = null;

            // Recreate the live beam immediately when a missile preempts a
            // ship target. Some bound beam controllers retain their original
            // target until destruction. Destroy the old beam before assigning
            // the new platform target; otherwise its cleanup clears the newly
            // assigned target and the weapon falls back to the ship.
            if (target != _currentTarget && HasActiveBullet)
            {
                _activeBullet.Vanish();
                TimeFromLastUse = 0f;
                InvokeTriggers(ConditionType.OnDeactivate);
            }
            _currentTarget = target;
            // Ship-mounted interceptors spread their fire. Defence drones are
            // intentionally allowed to focus the same missile, since a single
            // drone beam is not reliable enough against heavy ordnance.
            SetReservedTarget(!_interceptAllProjectiles && target != null && IsInterceptableProjectile(target)
                ? target
                : null);
            SetTarget(target);

            if (!target.IsActive())
            {
                if (HasActiveBullet)
                {
                    _activeBullet.Vanish();
                    TimeFromLastUse = 0;
                    InvokeTriggers(ConditionType.OnDeactivate);
                }

                return;
            }

            if (HasActiveBullet)
            {
                Aim();
                if (TryConsumeEnergy(_energyConsumption * elapsedTime))
                {
                    _activeBullet.Lifetime.Restore();
                    InvokeTriggers(ConditionType.OnRemainActive);
                    return;
                }
            }
            else if (TryConsumeEnergy(ActivationCost))
            {
                Aim();
                _activeBullet = CreateBullet();
                _activeBullet.Lifetime.Restore();
                InvokeTriggers(ConditionType.OnActivate);
                return;
            }

            if (HasActiveBullet)
            {
                _activeBullet.Vanish();
                TimeFromLastUse = 0;
                InvokeTriggers(ConditionType.OnDeactivate);
            }
        }

        protected override void OnDispose()
        {
            SetReservedTarget(null);
            if (BulletFactory.Stats.IsBoundToCannon)
                _activeBullet?.Vanish();
        }

        private IUnit FindTarget()
        {
            var position = Platform.Body.WorldPosition();
            var range = Info.Range;
            IUnit nearestMissile = null;
            IUnit nearestReservedMissile = null;
            IUnit nearestOtherProjectile = null;
            var missileDistance = float.MaxValue;
            var reservedMissileDistance = float.MaxValue;
            var shortestMissileImpactTime = float.MaxValue;
            var shortestOtherImpactTime = float.MaxValue;

            var candidates = InterceptionTargetCoordinator.GetProjectileCandidates(_scene);
            for (var candidateIndex = 0; candidateIndex < candidates.Count; ++candidateIndex)
            {
                var unit = candidates[candidateIndex];
                if (!unit.IsActive() || !IsInterceptableProjectile(unit) ||
                    !CanTargetProjectile(unit) ||
                    unit is IBullet { IsInterceptionProjectile: true })
                    continue;

                var reservedByOther = !_interceptAllProjectiles &&
                    InterceptionTargetCoordinator.IsReservedByOther(unit, this, _owner,
                        _preferProjectilesEvenIfReserved);
                if (reservedByOther && !_preferProjectilesEvenIfReserved)
                    continue;

                var impactTime = 0f;
                if (_interceptAllProjectiles && !ThreatensProtectedShip(unit, out impactTime))
                    continue;

                var distance = BattlefieldGeometry.SqrDistance(position, unit.Body.WorldPosition());
                if (distance > range * range)
                    continue;

                if (reservedByOther)
                {
                    if (distance < reservedMissileDistance)
                    {
                        reservedMissileDistance = distance;
                        nearestReservedMissile = unit;
                    }
                    continue;
                }

                if (_interceptAllProjectiles)
                {
                    if (unit.Type.Class == UnitClass.Missile)
                    {
                        if (impactTime >= shortestMissileImpactTime) continue;
                        shortestMissileImpactTime = impactTime;
                        nearestMissile = unit;
                    }
                    else
                    {
                        if (impactTime >= shortestOtherImpactTime) continue;
                        shortestOtherImpactTime = impactTime;
                        nearestOtherProjectile = unit;
                    }
                    continue;
                }
                if (distance >= missileDistance)
                    continue;

                nearestMissile = unit;
                missileDistance = distance;
            }

            if (nearestMissile != null)
                return nearestMissile;

            // The stasis beam is a control weapon rather than another damage
            // interceptor. If all missiles are already reserved by point-
            // defence, it must still stop one of them instead of falling back
            // to an enemy ship.
            if (_preferProjectilesEvenIfReserved && nearestReservedMissile != null)
                return nearestReservedMissile;

            if (_interceptAllProjectiles)
                return nearestOtherProjectile;

            return _scene.Ships.GetEnemyForTurret(_owner, position, Platform.Body.WorldRotation(), Platform.AutoAimingAngle, range);
        }

        private void SetTarget(IUnit target)
        {
            if (Platform is IUnitTargetingPlatform unitTargetingPlatform)
                unitTargetingPlatform.ActiveUnitTarget = target;
            else
                Platform.ActiveTarget = target as IShip;
        }

        private bool IsTargetInsideWeaponRange(IUnit target)
        {
            if (!target.IsActive())
                return false;

            var position = Platform.Body.WorldPosition();
            var range = Info.Range;
            return BattlefieldGeometry.SqrDistance(position, target.Body.WorldPosition()) <= range * range;
        }

        private bool HasActiveBullet => _activeBullet.IsActive();

        private bool IsInterceptableProjectile(IUnit unit)
        {
            return (_interceptAllProjectiles && unit is IBullet) ||
                   unit.Type.Class == UnitClass.Missile ||
                   unit is Combat.Component.Bullet.Bullet bullet && bullet.Controller is BallLightningController;
        }

        private bool CanTargetProjectile(IUnit unit)
        {
            // Macro-electrons are deliberately targetable by their emitter
            // and allied ships as well as by enemies; ordinary missiles retain
            // the original enemy-only point-defense rule.
            return IsMacroElectron(unit) || CombatRelations.AreEnemies(unit.Type, _owner.Type);
        }

        private bool ThreatensProtectedShip(IUnit projectile, out float impactTime)
        {
            impactTime = float.MaxValue;
            if (!_protectedShip.IsActive()) return false;

            var relativePosition = BattlefieldGeometry.Delta(_protectedShip.Body.WorldPosition(), projectile.Body.WorldPosition());
            var relativeVelocity = projectile.Body.WorldVelocity() - _protectedShip.Body.WorldVelocity();
            var speedSquared = relativeVelocity.sqrMagnitude;
            if (speedSquared < 0.01f || Vector2.Dot(relativePosition, relativeVelocity) >= 0f)
                return false;

            impactTime = -Vector2.Dot(relativePosition, relativeVelocity) / speedSquared;
            if (impactTime < 0f || impactTime > 7f) return false;
            var closestDistance = (relativePosition + relativeVelocity * impactTime).magnitude;
            return closestDistance <= Mathf.Max(5f, _protectedShip.Body.WorldScale() * 0.9f + 2f);
        }

        private static bool IsMacroElectron(IUnit unit)
        {
            return unit is Combat.Component.Bullet.Bullet bullet &&
                   bullet.Controller is BallLightningController;
        }

        private void SetReservedTarget(IUnit target)
        {
            if (_reservedTarget == target)
            {
                if (target != null)
                    InterceptionTargetCoordinator.Reserve(target, this, _owner,
                        _preferProjectilesEvenIfReserved);
                return;
            }

            InterceptionTargetCoordinator.Release(_reservedTarget, this,
                _preferProjectilesEvenIfReserved);
            _reservedTarget = target;
            if (target != null)
                InterceptionTargetCoordinator.Reserve(target, this, _owner,
                    _preferProjectilesEvenIfReserved);
        }

        private readonly IScene _scene;
        private readonly IShip _owner;
        private readonly IShip _protectedShip;
        private readonly float _energyConsumption;
        private readonly bool _interceptAllProjectiles;
        private readonly bool _preferProjectilesEvenIfReserved;
        private IBullet _activeBullet;
        private IUnit _currentTarget;
        private IUnit _reservedTarget;
    }
}
