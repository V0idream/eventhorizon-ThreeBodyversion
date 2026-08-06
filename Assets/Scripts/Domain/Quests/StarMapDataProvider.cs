using Session;
using System;
using System.Collections.Generic;
using Galaxy.StarContent;
using GameModel;
using GameServices.Random;

namespace Domain.Quests
{
    public class StarMapDataProvider : IStarMapDataProvider, IPlayerDataProvider
    {
        private readonly SessionDataLoadedSignal _sessionDataLoadedSignal;
        private readonly ISessionData _session;
        private readonly RegionMap _regionMap;
		private readonly Occupants _occupants;
		private readonly IRandom _random;

		private StarDataProvider _currentStar;
        private StarDataProvider _lastStar;

        public StarMapDataProvider(
            ISessionData session,
			Occupants occupants,
            RegionMap regionMap,
            IRandom random,
            SessionDataLoadedSignal sessionDataLoadedSignal)
        {
            _session = session;
			_occupants = occupants;
            _regionMap = regionMap;
			_random = random;
            _sessionDataLoadedSignal = sessionDataLoadedSignal;
            _sessionDataLoadedSignal.Event += OnSessionDataLoaded;
        }

        public IStarDataProvider CurrentStar
        {
            get
            {
                var id = _session.StarMap.PlayerPosition;
                if (_currentStar != null && _currentStar.Id == id) 
                    return _currentStar;

                return _currentStar = new StarDataProvider(id, _occupants, _regionMap, _session.Game.Seed);
            }
        }

        public IStarDataProvider GetStarData(int id)
        {
            if (_lastStar != null && _lastStar.Id == id) 
                return _lastStar;

            return _lastStar = new StarDataProvider(id, _occupants, _regionMap, _session.Game.Seed);
        }

        public int RandomStarAtDistance(int centerStarId, int distance, Random random)
        {
            return GameModel.StarLayout.GetAdjacentStars(centerStarId, distance).RandomElement(random);
        }

		public IEnumerable<int> GetStarsAtDistance(int centerStarId, int distance)
		{
			return GameModel.StarLayout.GetAdjacentStars(centerStarId, distance);
		}

		public bool HasHive(int starId)
		{
			// StarData reserves the handcrafted inner systems and region home stars
			// before evaluating the deterministic 600..649 hive range. Mirror that
			// rule here so quest beacons always point to a real, selectable hive.
			if (starId < 24)
				return false;

			var star = GetStarData(starId);
			if (star?.Region == null || star.Region.IsHome ||
				star.Region.Faction == null || star.Region.Faction.Id.Value == 0)
				return false;

			var value = _random.RandomInt(starId, 1000);
			return value >= 600 && value < 650;
		}

        public IEnumerable<IRegionDataProvider> GetRegionsNearby(int centerStarId, int minDistance, int maxDistance)
        {
            List<GameModel.Region> regions = new();
            _regionMap.GetAdjacentRegions(centerStarId, minDistance, maxDistance, regions);

            foreach (var item in regions)
                yield return new RegionDataProvider(item);
        }

        private void OnSessionDataLoaded()
        {
            _currentStar = null;
            _lastStar = null;
        }
    }
}
