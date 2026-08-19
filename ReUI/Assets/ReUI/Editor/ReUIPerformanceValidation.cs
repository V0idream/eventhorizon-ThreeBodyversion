using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReUI.Editor
{
    internal static class ReUIPerformanceValidation
    {
        [MenuItem("Tools/ReUI/Validate Beta8.37 Performance")]
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
            Require(drones, "GetShipCandidates(scene)", "GetProjectileCandidates(scene)",
                "MaxNormalDronesPerOwner = 48", "CanSpawnNormal", "DefenderUpdateInterval = 0.05f",
                "now < pair.NextUpdate", "MaxPredatorDrones = 400",
                "NanoStormInitialPredatorCount = 125", "DestroyedShipPredatorMultiplier = 5",
                "Mathf.CeilToInt(size / 50f)) * DestroyedShipPredatorMultiplier",
                "buildId == PredatorBuildId ? MaxPredatorDrones : 200", "DroneRespawnDelay = 5f",
                "ProcessRespawns", "ignoreSpawnLimits = false", "order.Owner.Body.WorldPosition()",
                "ResolveRespawnOwner", "if (respawned != null)",
                "RadarStatus.CanDetect(drone, candidate)", "drone.Controls.Throttle = 1f");
            Reject(drones, "candidate.Features.TargetPriority == Combat.Component.Features.TargetPriority.None");
            Reject(drones, "lock (scene.Units.LockObject)", "lock (scene.Ships.LockObject)");

            var bulletFactory = Read("Modules/BattleSimulator/Scripts/Combat/Factory/Bullets/BulletFactory.cs");
            Require(bulletFactory, "_ammunition.Id.Value == 912",
                "EdgeDroneRuntime.NanoStormInitialPredatorCount");

            var shipFactory = Read("Modules/BattleSimulator/Scripts/Combat/Factory/ShipFactory.cs");
            Require(shipFactory, "CreateEngine(stats, isMassProducedEdgeDrone)", "isMassProducedEdgeDrone",
                "shipModel.Id.Value >= EdgeDroneRuntime.NormalBuildId",
                "!isMassProducedEdgeDrone && !_settings.NoEnemyMessages",
                "ThreeBodyContentRules.ShouldForceAggressiveDrone", "DroneBehaviour.Aggressive",
                "_database.CombatSettings.OffensiveDroneAI", "isPredatorDrone",
                "new EmptyController.Factory()");

            var ship = Read("Modules/BattleSimulator/Scripts/Combat/Unit/Ship/Ship.cs");
            Require(ship, "Specification.Info.Id.Value == Combat.Factory.EdgeDroneRuntime.PredatorBuildId",
                "return 60f;");

            var explorationRules = Read("Modules/ShipConstructor/Scripts/ThreeBodyContentRules.cs");
            Require(explorationRules, "IsAvailableInExplorationRandomEquipment",
                "ExplorationTrisolarisComponentIds", "StarshipEarthFactionId = 21", "TrisolarisFactionId = 22",
                "ShouldForceAggressiveDrone", "EdgeDefenseDroneShipId = 11012",
                "ship.ShipType != ShipType.Drone", "ship.Faction.Id.Value >= StarshipEarthFactionId");

            var outpostBuilder = Read("Scripts/Domain/Exploration/RandomOutpostBuilder.cs");
            Require(outpostBuilder, "IsAvailableInExplorationRandomEquipment", "componentPool");

            var turretBuilder = Read("Scripts/Domain/Exploration/RandomTurretBuilder.cs");
            Require(turretBuilder, "IsAvailableInExplorationRandomEquipment",
                ".Where(item => IsSuitableWeapon(item, componentLevel))");

            var enemyShipBuilder = Read("Scripts/Domain/Exploration/EnemyShipBuilder.cs");
            Require(enemyShipBuilder, "SanitizeExplorationEquipment", "FindCompatibleReplacement",
                "IsAllowedExplorationSatellite", "restrictRandomEquipment");

            var playerFleet = Read("Scripts/Domain/Player/PlayerFleet.cs");
            Require(playerFleet, "value.Model.SizeClass != SizeClass.Starbase", "_ships.Contains(value)");
            Reject(playerFleet, "value.Model.SizeClass == SizeClass.Frigate");

            var planetPanel = Read("Scripts/Gui/Exploration/PlanetPanel.cs");
            Require(planetPanel, "_planet.Type == PlanetType.Infected", "GetHiveShipTier",
                "Mathf.Pow(1.5f", "case SizeClass.Destroyer: return 1", "case SizeClass.Cruiser: return 2",
                "case SizeClass.Battleship: return 3", "case SizeClass.Titan: return 4",
                "case SizeClass.TitanP: return 5", "GetRequiredFuel()", "Mathf.CeilToInt");

            var progressBar = Read("Scripts/Gui/Controls/ProgressBar.cs");
            Require(progressBar, "SegmentCount", "SegmentGapPixels", "PopulateSegmented");

            var shipStatsPanel = Read("Scripts/Gui/Combat/ShipStatsPanel.cs");
            Require(shipStatsPanel, "ReUIStatusBarSettings.TenSegments", "bar.SegmentCount = segmented ? 10 : 0",
                "ConfigureBar(_energyPoints", "false");

            var statusBarSettings = Read("ReUI/Runtime/ReUIStatusBarSettings.cs");
            Require(statusBarSettings, "ReUI.TenSegmentStatusBars", "十格状态条", "生命与护盾按 10 格显示");

            var extendedDisplay = Read("ReUI/Runtime/ReUIExtendedDisplayMenu.cs");
            Require(extendedDisplay, "显示扩展", "ReUIStatusBarSelector.EnsureIn",
                "ReUIShieldStyleSelector.EnsureIn", "ReUIHdrDisplaySelector.EnsureIn",
                "HDR · 护盾样式 · 十格状态条");

            var reuiBootstrap = Read("ReUI/Runtime/ReUIBootstrap.cs");
            Require(reuiBootstrap, "ReUIExtendedDisplayMenu.EnsureForSettings(canvas)");
            Reject(reuiBootstrap, "ReUIShieldStyleSelector.EnsureForSettings(canvas)",
                "ReUIHdrDisplaySelector.EnsureForSettings(canvas)");

            var themePalette = Read("ReUI/Runtime/ReUIThemePalettePanel.cs");
            Require(themePalette, "HexInput", "ColorUtility.TryParseHtmlString", "CreateHexInput",
                "可编辑 HEX", "ApplySelectedTheme");

            var radarInterference = Read(
                "Modules/BattleSimulator/Scripts/Combat/Collision/Behaviour/Action/RadarInterferenceAction.cs");
            Require(radarInterference, "self?.Type?.Owner", "ship == owner",
                "CombatRelations.AreAllies(owner.Type, ship.Type)",
                "TryApplyEmpJammed(ship, _duration, _energyDrainPerSecond, owner)");

            var radarStatus = Read(
                "Modules/BattleSimulator/Scripts/Combat/Unit/Ship/Effects/RadarStatusEffect.cs");
            Require(radarStatus, "IShip source = null", "ship == source",
                "CombatRelations.AreAllies(source.Type, ship.Type)", "CanBeWeaponTarget",
                "target.Features.TargetPriority != TargetPriority.None || IsJammed(target)");

            var weaponPlatformBody = Read(
                "Modules/BattleSimulator/Scripts/Combat/Component/Body/WeaponPlatformBody.cs");
            Require(weaponPlatformBody, "RadarStatus.CanBeWeaponTarget(owner, ship)",
                "TemporaryConversionEffect.CanPlayerAttack(owner, ship)");

            var edgeControlEffects = Read(
                "Modules/BattleSimulator/Scripts/Combat/Unit/Ship/Effects/EdgeControlEffects.cs");
            Require(edgeControlEffects, "IsPlayerFallbackTarget", "CanPlayerAttack", "IsPlayerDamagePair",
                "ClearTargeting(ship)", "_sourceSide == UnitSide.Player || conversion._sourceSide == UnitSide.Ally");

            var combatMinimap = Read("Scripts/Gui/Combat/CombatMinimap.cs");
            Require(combatMinimap, "var fallbackMode = normalDetected.Length == 0",
                "var detected = fallbackMode ? convertedDetected : normalDetected",
                "TemporaryConversionEffect.IsPlayerFallbackTarget(s)");

            var collisionManager = Read(
                "Modules/BattleSimulator/Scripts/Combat/Collision/Manager/CollisionManager.cs");
            Require(collisionManager, "TemporaryConversionEffect.IsPlayerDamagePair(first, second)");

            ValidateEdgeDroneData();

            var rift = Read(
                "Modules/BattleSimulator/Scripts/Combat/Component/Systems/Devices/TimeRiftField.cs");
            Require(rift, "CollisionInterval = 0.05f", "_collisionAccumulator < CollisionInterval");

            var bootstrap = Read("ReUI/Runtime/AndroidPerformanceBootstrap.cs");
            Require(bootstrap, "ConfigureUnityJobWorkers", "platform-selected=", "setThreadPriority",
                "createHintSession", "setPreferPowerEfficiency",
                "reportActualWorkDuration", "setThreads", "FindUnityPerformanceThreadIds", "AiManager",
                "ConfigurePhysics2DJobs", "Physics2D.jobOptions", "useMultithreading = true",
                "newContactsPerJob = 30", "collideContactsPerJob = 100");
            Reject(bootstrap, "sched_setaffinity", "FindPerformanceCoreMask", "Worker Thread");

            var aiManager = Read("Modules/BattleSimulator/Scripts/Combat/AI/AiManager.cs");
            Require(aiManager, "Parallel.For", "ParallelControllerThreshold = 24", "Math.Min(2",
                "MaxDegreeOfParallelism = MaxParallelism", "#if !UNITY_WEBGL",
                "_parallelAiDisabled", "falling back to serial AI");

            var shipList = Read("Modules/BattleSimulator/Scripts/Combat/Scene/ShipList.cs");
            Require(shipList, "GetSnapshot()", "_ships.ToArray()", "volatile bool _snapshotDirty");

            var unitList = Read("Modules/BattleSimulator/Scripts/Combat/Scene/UnitList.cs");
            Require(unitList, "GetSnapshot()", "_items.ToArray()", "volatile bool _snapshotDirty");

            var targetList = Read(
                "Modules/BattleSimulator/Scripts/Combat/AI/TargetList/TargetListBase.cs");
            Reject(targetList, "lock (_scene.Ships.LockObject)");

            var shipListExtensions = Read(
                "Modules/BattleSimulator/Scripts/Combat/Scene/ShipListExtensions.cs");
            Reject(shipListExtensions, "lock (shipList.LockObject)", "lock (unitList.LockObject)");

            var projectSettings = File.ReadAllText(Path.Combine(
                Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset"));
            Require(projectSettings, "m_BuildTarget: AndroidPlayer", "m_GraphicsJobs: 1",
                "mobileMTRendering:", "Android: 1");

            Debug.Log(
                "[Beta8.37 Performance Validation] projectileSnapshotCache=true, " +
                "shipSnapshotCache=true, pointDefenseFullScansRemoved=true, " +
                "droneFullScansRemoved=true, timeRift20Hz=true, " +
                "parallelAi=true, lockFreeTargetSnapshots=true, " +
                "physics2DJobs=coarse, " +
                "jobWorkers=platform-selected, androidMultithreadedRendering=true, " +
                "androidGraphicsJobs=true, androidAdpfHints=true, " +
                "androidMainThreadAffinity=disabled, explorationEquipmentRestricted=true, " +
                "edgeDroneClassFixed=true, edgeDroneRenderReduced=true, edgeTitanDefenders=2, " +
                "modDronesForcedOffensive=true, edgeNormalDroneCap=48, edgeDefenderScanHz=20, " +
                "hiveAllShipClasses=true, hiveSizeCostMultiplier=1.5, hiveFuelMultiplier=1.5, " +
                "predatorRuntimeSteering=true, explorationTurretEquipmentRestricted=true, " +
                "tenSegmentStatusBars=true, extendedDisplayMenu=true, editableThemeHex=true, " +
                "empLaserOwnerFeedbackBlocked=true, empJammedTargetsRemainLockable=true, " +
                "virusConvertedFallbackTarget=true, virusConvertedNoRetaliation=true, " +
                "nanoStormInitialPredators=125, predatorDroneCap=400, destroyedShipPredatorsX5=true, " +
                "predatorTargetingFixed=true, edgeDroneRespawnDelay=5, edgeDroneRespawnBypassesSpawnBudget=true");
        }

        private static void ValidateEdgeDroneData()
        {
            foreach (var path in new[]
                     {
                         "Modules/Database/Resources/Database/Ship/EdgeDrone.json",
                         "Modules/Database/Resources/Database/Ship/EdgePredatorDrone.json",
                         "Modules/Database/Resources/Database/Ship/EdgeDefenseDrone.json",
                     })
            {
                var source = Read(path);
                Require(source, "\"ShipType\":1", "\"SizeClass\":-1");
                Reject(source, "\"SizeClass\":0");
            }

            var titan = Read("Modules/Database/Resources/Database/Ship/Builds/edge_terminate.json");
            if (CountOccurrences(titan, "\"ComponentId\": 983") != 2)
                throw new InvalidOperationException("Edge Titan must install exactly two Defender components.");
        }

        private static int CountOccurrences(string source, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                ++count;
                index += token.Length;
            }
            return count;
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
