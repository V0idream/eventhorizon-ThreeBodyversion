using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReUI.Editor
{
    internal static class ReUIPerformanceValidation
    {
        [MenuItem("Tools/ReUI/Validate Beta8.30 Performance")]
        public static void Validate()
        {
            var coordinator = Read(
                "Modules/BattleSimulator/Scripts/Combat/Component/Systems/Weapons/InterceptionTargetCoordinator.cs");
            Require(coordinator, "GetProjectileCandidates", "GetShipCandidates", "CachedFixedTime",
                "ProjectileCandidates.Clear()", "ShipCandidates.Clear()");

            var laser = Read(
                "Modules/BattleSimulator/Scripts/Combat/Component/Systems/Weapons/AutoPointDefenseLaser.cs");
            Require(laser, "GetProjectileCandidates(_scene)");
            Reject(laser, "lock (_scene.Units.LockObject)");

            var cannon = Read(
                "Modules/BattleSimulator/Scripts/Combat/Component/Systems/Weapons/AutoPointDefenseCannon.cs");
            Require(cannon, "GetProjectileCandidates(_scene)");
            Reject(cannon, "lock (_scene.Units.LockObject)");

            var drones = Read("Modules/BattleSimulator/Scripts/Combat/Factory/EdgeDroneRuntime.cs");
            Require(drones, "GetShipCandidates(scene)", "GetProjectileCandidates(scene)");
            Reject(drones, "lock (scene.Units.LockObject)", "lock (scene.Ships.LockObject)");

            var rift = Read(
                "Modules/BattleSimulator/Scripts/Combat/Component/Systems/Devices/TimeRiftField.cs");
            Require(rift, "CollisionInterval = 0.05f", "_collisionAccumulator < CollisionInterval");

            var bootstrap = Read("ReUI/Runtime/AndroidPerformanceBootstrap.cs");
            Require(bootstrap, "ConfigureUnityJobWorkers", "sched_setaffinity", "FindPerformanceCoreMask",
                "setThreadPriority", "selected >= 2 ? mask : 0UL");

            Debug.Log(
                "[Beta8.30 Performance Validation] projectileSnapshotCache=true, " +
                "shipSnapshotCache=true, pointDefenseFullScansRemoved=true, " +
                "droneFullScansRemoved=true, timeRift20Hz=true, " +
                "jobWorkersConfigured=true, androidMainThreadAffinity=best-effort");
        }

        private static string Read(string relativePath) =>
            File.ReadAllText(Path.Combine(Application.dataPath, relativePath));

        private static void Require(string source, params string[] tokens)
        {
            foreach (var token in tokens)
                if (!source.Contains(token))
                    throw new InvalidOperationException("Missing performance implementation token: " + token);
        }

        private static void Reject(string source, params string[] tokens)
        {
            foreach (var token in tokens)
                if (source.Contains(token))
                    throw new InvalidOperationException("Legacy performance hotspot remains: " + token);
        }
    }
}
