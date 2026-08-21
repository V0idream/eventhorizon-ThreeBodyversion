using System;
using System.Collections.Generic;
using System.Linq;
using Constructor;
using Constructor.Satellites;
using Constructor.Ships;
using Economy;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;
using GameServices.Player;
using GameServices.Research;
using ShipEditor.Context;
using Zenject;

namespace Game.Adventure
{
    /// <summary>
    /// In-memory progression state for Adventure Mode.  The owned ships and
    /// component inventory intentionally never touch PlayerFleet/PlayerInventory;
    /// only explicit permanent rewards (credits/research) are written back.
    /// </summary>
    public sealed class AdventureRun : IInventoryProvider
    {
        [Inject]
        public AdventureRun(IDatabase database, PlayerResources playerResources, PlayerInventory playerInventory, Research research)
        {
            _database = database;
            _playerResources = playerResources;
            _playerInventory = playerInventory;
            _research = research;
        }

        public bool Active { get; private set; }
        public bool BossStage { get; private set; }
        public bool Victory { get; private set; }
        public bool Defeat { get; private set; }
        public int Kills { get; private set; }
        public int SuppliesCollected { get; private set; }
        public int Seed { get; private set; }
        public Faction Faction { get; private set; }
        public IReadOnlyList<IShip> OwnedShips => _ships;

        public event Action FleetChanged;
        public event Action InventoryChanged;
        public event Action RunChanged;

        public void Begin(IShip sourceShip)
        {
            if (sourceShip == null || sourceShip.Model == null || sourceShip.Model.SizeClass != SizeClass.Frigate)
                throw new ArgumentException("Adventure Mode must start with an owned frigate.", nameof(sourceShip));

            _ships.Clear();
            _components.Clear();
            _satellites.Clear();
            _hullConditions.Clear();
            Faction = sourceShip.Model.Faction;
            var startingShip = CreateAdventureCopy(sourceShip);
            _ships.Add(startingShip);
            _hullConditions[startingShip] = 1f;
            Seed = Environment.TickCount;
            Kills = 0;
            SuppliesCollected = 0;
            BossStage = false;
            Victory = false;
            Defeat = false;
            Active = true;

            AddStartingComponents(_database.GalaxySettings.StartingInventory?.Loot);

            // The normal starting inventory already contains TargetingUnit in
            // this database.  Treat the requirement as "at least ten" so the
            // mode does not silently grant twenty after content changes.
            var targeting = _database.GetComponent(new ItemId<Component>(91));
            if (targeting != null && targeting != Component.DefaultValue)
            {
                var info = new ComponentInfo(targeting);
                _components.TryGetValue(info, out var current);
                _components[info] = Math.Max(10, current);
            }

            FleetChanged?.Invoke();
            InventoryChanged?.Invoke();
            RunChanged?.Invoke();
        }

        public void End()
        {
            Active = false;
            BossStage = false;
            RunChanged?.Invoke();
        }

        public IShip AddShip(ShipBuild build)
        {
            if (!Active || build == null || build == ShipBuild.DefaultValue || build.Ship == null)
                return null;
            if (Faction == null || build.Faction == null || build.Faction.Id != Faction.Id)
                return null;

            var previousRank = _ships.Count == 0 ? -1 : _ships.Max(GetAdventureRank);
            var fallbackFinalPromotion = build.Ship.ShipType != ShipType.Flagship &&
                                         GetAdventureRank(build) == previousRank &&
                                         !HasHigherProgressionBuild(previousRank);
            var ship = new CommonShip(build, _database);
            _ships.Add(ship);
            _hullConditions[ship] = 1f;
            if (ship.Model.ShipType == ShipType.Flagship || fallbackFinalPromotion)
                BossStage = true;
            FleetChanged?.Invoke();
            RunChanged?.Invoke();
            return ship;
        }

        public bool RemoveShip(IShip ship)
        {
            if (ship == null || !_ships.Remove(ship))
                return false;
            _hullConditions.Remove(ship);
            FleetChanged?.Invoke();
            RunChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// A death removes every hull in the highest currently owned adventure
        /// tier.  Flagships count as the final tier; Titan/Starbase hulls are not
        /// part of Adventure progression.
        /// </summary>
        public IReadOnlyList<IShip> ApplyDeathPenalty()
        {
            if (_ships.Count == 0)
                return Array.Empty<IShip>();

            var highestRank = _ships.Max(GetAdventureRank);
            var removed = _ships.Where(item => GetAdventureRank(item) == highestRank).ToArray();
            foreach (var ship in removed)
            {
                _ships.Remove(ship);
                _hullConditions.Remove(ship);
            }

            BossStage = _ships.Any(item => item.Model.ShipType == ShipType.Flagship);
            if (!_ships.Any(item => item.Model.SizeClass == SizeClass.Frigate))
                Defeat = true;

            FleetChanged?.Invoke();
            RunChanged?.Invoke();
            return removed;
        }

        public IReadOnlyList<ShipBuild> GetUpgradeChoices()
        {
            if (!Active || Faction == null || BossStage || _ships.Count == 0)
                return Array.Empty<ShipBuild>();

            var currentRank = _ships.Max(GetAdventureRank);
            var candidates = _database.ShipBuildList
                .Where(IsAdventureProgressionBuild)
                .Where(item => item.Faction != null && item.Faction.Id == Faction.Id)
                .Where(item => GetAdventureRank(item) > currentRank)
                .ToList();
            if (candidates.Count == 0)
            {
                // Some legacy/player-available factions have no authored flagship.
                // Keep those runs finishable: a further upgrade at the faction's
                // highest available combat tier becomes its flagship-equivalent.
                return _database.ShipBuildList
                    .Where(IsAdventureProgressionBuild)
                    .Where(item => item.Faction != null && item.Faction.Id == Faction.Id)
                    .Where(item => GetAdventureRank(item) == currentRank)
                    .OrderBy(item => item.Id.Value)
                    .ToArray();
            }

            var nextRank = candidates.Min(GetAdventureRank);
            return candidates.Where(item => GetAdventureRank(item) == nextRank)
                .OrderBy(item => item.Id.Value)
                .ToArray();
        }

        public ShipBuild GetBossBuild()
        {
            if (Faction == null)
                return null;

            var sameFaction = _database.ShipBuildList
                .Where(item => item != null && item != ShipBuild.DefaultValue && item.Ship != null)
                .Where(item => item.Faction != null && item.Faction.Id == Faction.Id)
                .Where(item => item.Ship.SizeClass != SizeClass.Starbase && item.Ship.SizeClass != SizeClass.TitanP)
                .Where(item => item.AvailableForEnemy)
                .ToArray();

            var flagship = sameFaction
                .Where(item => item.Ship.ShipType == ShipType.Flagship)
                .OrderByDescending(item => item.DifficultyClass)
                .ThenBy(item => item.Id.Value)
                .FirstOrDefault();
            if (flagship != null)
                return flagship;

            // Compatibility fallback for legacy factions without a flagship
            // record. The highest same-faction combat build becomes the final
            // capital-ship opponent rather than leaving the run unwinnable.
            return sameFaction
                .Where(item => item.Ship.ShipType == ShipType.Common)
                .Where(item => item.Ship.SizeClass >= SizeClass.Frigate && item.Ship.SizeClass <= SizeClass.Battleship)
                .OrderByDescending(GetAdventureRank)
                .ThenByDescending(item => item.DifficultyClass)
                .ThenBy(item => item.Id.Value)
                .FirstOrDefault();
        }

        private bool HasHigherProgressionBuild(int currentRank)
        {
            if (Faction == null) return false;
            return _database.ShipBuildList
                .Where(IsAdventureProgressionBuild)
                .Any(item => item.Faction != null && item.Faction.Id == Faction.Id &&
                             GetAdventureRank(item) > currentRank);
        }

        public ShipBuild GetRandomEnemyBuild(Random random, int desiredRank)
        {
            var pool = _database.ShipBuildList
                .Where(item => item != null && item != ShipBuild.DefaultValue && item.Ship != null)
                .Where(item => item.AvailableForEnemy)
                .Where(item => item.Ship.ShipType == ShipType.Common)
                .Where(item => item.Ship.SizeClass >= SizeClass.Frigate && item.Ship.SizeClass <= SizeClass.Battleship)
                .Where(item => item.Faction == null || Faction == null || item.Faction.Id != Faction.Id)
                .Where(item => Math.Abs(GetAdventureRank(item) - desiredRank) <= 1)
                .ToList();
            if (pool.Count == 0)
                pool = _database.ShipBuildList
                    .Where(item => item != null && item != ShipBuild.DefaultValue && item.Ship != null &&
                                   item.AvailableForEnemy && item.Ship.ShipType == ShipType.Common &&
                                   item.Ship.SizeClass >= SizeClass.Frigate && item.Ship.SizeClass <= SizeClass.Battleship)
                    .ToList();
            return pool.Count == 0 ? null : pool[random.Next(pool.Count)];
        }

        public ComponentInfo GetRandomTemporaryComponent(Random random)
        {
            var level = 20 + Math.Max(0, Kills / 3) * 5;
            if (ComponentInfo.TryCreateRandomComponent(_database, level, Faction, random, true,
                    ComponentQuality.P3, out var info))
                return info;
            return ComponentInfo.Empty;
        }

        public void RegisterKill() { Kills++; RunChanged?.Invoke(); }
        public void RegisterSupply() { SuppliesCollected++; RunChanged?.Invoke(); }

        public void RememberHullCondition(IShip ship, float armorPercentage)
        {
            if (ship == null || !_ships.Contains(ship)) return;
            _hullConditions[ship] = Math.Max(0f, Math.Min(1f, armorPercentage));
        }

        public float GetHullCondition(IShip ship)
        {
            return ship != null && _hullConditions.TryGetValue(ship, out var value)
                ? Math.Max(0f, Math.Min(1f, value))
                : 1f;
        }

        public void AwardCredits(long amount)
        {
            if (amount <= 0) return;
            _playerResources.Money = (long)_playerResources.Money + amount;
        }

        public VictoryReward AwardVictoryRewards()
        {
            if (Victory || Defeat || Faction == null)
                return null;

            Victory = true;
            BossStage = true;
            // Permanent rewards intentionally scale with the run while keeping
            // the mode useful even for a short successful route.
            var credits = 100_000L + Kills * 2_500L + SuppliesCollected * 1_000L;
            var researchPoints = 100 + Math.Min(400, Kills * 5);
            AwardCredits(credits);
            _research.AddResearchPoints(Faction, researchPoints);

            var random = new Random(Seed ^ Kills ^ (SuppliesCollected << 8));
            var rewardCount = 3 + Math.Min(3, SuppliesCollected / 5);
            var components = new List<ComponentInfo>();
            for (var i = 0; i < rewardCount; i++)
            {
                // This is the same component generator used by normal
                // exploration containers and ship wrecks in LootGenerator.
                if (ComponentInfo.TryCreateRandomComponent(_database, 100, Faction, random, true,
                        ComponentQuality.P3, out var component) && component)
                {
                    _playerInventory.Components.Add(component);
                    components.Add(component);
                }
            }
            RunChanged?.Invoke();
            return new VictoryReward(credits, researchPoints, components);
        }

        public sealed class VictoryReward
        {
            public VictoryReward(long credits, int researchPoints, IReadOnlyList<ComponentInfo> components)
            {
                Credits = credits;
                ResearchPoints = researchPoints;
                Components = components ?? Array.Empty<ComponentInfo>();
            }

            public long Credits { get; }
            public int ResearchPoints { get; }
            public IReadOnlyList<ComponentInfo> Components { get; }
        }

        public void MarkDefeat()
        {
            Defeat = true;
            RunChanged?.Invoke();
        }

        public static bool IsAdventureProgressionBuild(ShipBuild build)
        {
            if (build == null || build == ShipBuild.DefaultValue || build.Ship == null || !build.AvailableForPlayer)
                return false;
            if (build.Ship.SizeClass == SizeClass.Starbase || build.Ship.SizeClass == SizeClass.TitanP)
                return false;
            if (build.Ship.ShipType == ShipType.Flagship)
                return true;
            return build.Ship.ShipType == ShipType.Common &&
                   build.Ship.SizeClass >= SizeClass.Frigate && build.Ship.SizeClass <= SizeClass.Battleship;
        }

        public static int GetAdventureRank(ShipBuild build)
        {
            if (build?.Ship == null) return -1;
            return build.Ship.ShipType == ShipType.Flagship ? 4 : (int)build.Ship.SizeClass;
        }

        public static int GetAdventureRank(IShip ship)
        {
            if (ship?.Model == null) return -1;
            return ship.Model.ShipType == ShipType.Flagship ? 4 : (int)ship.Model.SizeClass;
        }

        private void AddStartingComponents(LootContent content)
        {
            switch (content)
            {
                case LootContent_Component component when component.Component != null && component.Component != Component.DefaultValue:
                    AddComponent(new ComponentInfo(component.Component), Math.Max(1, component.MinAmount));
                    break;
                case LootContent_AllItems all:
                    foreach (var item in all.Items)
                        AddStartingComponents(item?.Loot);
                    break;
                case LootContent_RandomItems randomItems:
                    foreach (var item in randomItems.Items)
                        AddStartingComponents(item?.Loot);
                    break;
                case LootContent_ItemsWithChance chance:
                    foreach (var item in chance.Items)
                        AddStartingComponents(item?.Loot);
                    break;
            }
        }

        public static IShip CreateAdventureCopy(IShip source)
        {
            var copy = new CommonShip(source.Model, source.Components.Select(CloneInstalledComponent))
            {
                FirstSatellite = CloneSatellite(source.FirstSatellite),
                SecondSatellite = CloneSatellite(source.SecondSatellite),
                Experience = source.Experience,
                Name = source.Name,
            };
            copy.ColorScheme.Value = source.ColorScheme.Value;
            return copy;
        }

        private static ISatellite CloneSatellite(ISatellite source)
        {
            return source == null
                ? null
                : new CommonSatellite(source.Information, source.Components.Select(CloneInstalledComponent));
        }

        private static IntegratedComponent CloneInstalledComponent(IntegratedComponent source)
        {
            return new IntegratedComponent(source.Info, source.X, source.Y, source.BarrelId,
                source.KeyBinding, source.Behaviour, source.Locked, source.Rotation);
        }

        private void AddComponent(ComponentInfo component, int count)
        {
            if (!component || count <= 0) return;
            _components.TryGetValue(component, out var current);
            _components[component] = current + count;
        }

        #region Adventure ship-editor inventory
        public IEnumerable<IShip> Ships => _ships;
        public IReadOnlyCollection<ISatellite> SatelliteBuilds => Array.Empty<ISatellite>();
        public IReadOnlyCollection<Satellite> Satellites => _satellites.Keys.ToArray();
        public IReadOnlyCollection<ComponentInfo> Components => _components.Keys.ToArray();

        public int GetQuantity(ComponentInfo component) => _components.TryGetValue(component, out var count) ? count : 0;
        public void AddComponent(ComponentInfo component)
        {
            AddComponent(component, 1);
            InventoryChanged?.Invoke();
        }

        public bool TryRemoveComponent(ComponentInfo component)
        {
            if (!_components.TryGetValue(component, out var count) || count <= 0) return false;
            if (count == 1) _components.Remove(component);
            else _components[component] = count - 1;
            InventoryChanged?.Invoke();
            return true;
        }

        public int GetQuantity(Satellite satellite) => satellite != null && _satellites.TryGetValue(satellite, out var count) ? count : 0;
        public void AddSatellite(Satellite satellite)
        {
            if (satellite == null) return;
            _satellites.TryGetValue(satellite, out var count);
            _satellites[satellite] = count + 1;
            InventoryChanged?.Invoke();
        }

        public bool TryRemoveSatellite(Satellite satellite)
        {
            if (satellite == null || !_satellites.TryGetValue(satellite, out var count) || count <= 0) return false;
            if (count == 1) _satellites.Remove(satellite);
            else _satellites[satellite] = count - 1;
            InventoryChanged?.Invoke();
            return true;
        }

        public Price GetUnlockPrice(ComponentInfo component) => Price.Common(0);
        public bool TryPayForUnlock(ComponentInfo component) => true;
        #endregion

        private readonly IDatabase _database;
        private readonly PlayerResources _playerResources;
        private readonly PlayerInventory _playerInventory;
        private readonly Research _research;
        private readonly List<IShip> _ships = new();
        private readonly Dictionary<ComponentInfo, int> _components = new();
        private readonly Dictionary<Satellite, int> _satellites = new();
        private readonly Dictionary<IShip, float> _hullConditions = new();
    }
}
