using System;
using GameDatabase;
using GameDatabase.DataModel;
using Zenject;

namespace Domain.Quests
{
    public class QuestFactory
    {
        [Inject] private readonly IDatabase _database;
        [Inject] private readonly IQuestBuilderContext _questBuilderContext;

        public Quest Create(QuestProgress progress)
        {
            var data = _database.GetQuest(progress.QuestId);
            if (data == null)
            {
                UnityEngine.Debug.LogException(new ArgumentException("QuestFactory: quest not found - " + progress.QuestId));
                return null;
            }

            if (MothersTearsQuestBuilder.IsMothersTears(data))
                return MothersTearsQuestBuilder.Build(
                    data,
                    progress.StarId,
                    progress.Seed,
                    progress.ActiveNode,
                    _questBuilderContext);

            if (BeautifulProminenceQuestBuilder.IsBeautifulProminence(data))
                return BeautifulProminenceQuestBuilder.Build(
                    data,
                    progress.StarId,
                    progress.Seed,
                    progress.ActiveNode,
                    _questBuilderContext);

            var builder = new QuestBuilder(data, progress.StarId, progress.Seed, _questBuilderContext);
            return builder.Build(progress.ActiveNode);
        }

        public Quest Create(QuestModel data, int starId, int seedIncrement = 0)
        {
            var seed = _questBuilderContext.QuestDataProvider.GenerateSeed(data, starId) + seedIncrement;
            if (MothersTearsQuestBuilder.IsMothersTears(data))
                return MothersTearsQuestBuilder.Build(data, starId, seed, 0, _questBuilderContext);

            if (BeautifulProminenceQuestBuilder.IsBeautifulProminence(data))
                return BeautifulProminenceQuestBuilder.Build(data, starId, seed, 0, _questBuilderContext);

            var builder = new QuestBuilder(data, starId, seed, _questBuilderContext);
            return builder.Build();
        }
    }
}
