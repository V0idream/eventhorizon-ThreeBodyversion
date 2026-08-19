using System.Collections.Generic;
using Combat.Component.Body;
using Combat.Component.Engine;
using Combat.Component.Features;
using Combat.Component.Stats;
using Combat.Component.Systems;
using Combat.Component.Systems.Weapons;
using Combat.Component.Triggers;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Unit;
using UnityEngine;

namespace Combat.Component.Ship.Effects
{
    public sealed class TemporaryConversionEffect : IShipEffect
    {
        public TemporaryConversionEffect(IShip ship, UnitType source, float duration)
        {
            _ship = ship;
            _remaining = duration;
            _sourceSide = source.Side;
            _oldSide = ship.Type.SideOverride;
            _oldFaction = ship.Type.FactionOverride;
            ship.Type.SideOverride = source.Side;
            ship.Type.FactionOverride = source.FactionId;
            ClearTargeting(ship);
        }

        public static bool IsPlayerFallbackTarget(IShip target)
        {
            if (target?.Effects == null || !target.IsActive())
                return false;

            foreach (var effect in target.Effects.All)
            {
                if (effect is not TemporaryConversionEffect conversion || !conversion.IsAlive)
                    continue;
                if (conversion._sourceSide == UnitSide.Player || conversion._sourceSide == UnitSide.Ally)
                    return true;
            }

            return false;
        }

        public static bool CanPlayerAttack(IUnit attacker, IShip target)
        {
            var attackerShip = attacker as IShip ?? attacker?.Type?.Owner;
            return attackerShip != null && attackerShip.IsActive() &&
                   attackerShip.Type.Side == UnitSide.Player &&
                   IsPlayerFallbackTarget(target);
        }

        public static bool IsPlayerDamagePair(IUnit first, IUnit second)
        {
            return second is IShip secondShip && CanPlayerAttack(first, secondShip) ||
                   first is IShip firstShip && CanPlayerAttack(second, firstShip);
        }

        public bool IsAlive => _remaining > 0f && _ship.IsActive();
        public void UpdatePhysics(IShip ship, float elapsedTime) => _remaining -= elapsedTime;
        public void UpdateView(IShip ship, float elapsedTime) { }
        public void Dispose()
        {
            if (_ship?.Type == null) return;
            _ship.Type.SideOverride = _oldSide;
            _ship.Type.FactionOverride = _oldFaction;
        }
        public IEngineModification EngineModification => null;
        public IFeaturesModification FeaturesModification => null;
        public ISystemsModification SystemsModification => null;
        public IStatsModification StatsModification => null;
        public IUnitAction UnitAction => null;

        private static void ClearTargeting(IShip ship)
        {
            if (ship?.Systems == null) return;
            var systems = ship.Systems.All;
            for (var i = 0; i < systems.Count; i++)
            {
                if (systems[i] is not IWeapon weapon) continue;
                weapon.Platform.ActiveTarget = null;
                systems[i].Active = false;
                ship.Controls?.Systems.SetState(i, false);
            }
        }

        private readonly IShip _ship;
        private readonly UnitSide _sourceSide;
        private readonly UnitSide? _oldSide;
        private readonly int? _oldFaction;
        private float _remaining;
    }

    public sealed class StasisEffect : IShipEffect, IEngineModification
    {
        public StasisEffect(float duration) => _remaining = duration;

        public static void Apply(IShip ship, float duration)
        {
            if (ship == null || duration <= 0f)
                return;

            foreach (var effect in ship.Effects.All)
            {
                if (effect is not StasisEffect stasis)
                    continue;

                stasis._remaining = Mathf.Max(stasis._remaining, duration);
                stasis.ClampToModifiedLimits(ship);
                return;
            }

            var newEffect = new StasisEffect(duration);
            ship.AddEffect(newEffect);
            newEffect.ApplyInitialSlowdown(ship);
        }

        public bool IsAlive => _remaining > 0f;
        public void UpdatePhysics(IShip ship, float elapsedTime)
        {
            _remaining -= elapsedTime;
            if (IsAlive)
                ClampToModifiedLimits(ship);
        }
        public void UpdateView(IShip ship, float elapsedTime) { }
        public void Dispose() { }
        public bool TryApplyModification(ref EngineData data)
        {
            if (!IsAlive) return false;
            data.Velocity *= 0.2f;
            data.AngularVelocity *= 0.2f;
            data.Propulsion *= 0.2f;
            data.TurnRate *= 0.2f;
            return true;
        }
        public IEngineModification EngineModification => this;
        public IFeaturesModification FeaturesModification => null;
        public ISystemsModification SystemsModification => null;
        public IStatsModification StatsModification => null;
        public IUnitAction UnitAction => null;

        private void ApplyInitialSlowdown(IShip ship)
        {
            if (ship?.Body == null)
                return;

            SetVelocity(ship.Body, ship.Body.Velocity * VelocityMultiplier);
            SetAngularVelocity(ship.Body, ship.Body.AngularVelocity * VelocityMultiplier);
        }

        private void ClampToModifiedLimits(IShip ship)
        {
            if (ship?.Body == null || ship.Engine == null)
                return;

            var body = ship.Body;
            var velocity = body.Velocity;
            var maximumSpeed = Mathf.Max(0f, ship.Engine.MaxVelocity);
            if (maximumSpeed > 0f && velocity.sqrMagnitude > maximumSpeed * maximumSpeed)
                SetVelocity(body, velocity.normalized * maximumSpeed);

            var maximumAngularSpeed = Mathf.Max(0f, ship.Engine.MaxAngularVelocity);
            if (maximumAngularSpeed > 0f && Mathf.Abs(body.AngularVelocity) > maximumAngularSpeed)
                SetAngularVelocity(body, Mathf.Clamp(body.AngularVelocity,
                    -maximumAngularSpeed, maximumAngularSpeed));
        }

        private static void SetVelocity(IBody body, Vector2 velocity)
        {
            if (body is RigidBodyAdapter rigidBody)
                rigidBody.Velocity = velocity;
            else
                body.ApplyAcceleration(velocity - body.Velocity);
        }

        private static void SetAngularVelocity(IBody body, float angularVelocity)
        {
            if (body is RigidBodyAdapter rigidBody)
                rigidBody.AngularVelocity = angularVelocity;
            else
                body.ApplyAngularAcceleration(angularVelocity - body.AngularVelocity);
        }

        private float _remaining;
        private const float VelocityMultiplier = 0.2f;
    }

    public sealed class FirewallCollapseEffect : IShipEffect
    {
        public FirewallCollapseEffect(IShip ship, UnitType caster, float duration, int group)
        {
            _ship = ship;
            _remaining = duration;
            _token = group;
            Active[ship.Type] = new Entry(_token, caster.Side, caster.FactionId, ship);
            ClearTargeting(ship);
        }

        public static bool TryGetForcedTarget(IUnit attacker, out IShip target)
        {
            target = null;
            var attackerShip = attacker as IShip ?? attacker?.Type?.Owner;
            if (attackerShip == null || !attackerShip.IsActive() ||
                !TryGetEntry(attackerShip.Type, out var sourceEntry))
                return false;

            var bestDistance = float.PositiveInfinity;
            foreach (var item in Active.Values)
            {
                var candidate = item.Ship;
                if (item.Token != sourceEntry.Token || candidate == attackerShip ||
                    candidate == null || !candidate.IsActive())
                    continue;

                var distance = (candidate.Body.WorldPosition() - attackerShip.Body.WorldPosition()).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                target = candidate;
            }

            return target != null;
        }

        public static bool TryResolve(UnitType first, UnitType second, out bool allies)
        {
            var hasFirst = TryGetEntry(first, out var a);
            var hasSecond = TryGetEntry(second, out var b);
            if (hasFirst && hasSecond)
            {
                allies = a.Token != b.Token;
                return true;
            }
            if (hasFirst && IsProtectedType(second, a))
            {
                allies = true;
                return true;
            }
            if (hasSecond && IsProtectedType(first, b))
            {
                allies = true;
                return true;
            }
            allies = false;
            return false;
        }

        public static bool IsProtectedPair(UnitType first, UnitType second)
        {
            if (!TryResolve(first, second, out var allies) || !allies) return false;
            return TryGetEntry(first, out _) || TryGetEntry(second, out _);
        }

        public bool IsAlive => _remaining > 0f && _ship.IsActive();
        public static int NewGroup() => ++_nextToken;
        public void UpdatePhysics(IShip ship, float elapsedTime) => _remaining -= elapsedTime;
        public void UpdateView(IShip ship, float elapsedTime) { }
        public void Dispose()
        {
            if (_ship?.Type != null && Active.TryGetValue(_ship.Type, out var entry) && entry.Token == _token)
                Active.Remove(_ship.Type);
        }
        public IEngineModification EngineModification => null;
        public IFeaturesModification FeaturesModification => null;
        public ISystemsModification SystemsModification => null;
        public IStatsModification StatsModification => null;
        public IUnitAction UnitAction => null;

        private static bool TryGetEntry(UnitType type, out Entry entry)
        {
            if (type != null && Active.TryGetValue(type, out entry)) return true;
            var ownerType = type?.Owner?.Type;
            if (ownerType != null && Active.TryGetValue(ownerType, out entry)) return true;
            entry = default;
            return false;
        }

        private static bool IsProtectedType(UnitType type, Entry entry)
        {
            if (type == null) return false;
            if (type.FactionId == entry.ProtectedFaction) return true;
            if (type.Side == entry.ProtectedSide) return true;
            return entry.ProtectedSide == UnitSide.Player && type.Side == UnitSide.Ally ||
                   entry.ProtectedSide == UnitSide.Ally && type.Side == UnitSide.Player;
        }

        private static void ClearTargeting(IShip ship)
        {
            if (ship?.Systems == null) return;
            var systems = ship.Systems.All;
            for (var i = 0; i < systems.Count; i++)
            {
                if (systems[i] is not IWeapon weapon) continue;
                weapon.Platform.ActiveTarget = null;
                systems[i].Active = false;
                ship.Controls?.Systems.SetState(i, false);
            }
        }

        private readonly struct Entry
        {
            public Entry(int token, UnitSide side, int faction, IShip ship)
            {
                Token = token;
                ProtectedSide = side;
                ProtectedFaction = faction;
                Ship = ship;
            }
            public int Token { get; }
            public UnitSide ProtectedSide { get; }
            public int ProtectedFaction { get; }
            public IShip Ship { get; }
        }
        private static readonly Dictionary<UnitType, Entry> Active = new();
        private static int _nextToken;
        private readonly IShip _ship;
        private readonly int _token;
        private float _remaining;
    }
}
