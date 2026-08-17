using System.Collections.Generic;
using System.Linq;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Model;
using DatabaseComponent = GameDatabase.DataModel.Component;
using UnityEngine;

namespace Constructor
{
    /// <summary>
    /// Content that is intentionally developer/quest/technology-only. Keeping
    /// the exclusion in one place prevents strategic content from leaking into
    /// random shops or exploration rewards through a new code path.
    /// </summary>
    public static class ThreeBodyContentRules
    {
        public const int CreativeWorkshopComponentId = 936;
        public const int ObserverCoreComponentId = 937;
        public const int ObserverShipId = 167;
        public const int ObserverShipBuildId = 418;
        public const int SilentCoreComponentId = 955;
        public const int DeflectionShieldComponentId = 956;
        public const int AngelShieldComponentId = 957;
        public const int SubspaceShieldComponentId = 958;
        public const int ElectronicShieldComponentId = 959;
        public const int SmallUniverseEntranceComponentId = 965;
        public const int TimeRiftGeneratorComponentId = 966;
        public const int EdgeFirewallCollapseComponentId = 980;
        public const int EdgeDroneHiveComponentId = 982;
        public const int EdgeDefenderComponentId = 983;
        public const int StasisFieldComponentId = 986;
        public const int WanNianFengXueShipId = 94009;
        public const int WanNianFengXueBuildId = 94009;
        public const int RestrictedTradePrice = int.MaxValue;

        private static readonly HashSet<int> RestrictedComponentIds = new()
        {
            // Internal faction markers. They are database helpers rather than
            // equipment and must never become random goods.
            288, 289, 290, 291, 292, 293, 294,

            // Empty Dream equipment. These neutral, level-zero components
            // were the main source of severe early-game merchant imbalance.
            295, 296, 297, 298, 299,

            // Strategic and developer-only equipment. These remain available
            // through their technology, quest or dedicated ship acquisition
            // paths, but are excluded from every random market/reward roll.
            311, // Starship Earth Dimension Ascension
            CreativeWorkshopComponentId,
            ObserverCoreComponentId,
            948, // Trisolaris Super Engine
            949, // Trisolaris Antigravity Core
            950, // Ideal Blackbody
            951, // EMP Missile
            952, // Sophon
            953, // Stellar Hydrogen Bomb
            954, // Light-speed Positron Beam
            SilentCoreComponentId,
            DeflectionShieldComponentId,
            AngelShieldComponentId,
            SubspaceShieldComponentId,
            ElectronicShieldComponentId,
            SmallUniverseEntranceComponentId,
            TimeRiftGeneratorComponentId,
            EdgeFirewallCollapseComponentId,
            EdgeDroneHiveComponentId,
            EdgeDefenderComponentId,
            StasisFieldComponentId,
        };

        private const string CreativeWorkshopBuildPreference = "ThreeBody.CreativeWorkshop.BuildId";

        public static bool IsRestrictedComponent(DatabaseComponent component)
        {
            return component != null && RestrictedComponentIds.Contains(component.Id.Value);
        }

        public static bool IsAvailableInRandomMarket(DatabaseComponent component)
        {
            if (component == null || IsRestrictedComponent(component))
                return false;

            // A hidden faction is explicitly not part of the merchant pool.
            // Enforce that rule for components as well as faction research and
            // ships, so Singer, Fringe World and developer content cannot leak
            // through the faction-agnostic random component generator.
            return component.Faction == null || !component.Faction.HideFromMerchants;
        }

        public static bool IsRestrictedShip(Ship ship)
        {
            if (ship == null) return false;
            switch (ship.Id.Value)
            {
                case 160:    // Empty Dream
                case 166:    // Waterdrop
                case ObserverShipId:
                case 114514: // Three Body developer flagship
                case WanNianFengXueShipId:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsRestrictedSatellite(Satellite satellite)
        {
            return satellite != null &&
                   (satellite.Id.Value == 950 || // Experimental equipment platform
                    satellite.Id.Value == 951);  // Weapon test platform
        }

        public static IReadOnlyList<ShipBuild> GetCreativeWorkshopBuilds(IDatabase database)
        {
            if (database == null)
                return System.Array.Empty<ShipBuild>();

            // The code is deliberately based on the build list, rather than
            // only ships, so alternate enemy/default layouts can be selected
            // as workshop drones as well.
            return database.ShipBuildList
                .Where(item => item != null && item != ShipBuild.DefaultValue && item.Ship != null)
                .OrderBy(item => item.Id.Value)
                .ToArray();
        }

        public static bool TryGetCreativeWorkshopDrone(IDatabase database, int persistedBarrelId, int behaviour, out ShipBuild shipBuild)
        {
            shipBuild = ShipBuild.DefaultValue;
            var code = ((byte)persistedBarrelId << 8) | (byte)behaviour;
            if (code == 0)
                return false;

            var builds = GetCreativeWorkshopBuilds(database);
            var index = code - 1;
            if (index < 0 || index >= builds.Count)
                return false;

            shipBuild = builds[index];
            return shipBuild != null && shipBuild != ShipBuild.DefaultValue;
        }

        public static bool TryEncodeCreativeWorkshopDrone(IDatabase database, ShipBuild shipBuild, out int persistedBarrelId, out int behaviour)
        {
            persistedBarrelId = 0;
            behaviour = 0;
            if (shipBuild == null || shipBuild == ShipBuild.DefaultValue)
                return false;

            var builds = GetCreativeWorkshopBuilds(database);
            var index = -1;
            for (var i = 0; i < builds.Count; ++i)
            {
                if (builds[i].Id == shipBuild.Id)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0 || index >= ushort.MaxValue - 1)
                return false;

            var code = index + 1;
            persistedBarrelId = (sbyte)(code >> 8);
            behaviour = (sbyte)(code & 0xff);
            return true;
        }

        public static bool TryGetSelectedCreativeWorkshopDrone(IDatabase database, out ShipBuild shipBuild)
        {
            shipBuild = ShipBuild.DefaultValue;
            var selectedId = PlayerPrefs.GetInt(CreativeWorkshopBuildPreference, 0);
            if (selectedId <= 0)
                return false;

            shipBuild = database?.GetShipBuild(new ItemId<ShipBuild>(selectedId));
            return shipBuild != null && shipBuild != ShipBuild.DefaultValue;
        }

        public static bool TryGetCreativeWorkshopSelectionSettings(IDatabase database, out int persistedBarrelId, out int behaviour)
        {
            persistedBarrelId = int.MinValue;
            behaviour = 0;
            return TryGetSelectedCreativeWorkshopDrone(database, out var build) &&
                   TryEncodeCreativeWorkshopDrone(database, build, out persistedBarrelId, out behaviour);
        }

        public static void SetSelectedCreativeWorkshopDrone(ShipBuild shipBuild)
        {
            if (shipBuild == null || shipBuild == ShipBuild.DefaultValue)
                return;

            PlayerPrefs.SetInt(CreativeWorkshopBuildPreference, shipBuild.Id.Value);
            PlayerPrefs.Save();
        }
    }
}
