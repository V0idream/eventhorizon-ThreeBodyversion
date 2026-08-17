using System.Collections.Generic;
using Combat.Component.Bullet;
using Combat.Component.Controller;
using Combat.Component.Ship;
using Combat.Component.Unit;
using Combat.Component.Unit.Classification;
using Combat.Scene;
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
        public static IReadOnlyList<IUnit> GetProjectileCandidates(IScene scene)
        {
            RefreshCandidateCache(scene);
            return ProjectileCandidates;
        }

        public static IReadOnlyList<IShip> GetShipCandidates(IScene scene)
        {
            RefreshCandidateCache(scene);
            return ShipCandidates;
        }

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

        private static void RefreshCandidateCache(IScene scene)
        {
            var frame = Time.frameCount;
            var fixedTime = Time.fixedTime;
            if (ReferenceEquals(scene, CachedScene) && frame == CachedFrame &&
                Mathf.Approximately(fixedTime, CachedFixedTime))
                return;

            CachedScene = scene;
            CachedFrame = frame;
            CachedFixedTime = fixedTime;
            ProjectileCandidates.Clear();
            ShipCandidates.Clear();

            if (scene == null)
                return;

            // Build one immutable-for-this-tick snapshot instead of letting
            // every installed point-defence turret lock and scan the complete
            // unit list independently. This changes the dominant cost from
            // O(interceptors * all units) to O(all units + interceptors *
            // projectiles), with no per-turret allocations.
            lock (scene.Units.LockObject)
            {
                var units = scene.Units.Items;
                for (var i = 0; i < units.Count; ++i)
                {
                    var unit = units[i];
                    if (!unit.IsActive())
                        continue;
                    if (unit is IBullet || unit.Type.Class == UnitClass.Missile ||
                        unit is Combat.Component.Bullet.Bullet { Controller: BallLightningController })
                        ProjectileCandidates.Add(unit);
                }
            }

            lock (scene.Ships.LockObject)
            {
                var ships = scene.Ships.Items;
                for (var i = 0; i < ships.Count; ++i)
                {
                    var ship = ships[i];
                    if (ship.IsActive())
                        ShipCandidates.Add(ship);
                }
            }
        }

        private const float ReservationLifetime = 0.2f;
        private static readonly Dictionary<IUnit, Reservation> Reservations = new();
        // Stasis beams coordinate independently from damage point-defence.
        // Otherwise a missile reserved by any ordinary interceptor appears
        // unavailable to every stasis beam and all stasis beams fall back to
        // the same already-reserved target.
        private static readonly Dictionary<IUnit, Reservation> ControlReservations = new();
        private static readonly List<IUnit> ProjectileCandidates = new(128);
        private static readonly List<IShip> ShipCandidates = new(64);
        private static IScene CachedScene;
        private static int CachedFrame = -1;
        private static float CachedFixedTime = float.MinValue;
    }
}
