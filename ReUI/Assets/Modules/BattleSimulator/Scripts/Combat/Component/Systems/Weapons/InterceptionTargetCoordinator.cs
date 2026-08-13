using System.Collections.Generic;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Unit;
using UnityEngine;

namespace Combat.Component.Systems.Weapons
{
    /// <summary>
    /// Coordinates autonomous interception weapons so several systems on the
    /// same side do not waste their fire on one projectile while other threats
    /// remain untouched.
    /// </summary>
    internal static class InterceptionTargetCoordinator
    {
        public static bool IsReservedByOther(IUnit target, object owner, IUnit interceptor,
            bool controlChannel = false)
        {
            if (target == null) return false;
            var reservations = controlChannel ? ControlReservations : Reservations;
            if (!reservations.TryGetValue(target, out var reservation)) return false;
            if (!target.IsActive() || reservation.ExpiresAt <= Time.time)
            {
                reservations.Remove(target);
                return false;
            }
            if (ReferenceEquals(reservation.Owner, owner)) return false;
            // Opposing fleets coordinate independently. This matters for
            // special projectiles that are intentionally targetable by both
            // their emitter and its enemies.
            return reservation.Interceptor == null || interceptor == null ||
                   !CombatRelations.AreEnemies(reservation.Interceptor.Type, interceptor.Type);
        }

        public static void Reserve(IUnit target, object owner, IUnit interceptor,
            bool controlChannel = false)
        {
            if (target == null || owner == null) return;
            var reservations = controlChannel ? ControlReservations : Reservations;
            reservations[target] = new Reservation(owner, interceptor, Time.time + ReservationLifetime);
        }

        public static void Release(IUnit target, object owner, bool controlChannel = false)
        {
            if (target == null || owner == null) return;
            var reservations = controlChannel ? ControlReservations : Reservations;
            if (reservations.TryGetValue(target, out var reservation) &&
                ReferenceEquals(reservation.Owner, owner))
                reservations.Remove(target);
        }

        private readonly struct Reservation
        {
            public Reservation(object owner, IUnit interceptor, float expiresAt)
            {
                Owner = owner;
                Interceptor = interceptor;
                ExpiresAt = expiresAt;
            }

            public object Owner { get; }
            public IUnit Interceptor { get; }
            public float ExpiresAt { get; }
        }

        private const float ReservationLifetime = 0.2f;
        private static readonly Dictionary<IUnit, Reservation> Reservations = new();
        // Stasis beams coordinate independently from damage point-defence.
        // Otherwise a missile reserved by any ordinary interceptor appears
        // unavailable to every stasis beam and all stasis beams fall back to
        // the same already-reserved target.
        private static readonly Dictionary<IUnit, Reservation> ControlReservations = new();
    }
}
