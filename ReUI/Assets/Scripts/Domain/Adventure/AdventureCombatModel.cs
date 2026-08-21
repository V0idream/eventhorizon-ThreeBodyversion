using System.Collections.Generic;
using System.Linq;
using Combat.Component.Unit.Classification;
using Combat.Domain;
using Constructor.Ships;
using GameDatabase;
using GameDatabase.DataModel;
using GameModel.Quests;
using GameServices.Economy;
using GameServices.Player;

namespace Game.Adventure
{
    public sealed class AdventureCombatModel : ICombatModel
    {
        public AdventureCombatModel(AdventureRun run, IDatabase database, PlayerSkills playerSkills, int level)
        {
            Run = run;
            var rules = database.GalaxySettings.QuickCombatRules ?? database.CombatSettings.DefaultCombatRules;
            Rules = rules.Create(level, true);
            Player = new AdventureFleetModel(database, UnitSide.Player, 100, playerSkills);
            Allies = new AdventureFleetModel(database, UnitSide.Ally, 70, playerSkills);
            Enemies = new AdventureFleetModel(database, UnitSide.Enemy, 45);
            Player.Sync(run.OwnedShips);
            foreach (var info in Player.Ships.OfType<ShipInfo>())
                info.RestoreForNextActivation(run.GetHullCondition(info.ShipData));
        }

        public AdventureRun Run { get; }
        public AdventureFleetModel Player { get; }
        public AdventureFleetModel Allies { get; }
        public AdventureFleetModel Enemies { get; }
        public CombatRulesAdapter Rules { get; }
        public IFleetModel PlayerFleet => Player;
        public IFleetModel AllyFleet => Allies;
        public IFleetModel EnemyFleet => Enemies;
        public IShipInfo DefenseStarbase => null;
        public bool IsStarbaseDefense => false;

        public IReward GetReward(LootGenerator lootGenerator, PlayerSkills playerSkills, Galaxy.Star currentStar)
        {
            // Adventure rewards are granted by AdventureCombatController.  This
            // state never routes through CombatState's ordinary reward path.
            return null;
        }
    }

    public sealed class AdventureFleetModel : IFleetModel
    {
        public AdventureFleetModel(IDatabase database, UnitSide side, int aiLevel, PlayerSkills playerSkills = null)
        {
            _database = database;
            _side = side;
            _playerSkills = playerSkills;
            AiLevel = aiLevel;
        }

        public IList<IShipInfo> Ships => _ships;
        public int AiLevel { get; }

        public IShipInfo Add(IShip ship, bool collaborative = false)
        {
            if (ship == null || _ships.Any(item => ReferenceEquals(item.ShipData, ship)))
                return _ships.FirstOrDefault(item => ReferenceEquals(item.ShipData, ship));

            var spec = _playerSkills != null
                ? ship.CreateBuilder().ApplyPlayerSkills(_playerSkills).Build(_database.ShipSettings)
                : ship.CreateBuilder().Build(_database.ShipSettings);
            var info = new ShipInfo(ship, spec, _side, collaborative);
            _ships.Add(info);
            return info;
        }

        public bool Remove(IShipInfo ship)
        {
            if (ship == null)
                return false;
            if (ship.Status == ShipStatus.Active)
                ship.Destroy();
            return _ships.Remove(ship);
        }

        public bool Remove(IShip ship)
        {
            var info = _ships.FirstOrDefault(item => ReferenceEquals(item.ShipData, ship));
            return Remove(info);
        }

        public void Sync(IEnumerable<IShip> ships)
        {
            var desired = new HashSet<IShip>(ships ?? Enumerable.Empty<IShip>());
            foreach (var item in _ships.Where(item => !desired.Contains(item.ShipData)).ToArray())
            {
                if (item.Status == ShipStatus.Active)
                    item.Destroy();
                _ships.Remove(item);
            }

            foreach (var ship in desired)
                Add(ship);
        }

        public void Rebuild(IShip ship)
        {
            var index = _ships.FindIndex(item => ReferenceEquals(item.ShipData, ship));
            if (index < 0) return;
            var previous = _ships[index];
            if (previous.Status == ShipStatus.Active)
                return;

            var spec = _playerSkills != null
                ? ship.CreateBuilder().ApplyPlayerSkills(_playerSkills).Build(_database.ShipSettings)
                : ship.CreateBuilder().Build(_database.ShipSettings);
            _ships[index] = new ShipInfo(ship, spec, _side, previous.IsCollaborativeAlly);
        }

        private readonly IDatabase _database;
        private readonly UnitSide _side;
        private readonly PlayerSkills _playerSkills;
        private readonly List<IShipInfo> _ships = new();
    }
}
