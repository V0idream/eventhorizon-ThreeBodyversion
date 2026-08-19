using System;
using System.Collections.Generic;
using System.Linq;
using Economy;
using Economy.ItemType;
using Economy.Products;
using Galaxy.StarContent;
using GameDatabase.DataModel;
using GameModel;
using GameServices;
using GameServices.Economy;
using GameServices.Settings;
using Services.InternetTime;
using Session;
using UnityEngine;
using CommonComponents.Signals;

namespace Domain.Player
{
    public class DailyReward : GameServiceBase
    {
        public DailyReward(
            ISessionData session, 
            GameSettings gameSettings, 
            InternetTimeService internetTime, 
            ServerTimeReceivedSignal timeReceivedSignal,
            DailyRewardAwailableSignal.Trigger rewardAvailableTrigger,
            SessionDataLoadedSignal sessionDataLoadedSignal,
            SessionCreatedSignal sessionCreatedSignal,
            LootGenerator lootGenerator,
            RegionMap regionMap,
            ItemTypeFactory itemTypeFactory)
            : base(sessionDataLoadedSignal, sessionCreatedSignal)
        {
            _session = session;
            _lootGenerator = lootGenerator;
            _gameSettings = gameSettings;
            _internetTime = internetTime;
            _rewardAvailableTrigger = rewardAvailableTrigger;
            _serverTimeReceivedSignal = timeReceivedSignal;
            _regionMap = regionMap;
            _itemTypeFactory = itemTypeFactory;
            _serverTimeReceivedSignal.Event += CheckForReward;
        }

        public bool IsRewardExists()
        {
            if (!_session.IsGameStarted() || System.DateTime.UtcNow.Ticks - _session.Game.GameStartTime  < System.TimeSpan.TicksPerDay)
                return false;

            return _internetTime.HasBeenReceived && IsRewardExists(TimeToDays(_internetTime.DateTime));
        }

        public IEnumerable<IProduct> CollectReward()
        {
            var size = GetRewardSizeAndUpdate();

            if (size <= 0)
                return null;

            var level = StarLayout.GetStarLevel(_session.StarMap.FurthestVisitedStar, 0);
            var seed = TimeToDays(_internetTime.DateTime);
            var rewards = _lootGenerator.GetDailyReward(size, level, seed).ToList();
            AppendCapturedStarbaseRewards(rewards);
            return rewards;
        }

        private void AppendCapturedStarbaseRewards(ICollection<IProduct> rewards)
        {
            var tradeCredits = 0;
            var tradeStars = 0;
            var researchByFaction = new Dictionary<Faction, int>();

            foreach (var region in CapturedStarbaseFacilities.GetCapturedRegions(_session, _regionMap))
            {
                var tier = region.CapturedStarbaseTier;
                if (tier <= 0)
                    continue;

                switch (region.CapturedStarbaseFacility)
                {
                    case CapturedStarbaseFacilityType.Trade:
                        tradeCredits += tier * CapturedStarbaseFacilities.TradeCreditsPerTier;
                        tradeStars += tier * CapturedStarbaseFacilities.TradeStarsPerTier;
                        break;

                    case CapturedStarbaseFacilityType.Research:
                        if (region.Faction == null || region.Faction == Faction.Empty)
                            break;
                        researchByFaction.TryGetValue(region.Faction, out var current);
                        researchByFaction[region.Faction] = current + tier;
                        break;
                }
            }

            if (tradeCredits > 0)
                rewards.Add(CommonProduct.Create(
                    _itemTypeFactory.CreateCurrencyItem(Currency.Credits),
                    tradeCredits));
            if (tradeStars > 0)
                rewards.Add(CommonProduct.Create(
                    _itemTypeFactory.CreateCurrencyItem(Currency.Stars),
                    tradeStars));

            foreach (var pair in researchByFaction.OrderBy(item => item.Key.Id.Value))
            {
                if (pair.Value <= 0)
                    continue;
                rewards.Add(CommonProduct.Create(
                    _itemTypeFactory.CreateResearchItem(pair.Key),
                    pair.Value));
            }
        }

        private void CheckForReward(DateTime time)
        {
            if (!_session.IsGameStarted())
                return;

            var days = TimeToDays(time);
            if (_session.Social.LastDailyRewardDate > days)
            {
                _session.Social.LastDailyRewardDate = days;
                _session.Social.FirstDailyRewardDate = 0;
            }
            if (_gameSettings.LastDailyRewardDate > days)
            {
                _gameSettings.LastDailyRewardDate = days;
            }
            
            if (IsRewardExists(days))
                _rewardAvailableTrigger.Fire();
        }

        private bool IsRewardExists(int currentDate)
        {
            var lastDate = Mathf.Max(_session.Social.LastDailyRewardDate, _gameSettings.LastDailyRewardDate);
            var daysLeft = currentDate - lastDate;

            return daysLeft > 0;
        }

        private int GetRewardSizeAndUpdate()
        {
            if (!_internetTime.HasBeenReceived)
                return 0;

            var currentDate = TimeToDays(_internetTime.DateTime);
            var lastDate = Mathf.Max(_session.Social.LastDailyRewardDate, _gameSettings.LastDailyRewardDate);
            var daysLeft = currentDate - lastDate;

            if (daysLeft < 0)
                return 0;

            _session.Social.LastDailyRewardDate = currentDate;
            _gameSettings.LastDailyRewardDate = currentDate;

            if (daysLeft != 1 || _session.Social.FirstDailyRewardDate == 0)
            {
                _session.Social.FirstDailyRewardDate = currentDate;
                return 1;
            }

            return 1 + currentDate - _session.Social.FirstDailyRewardDate;
        }

        public static int TimeToDays(DateTime time)
        {
            return (int)(time.Ticks / TimeSpan.TicksPerDay);
        }

        protected override void OnSessionDataLoaded()
        {
        }

        protected override void OnSessionCreated()
        {
            if (_internetTime.HasBeenReceived)
                CheckForReward(_internetTime.DateTime);
        }

        private readonly ISessionData _session;
        private readonly GameSettings _gameSettings;
        private readonly InternetTimeService _internetTime;
        private readonly ServerTimeReceivedSignal _serverTimeReceivedSignal;
        private readonly DailyRewardAwailableSignal.Trigger _rewardAvailableTrigger;
        private readonly LootGenerator _lootGenerator;
        private readonly RegionMap _regionMap;
        private readonly ItemTypeFactory _itemTypeFactory;
    }

    public class DailyRewardAwailableSignal : SmartWeakSignal<DailyRewardAwailableSignal> {}
}
