using System;
using System.Collections.Generic;
using GameDatabase.DataModel;
using GameModel;
using Session;
using UnityEngine;

namespace Galaxy.StarContent
{
    public enum CapturedStarbaseFacilityType
    {
        Trade = 0,
        Border = 1,
        Research = 2,
        Prism = 3,
        Lane = 4,
    }

    public readonly struct CapturedStarbaseSupportBonus
    {
        public CapturedStarbaseSupportBonus(int levelBonus, int extraBattleships)
        {
            LevelBonus = Mathf.Max(0, levelBonus);
            ExtraBattleships = Mathf.Clamp(extraBattleships, 0, CapturedStarbaseFacilities.MaxExtraBattleships);
        }

        public int LevelBonus { get; }
        public int ExtraBattleships { get; }
    }

    /// <summary>
    /// Persists and evaluates the role assigned to each captured starbase.
    /// Facility roles intentionally live outside the generated save schema so
    /// existing saves remain readable.  The key is scoped to the current save
    /// identity; an absent key means Trade, which migrates every previously
    /// captured station to the required default automatically.
    /// </summary>
    public static class CapturedStarbaseFacilities
    {
        public const int LevelsPerTier = 50;
        public const int MaxTier = 10;
        public const int MaxExtraBattleships = 10;
        public const int PrismRange = 50;
        public const int LaneRange = 100;
        public const int TradeCreditsPerTier = 10000;
        public const int TradeStarsPerTier = 10;

        public static int CalculateTier(int level)
        {
            return Mathf.Clamp(Mathf.Max(0, level) / LevelsPerTier, 0, MaxTier);
        }

        public static int GetTier(Region region)
        {
            return region == null || region == Region.Empty ? 0 : CalculateTier(region.HomeStarLevel);
        }

        public static CapturedStarbaseFacilityType GetFacilityType(ISessionData session, int regionId)
        {
            if (session == null || regionId <= Region.UnoccupiedRegionId)
                return CapturedStarbaseFacilityType.Trade;

            var value = PlayerPrefs.GetInt(GetStorageKey(session, regionId),
                (int)CapturedStarbaseFacilityType.Trade);
            return value >= (int)CapturedStarbaseFacilityType.Trade &&
                   value <= (int)CapturedStarbaseFacilityType.Lane
                ? (CapturedStarbaseFacilityType)value
                : CapturedStarbaseFacilityType.Trade;
        }

        public static void SetFacilityType(
            ISessionData session,
            int regionId,
            CapturedStarbaseFacilityType facilityType)
        {
            if (session == null || regionId <= Region.UnoccupiedRegionId)
                return;

            var value = Mathf.Clamp((int)facilityType,
                (int)CapturedStarbaseFacilityType.Trade,
                (int)CapturedStarbaseFacilityType.Lane);
            PlayerPrefs.SetInt(GetStorageKey(session, regionId), value);
            PlayerPrefs.Save();
        }

        public static IEnumerable<Region> GetCapturedRegions(ISessionData session, RegionMap regionMap)
        {
            if (session == null || regionMap == null)
                yield break;

            var seen = new HashSet<int>();
            foreach (var regionId in session.Regions.Regions)
            {
                if (regionId <= Region.UnoccupiedRegionId || !seen.Add(regionId))
                    continue;

                var region = regionMap[regionId];
                if (region != null && region != Region.Empty && region.IsCaptured)
                    yield return region;
            }

            // The player's initial region is captured by construction and is
            // not present in the captured-bases bitset in older saves. Include
            // it explicitly when it represents a valid owned region.
            if (seen.Add(Region.PlayerHomeRegionId))
            {
                var homeRegion = regionMap[Region.PlayerHomeRegionId];
                if (homeRegion != null && homeRegion != Region.Empty && homeRegion.IsCaptured)
                    yield return homeRegion;
            }
        }

        public static CapturedStarbaseSupportBonus GetSupportBonus(
            ISessionData session,
            RegionMap regionMap,
            Faction faction)
        {
            if (faction == null || faction == Faction.Empty)
                return new CapturedStarbaseSupportBonus(0, 0);

            var levelBonus = 0;
            var extraBattleships = 0;
            foreach (var region in GetCapturedRegions(session, regionMap))
            {
                if (region.Faction == null || region.Faction.Id.Value != faction.Id.Value ||
                    region.CapturedStarbaseFacility != CapturedStarbaseFacilityType.Border)
                    continue;

                var tier = region.CapturedStarbaseTier;
                levelBonus += tier;
                if (tier >= MaxTier)
                    extraBattleships++;
            }

            return new CapturedStarbaseSupportBonus(
                levelBonus,
                Mathf.Min(MaxExtraBattleships, extraBattleships));
        }

        public static bool IsAdvancedFacilityUnlocked(Region region)
        {
            return region != null && region != Region.Empty && region.IsCaptured &&
                   region.CapturedStarbaseTier >= MaxTier;
        }

        public static bool IsPrismCharged(ISessionData session, int regionId)
        {
            if (session == null || regionId <= Region.UnoccupiedRegionId)
                return false;

            return PlayerPrefs.GetString(GetPrismUseKey(session, regionId), string.Empty) != TodayKey;
        }

        public static bool TryConsumePrismCharge(ISessionData session, int regionId)
        {
            if (!IsPrismCharged(session, regionId))
                return false;

            PlayerPrefs.SetString(GetPrismUseKey(session, regionId), TodayKey);
            PlayerPrefs.Save();
            return true;
        }

        public static string GetChineseName(CapturedStarbaseFacilityType facilityType)
        {
            switch (facilityType)
            {
                case CapturedStarbaseFacilityType.Border:
                    return "边防站";
                case CapturedStarbaseFacilityType.Research:
                    return "科研站";
                case CapturedStarbaseFacilityType.Prism:
                    return "棱镜";
                case CapturedStarbaseFacilityType.Lane:
                    return "航道";
                default:
                    return "贸易站";
            }
        }

        public static Color GetMapColor(CapturedStarbaseFacilityType facilityType)
        {
            switch (facilityType)
            {
                case CapturedStarbaseFacilityType.Border:
                    return new Color(1f, 0.18f, 0.16f, 1f);
                case CapturedStarbaseFacilityType.Research:
                    return new Color(0.18f, 0.55f, 1f, 1f);
                case CapturedStarbaseFacilityType.Prism:
                    return new Color(1f, 0.78f, 0.2f, 1f);
                case CapturedStarbaseFacilityType.Lane:
                    return new Color(0.62f, 0.34f, 1f, 1f);
                default:
                    return new Color(0.18f, 1f, 0.38f, 1f);
            }
        }

        public static string GetTierText(int tier)
        {
            return tier > 0 ? tier + "阶" : "未达一阶";
        }

        private static string GetStorageKey(ISessionData session, int regionId)
        {
            return $"ThreeBody.CapturedStarbaseFacility.{session.Game.Seed}." +
                   $"{session.Game.GameStartTime}.{regionId}";
        }

        private static string GetPrismUseKey(ISessionData session, int regionId)
        {
            return $"ThreeBody.CapturedStarbasePrism.{session.Game.Seed}." +
                   $"{session.Game.GameStartTime}.{regionId}";
        }

        private static string TodayKey => DateTime.Now.ToString("yyyyMMdd");
    }
}
