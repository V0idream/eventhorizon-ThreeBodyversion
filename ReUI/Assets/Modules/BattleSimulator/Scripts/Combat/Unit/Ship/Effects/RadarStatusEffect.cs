using Combat.Component.Engine;
using Combat.Component.Features;
using Combat.Component.Ship;
using Combat.Component.Stats;
using Combat.Component.Systems;
using Combat.Component.Systems.Weapons;
using Combat.Component.Triggers;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Component.Ship.Effects
{
    public enum RadarStatusKind
    {
        Jammed,
        Stealthed,
    }

    public sealed class RadarStatusEffect : IShipEffect, IFeaturesModification
    {
        public RadarStatusEffect(RadarStatusKind kind, float duration, float energyDrainPerSecond = 0f,
            float maximumActiveDuration = 0f, float immunityDuration = 0f)
        {
            _kind = kind;
            _remaining = Mathf.Max(0f, duration);
            _energyDrainPerSecond = Mathf.Max(0f, energyDrainPerSecond);
            _maximumActiveDuration = Mathf.Max(0f, maximumActiveDuration);
            _immunityDuration = Mathf.Max(0f, immunityDuration);
            if (_maximumActiveDuration > 0f)
                _remaining = Mathf.Min(_remaining, _maximumActiveDuration);
        }

        public RadarStatusKind Kind => _kind;
        public bool IsAlive => _remaining > 0f || _immunityRemaining > 0f;
        public bool IsStatusActive => _remaining > 0f;
        public bool IsEmpLimited => _maximumActiveDuration > 0f;

        public void Refresh(float duration, float energyDrainPerSecond = 0f)
        {
            // Strategic effects such as the Sophon use the unrestricted path.
            // If one arrives during ordinary EMP immunity it deliberately
            // overrides that immunity instead of inheriting the EMP cap.
            _maximumActiveDuration = 0f;
            _immunityDuration = 0f;
            _immunityRemaining = 0f;
            _activeDuration = 0f;
            _remaining = Mathf.Max(_remaining, Mathf.Max(0f, duration));
            _energyDrainPerSecond = Mathf.Max(_energyDrainPerSecond, Mathf.Max(0f, energyDrainPerSecond));
        }

        public bool TryRefreshEmp(float duration, float energyDrainPerSecond)
        {
            if (!IsEmpLimited || _immunityRemaining > 0f)
                return false;

            var available = Mathf.Max(0f, _maximumActiveDuration - _activeDuration);
            if (available <= 0f)
                return false;

            _remaining = Mathf.Min(Mathf.Max(_remaining, Mathf.Max(0f, duration)), available);
            _energyDrainPerSecond = Mathf.Max(_energyDrainPerSecond, Mathf.Max(0f, energyDrainPerSecond));
            return _remaining > 0f;
        }

        public void UpdatePhysics(IShip ship, float elapsedTime)
        {
            var deltaTime = Mathf.Max(0f, elapsedTime);
            if (_remaining > 0f)
            {
                var available = IsEmpLimited
                    ? Mathf.Max(0f, _maximumActiveDuration - _activeDuration)
                    : _remaining;
                var appliedTime = Mathf.Min(_remaining, Mathf.Min(deltaTime, available));
                _remaining -= appliedTime;
                _activeDuration += appliedTime;

                if (_kind == RadarStatusKind.Jammed && _energyDrainPerSecond > 0f)
                    ship.Stats.Energy.Get(_energyDrainPerSecond * appliedTime);

                if (IsEmpLimited && (_remaining <= 0f || _activeDuration >= _maximumActiveDuration))
                {
                    _remaining = 0f;
                    _immunityRemaining = _immunityDuration;
                }
                return;
            }

            if (_immunityRemaining > 0f)
                _immunityRemaining = Mathf.Max(0f, _immunityRemaining - deltaTime);
        }

        public void UpdateView(IShip ship, float elapsedTime) { }
        public void Dispose() { }

        public bool TryApplyModification(ref FeaturesData data)
        {
            if (!IsStatusActive)
                return false;

            if (_kind == RadarStatusKind.Stealthed)
                data.TargetPriority = TargetPriority.None;

            return true;
        }

        public IEngineModification EngineModification => null;
        public IFeaturesModification FeaturesModification => _kind == RadarStatusKind.Stealthed ? this : null;
        public ISystemsModification SystemsModification => null;
        public IStatsModification StatsModification => null;
        public IUnitAction UnitAction => null;

        private readonly RadarStatusKind _kind;
        private float _remaining;
        private float _energyDrainPerSecond;
        private float _maximumActiveDuration;
        private float _immunityDuration;
        private float _activeDuration;
        private float _immunityRemaining;
    }

    public static class RadarStatus
    {
        public static bool IsJammed(IShip ship) => HasStatus(ship, RadarStatusKind.Jammed);
        public static bool IsStealthed(IShip ship) => HasStatus(ship, RadarStatusKind.Stealthed);
        public static bool IsStealthedFrom(IShip target, IShip observer)
        {
            if (!IsStealthed(target))
                return false;

            return observer == null || !HasStealthReveal(observer.Type.Side);
        }

        public static bool CanDetect(IShip observer, IShip target)
        {
            return observer != null && target != null && !IsJammed(observer) &&
                   !SmallUniverseTransitEffect.IsInTransit(target) && !IsStealthedFrom(target, observer);
        }

        public static void ApplyJammed(IShip ship, float duration, float energyDrainPerSecond)
        {
            Apply(ship, RadarStatusKind.Jammed, duration, energyDrainPerSecond);
            ClearTargeting(ship);
        }

        public static bool TryApplyEmpJammed(IShip ship, float duration, float energyDrainPerSecond)
        {
            if (ship == null || ship.Effects == null || duration <= 0f)
                return false;

            foreach (var effect in ship.Effects.All)
            {
                if (effect is not RadarStatusEffect status || status.Kind != RadarStatusKind.Jammed)
                    continue;

                // An unrestricted strategic disruption already jams the ship;
                // ordinary EMP may still deliver its immediate energy effect.
                if (!status.IsEmpLimited)
                {
                    ClearTargeting(ship);
                    return true;
                }

                if (!status.TryRefreshEmp(duration, energyDrainPerSecond))
                    return false;

                ClearTargeting(ship);
                return true;
            }

            ship.AddEffect(new RadarStatusEffect(
                RadarStatusKind.Jammed,
                duration,
                energyDrainPerSecond,
                EmpMaximumActiveDuration,
                EmpImmunityDuration));
            ClearTargeting(ship);
            return true;
        }

        public static void ApplyStealth(IShip ship, float duration)
        {
            Apply(ship, RadarStatusKind.Stealthed, duration, 0f);
        }

        public static void RevealStealthFor(UnitSide observerSide, float duration)
        {
            if (duration <= 0f)
                return;

            var until = Time.time + duration;
            if (_stealthRevealUntil.TryGetValue(observerSide, out var current) && current > until)
                return;

            _stealthRevealUntil[observerSide] = until;
        }

        private static bool HasStealthReveal(UnitSide observerSide)
        {
            if (!_stealthRevealUntil.TryGetValue(observerSide, out var until))
                return false;

            if (until > Time.time)
                return true;

            _stealthRevealUntil.Remove(observerSide);
            return false;
        }

        private static bool HasStatus(IShip ship, RadarStatusKind kind)
        {
            if (ship == null || ship.Effects == null)
                return false;

            foreach (var effect in ship.Effects.All)
                if (effect is RadarStatusEffect status && status.Kind == kind && status.IsStatusActive)
                    return true;

            return false;
        }

        private static void Apply(IShip ship, RadarStatusKind kind, float duration, float energyDrainPerSecond)
        {
            if (ship == null || ship.Effects == null || duration <= 0f)
                return;

            foreach (var effect in ship.Effects.All)
            {
                if (effect is not RadarStatusEffect status || status.Kind != kind)
                    continue;

                status.Refresh(duration, energyDrainPerSecond);
                return;
            }

            ship.AddEffect(new RadarStatusEffect(kind, duration, energyDrainPerSecond));
        }

        private static void ClearTargeting(IShip ship)
        {
            if (ship == null || ship.Systems == null)
                return;

            var systems = ship.Systems.All;
            for (int i = 0; i < systems.Count; i++)
            {
                if (systems[i] is not IWeapon weapon)
                    continue;

                weapon.Platform.ActiveTarget = null;
                if (ship.Controls != null)
                    ship.Controls.Systems.SetState(i, false);
            }
        }

        private static readonly Dictionary<UnitSide, float> _stealthRevealUntil = new();
        private const float EmpMaximumActiveDuration = 10f;
        private const float EmpImmunityDuration = 30f;
    }
}
