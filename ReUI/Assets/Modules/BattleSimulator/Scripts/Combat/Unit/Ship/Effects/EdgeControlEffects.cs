using System.Collections.Generic;
using Combat.Component.Engine;
using Combat.Component.Features;
using Combat.Component.Stats;
using Combat.Component.Systems;
using Combat.Component.Systems.Weapons;
using Combat.Component.Triggers;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Unit;

namespace Combat.Component.Ship.Effects
{
    public sealed class TemporaryConversionEffect : IShipEffect
    {
        public TemporaryConversionEffect(IShip ship, UnitType source, float duration)
        {
            _ship = ship;
            _remaining = duration;
            _oldSide = ship.Type.SideOverride;
            _oldFaction = ship.Type.FactionOverride;
            ship.Type.SideOverride = source.Side;
            ship.Type.FactionOverride = source.FactionId;
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
        private readonly IShip _ship;
        private readonly UnitSide? _oldSide;
        private readonly int? _oldFaction;
        private float _remaining;
    }

    public sealed class StasisEffect : IShipEffect, IEngineModification
    {
        public StasisEffect(float duration) => _remaining = duration;
        public bool IsAlive => _remaining > 0f;
        public void UpdatePhysics(IShip ship, float elapsedTime) => _remaining -= elapsedTime;
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
        private float _remaining;
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
