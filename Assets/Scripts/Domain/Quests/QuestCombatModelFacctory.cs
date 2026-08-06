using System.Linq;
using Economy.Products;
using Combat.Domain;
using GameDatabase;
using GameDatabase.Enums;
using GameServices.Player;
using Combat.Component.Unit.Classification;
using ViewModel;
using Galaxy.StarContent;
using GameModel;
using Session;

namespace Domain.Quests
{
    public class QuestCombatModelFacctory
    {
		private readonly IDatabase _database;
		private readonly CombatModelBuilder.Factory _combatModelBuilderFactory;
		private readonly PlayerFleet _playerFleet;
		private readonly RegionMap _regionMap;
		private readonly ISessionData _session;

		public QuestCombatModelFacctory(
			IDatabase database,
			PlayerFleet playerFleet,
			CombatModelBuilder.Factory combatModelBuilderFactory,
			RegionMap regionMap,
			ISessionData session)
        {
			_database = database;
			_playerFleet = playerFleet;
			_combatModelBuilderFactory = combatModelBuilderFactory;
			_regionMap = regionMap;
			_session = session;
        }

		public Model.Military.IFleet CreateEnemyFleet(QuestEnemyData enemyData)
        {
			var builds = enemyData.EnemyFleet?
				.Where(item => item != null && item != GameDatabase.DataModel.ShipBuild.DefaultValue)
				.ToArray() ?? System.Array.Empty<GameDatabase.DataModel.ShipBuild>();
			return new Model.Military.QuestFleet(_database, builds, enemyData.EnemyLevel, enemyData.Seed);
		}

        public ICombatModel CreateCombatModel(QuestEnemyData enemyData, ILoot specialLoot)
        {
			var builder = _combatModelBuilderFactory.Create();
			builder.EnemyFleet = CreateEnemyFleet(enemyData);
			builder.PlayerFleet = Model.Factories.Fleet.Player(_playerFleet, _database);
			builder.Rules = enemyData.Rules ?? _database.CombatSettings.DefaultCombatRules;
            builder.StarLevel = enemyData.StarLevel;

			var loot = specialLoot?.Items.Select(item => CommonProduct.Create(item.Type, item.Quantity));

			return builder.Build(loot);
		}

		public ICombatModel CreateMothersTearsCombatModel(
			QuestEnemyData enemyData,
			ILoot specialLoot,
			int starId)
		{
			var builder = _combatModelBuilderFactory.Create();
			builder.EnemyFleet = CreateEnemyFleet(enemyData);
			builder.PlayerFleet = Model.Factories.Fleet.Player(_playerFleet, _database);
			builder.Rules = enemyData.Rules ?? _database.CombatSettings.DefaultCombatRules;
			builder.StarLevel = enemyData.StarLevel;
			builder.EnemyFactionIdOverride = 22;

			CombatRelations.SetRelation(0, 22, false);
			if (FactionPanelViewModel.IncludeStarshipEarthAllies)
			{
				CombatRelations.SetRelation(0, 21, true);
				var supportFaction = _database.GetFaction(
					new GameDatabase.Model.ItemId<GameDatabase.DataModel.Faction>(Region.StarshipEarthFactionId));
				var supportBonus = CapturedStarbaseFacilities.GetSupportBonus(
					_session, _regionMap, supportFaction);
				builder.AllyFleet = Model.Factories.Fleet.MothersTearsAllies(
					enemyData.StarLevel,
					starId ^ enemyData.Seed ^ 0x4D5441,
					_database,
					supportBonus.LevelBonus,
					supportBonus.ExtraBattleships);
			}

			var loot = specialLoot?.Items.Select(item => CommonProduct.Create(item.Type, item.Quantity));
			return builder.Build(loot);
		}
	}
}
