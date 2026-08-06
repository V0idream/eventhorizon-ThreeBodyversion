using System;
using System.Collections.Generic;
using System.Linq;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;
using Services.Localization;
using UnityEngine;

namespace Domain.Quests
{
    /// <summary>
    /// Runtime-built three-stage storyline for "Beautiful Prominence". The
    /// offer is created alongside Mother's Tears, while generated objectives
    /// and exploration progress are persisted per save seed.
    /// </summary>
    public static class BeautifulProminenceQuestBuilder
    {
        public const int QuestId = 204;
        public const int OfferNodeId = 5;
        public const int ConfidentialFileItemId = 2001;
        public const int WanNianFengXueTechnologyId = 419;
        public const int WanNianFengXueBuildId = 94009;

        public static bool IsBeautifulProminence(QuestModel model)
        {
            return model != null && model.Id.Value == QuestId;
        }

        public static Quest Build(
            QuestModel model,
            int originStarId,
            int seed,
            int activeNodeId,
            IQuestBuilderContext context)
        {
            var nodes = new Dictionary<int, INode>();
            var terminal = new TerminalNode(160, NodeType.CompleteQuest);
            nodes.Add(terminal.Id, terminal);

            var reward = new LootNode(150, FixedLoot.Create(
                context.LootItemFactory.CreateBlueprint(
                    context.Database.GetTechnology(new ItemId<Technology>(WanNianFengXueTechnologyId))),
                context.LootItemFactory.CreateShip(
                    context.Database.GetShipBuild(
                        new ItemId<ShipBuild>(WanNianFengXueBuildId)))));
            reward.TargetNode = terminal;
            nodes.Add(reward.Id, reward);

            var finalDialogue = CreateTextNode(
                140,
                "$BeautifulProminence_Dialog3",
                "$BeautifulProminence_SaluteOption",
                reward);
            nodes.Add(finalDialogue.Id, finalDialogue);

            var historyDialogue = CreateTextNode(
                130,
                "$BeautifulProminence_Dialog2",
                "$BeautifulProminence_HistoryOption",
                finalDialogue);
            nodes.Add(historyDialogue.Id, historyDialogue);

            var namingDialogue = CreateTextNode(
                120,
                "$BeautifulProminence_Dialog1",
                "$BeautifulProminence_NameOption",
                historyDialogue);
            nodes.Add(namingDialogue.Id, namingDialogue);

            var prominenceImage = CreateImageNode(
                110,
                "$BeautifulProminence_Image04",
                namingDialogue);
            nodes.Add(prominenceImage.Id, prominenceImage);

            var solarDiveImage = CreateImageNode(
                100,
                "$BeautifulProminence_Image03",
                prominenceImage);
            nodes.Add(solarDiveImage.Id, solarDiveImage);

            var fileExplanationDialogue = CreateTextNode(
                95,
                "$BeautifulProminence_FileExplanation",
                "$BeautifulProminence_CoincidenceOption",
                solarDiveImage);
            nodes.Add(fileExplanationDialogue.Id, fileExplanationDialogue);

            var waitForNextRoute = new BeautifulProminenceNextRouteNode(
                90,
                "$BeautifulProminence_Stage3Progress",
                fileExplanationDialogue);
            nodes.Add(waitForNextRoute.Id, waitForNextRoute);

            var defenseTarget = new BeautifulProminenceDefenseNode(
                80,
                "$BeautifulProminence_Stage2Progress",
                waitForNextRoute);
            nodes.Add(defenseTarget.Id, defenseTarget);

            var decryptImage = CreateImageNode(
                70,
                "$BeautifulProminence_Image02",
                defenseTarget);
            nodes.Add(decryptImage.Id, decryptImage);

            var confidentialFile = new LootNode(60, FixedLoot.Create(
                context.LootItemFactory.CreateQuestItem(
                    context.Database.GetQuestItem(new ItemId<QuestItem>(ConfidentialFileItemId)),
                    1)));
            confidentialFile.TargetNode = decryptImage;
            nodes.Add(confidentialFile.Id, confidentialFile);

            var explorationTarget = new BeautifulProminenceExplorationNode(
                50,
                "$BeautifulProminence_Stage1Progress",
                () => GetOrCreateTarget(context, seed, "stage1HiveTarget", () =>
                    SelectNearestHiveStar(context, originStarId, seed)),
                () => GetProgressKey(context, seed, "stage1Hive"),
                1,
                confidentialFile);
            nodes.Add(explorationTarget.Id, explorationTarget);

            var signalImage = CreateImageNode(
                40,
                "$BeautifulProminence_Image01",
                explorationTarget);
            nodes.Add(signalImage.Id, signalImage);

            var offer = new BeautifulProminenceOfferNode(
                OfferNodeId,
                "$BeautifulProminence_Offer",
                signalImage);
            nodes.Add(offer.Id, offer);

            if (!nodes.TryGetValue(activeNodeId, out var activeNode))
                activeNode = offer;

            var quest = new Quest(model, originStarId, seed);
            quest.Initialize(activeNode);
            return quest;
        }

        private static TextNode CreateImageNode(int id, string message, INode target)
        {
            return CreateTextNode(id, message, "$BeautifulProminence_Advance", target);
        }

        private static TextNode CreateTextNode(int id, string message, string button, INode target)
        {
            var node = new TextNode(
                id,
                message,
                null,
                SpriteId.Empty,
                default,
                EmptyLoot.Instance,
                RequiredViewMode.Any);
            node.AddAction(button, Severity.Info, EmptyRequirements.Instance, target);
            return node;
        }

        private static int SelectNearestHiveStar(
            IQuestBuilderContext context,
            int centerStarId,
            int seed)
        {
            var random = new System.Random(seed ^ 0x425031);
            for (var distance = 1; distance <= 240; ++distance)
            {
                var candidates = context.StarMapDataProvider
                    .GetStarsAtDistance(centerStarId, distance)
                    .Where(context.StarMapDataProvider.HasHive)
                    .OrderBy(_ => random.Next())
                    .ThenBy(id => id)
                    .ToArray();
                if (candidates.Length > 0)
                    return candidates[0];
            }

			Debug.LogWarning("Beautiful Prominence: no hive found within 240 light-years; using the nearest deterministic fallback.");
			for (var distance = 241; distance <= 600; ++distance)
			{
				var candidates = context.StarMapDataProvider
					.GetStarsAtDistance(centerStarId, distance)
					.Where(context.StarMapDataProvider.HasHive)
					.OrderBy(id => id)
					.ToArray();
				if (candidates.Length > 0)
					return candidates[0];
			}

			return centerStarId;
        }

        private static int GetOrCreateTarget(
            IQuestBuilderContext context,
            int seed,
            string stage,
            Func<int> factory)
        {
            var key = GetProgressKey(context, seed, stage);
            if (PlayerPrefs.HasKey(key))
                return PlayerPrefs.GetInt(key);
            var target = factory();
            PlayerPrefs.SetInt(key, target);
            PlayerPrefs.Save();
            return target;
        }

        private static string GetProgressKey(IQuestBuilderContext context, int seed, string stage)
        {
            return $"ThreeBody.BeautifulProminence.{context.GameDataProvider.GameSeed}.{seed}.{stage}";
        }

        private sealed class FixedLoot : ILoot
        {
            private FixedLoot(IEnumerable<LootItem> items)
            {
                Items = items?.ToArray() ?? Array.Empty<LootItem>();
            }

            public IEnumerable<LootItem> Items { get; }
            public bool CanBeRemoved => true;

            public static ILoot Create(params LootItem[] items)
            {
                return new FixedLoot(items);
            }
        }
    }

    internal sealed class BeautifulProminenceOfferNode : INode
    {
        public BeautifulProminenceOfferNode(int id, string text, INode acceptedTarget)
        {
            Id = id;
            _text = text;
            _acceptedTarget = acceptedTarget;
        }

        public int Id { get; }
        public NodeType Type => NodeType.Condition;
        public bool ActionRequired => false;
        public string GetRequirementsText(ILocalization localization) => localization.GetString(_text);
        public bool TryGetBeacons(ICollection<int> beacons) => false;
        public void Initialize() { }
        public bool TryProceed(out INode target) { target = null; return false; }
        public bool TryInvokeAction(IQuestActionProcessor processor) => false;

        public bool TryProcessEvent(IQuestEventData data, out INode target)
        {
            target = data.Type == QuestEventType.BeautifulProminenceAccepted ? _acceptedTarget : null;
            return target != null;
        }

        private readonly string _text;
        private readonly INode _acceptedTarget;
    }

    internal sealed class BeautifulProminenceExplorationNode : INode
    {
        public BeautifulProminenceExplorationNode(
            int id,
            string progressText,
            Func<int> target,
            Func<string> progressKey,
            int requiredScans,
            INode completedTarget)
        {
            Id = id;
            _progressText = progressText;
            _targetFactory = target;
            _progressKeyFactory = progressKey;
            _requiredScans = requiredScans;
            _completedTarget = completedTarget;
        }

        public int Id { get; }
        public NodeType Type => NodeType.Condition;
        public bool ActionRequired => false;

        public string GetRequirementsText(ILocalization localization)
        {
            EnsureState();
            return localization.GetString(_progressText, _completedScans, _requiredScans);
        }

        public bool TryGetBeacons(ICollection<int> beacons)
        {
            EnsureState();
            if (_completedScans < _requiredScans) beacons.Add(_target);
            return _completedScans < _requiredScans;
        }

        public void Initialize() => EnsureState();

        public bool TryProceed(out INode target)
        {
            EnsureState();
            target = _completedScans >= _requiredScans ? _completedTarget : this;
            return target != this;
        }

        public bool TryProcessEvent(IQuestEventData data, out INode target)
        {
            EnsureState();
            target = this;
            if (data.Type != QuestEventType.ExplorationHiveCompleted)
                return false;

            var star = data as StarEventData;
            if (star == null || star.StarId != _target || _completedScans >= _requiredScans)
                return false;

            _completedScans++;
            PlayerPrefs.SetInt(_progressKey, _completedScans);
            PlayerPrefs.Save();
            if (_completedScans >= _requiredScans)
                target = _completedTarget;
            return true;
        }

        public bool TryInvokeAction(IQuestActionProcessor processor) => false;

        private void EnsureState()
        {
            if (_initialized) return;
            _target = _targetFactory();
            _progressKey = _progressKeyFactory();
            _completedScans = Mathf.Clamp(PlayerPrefs.GetInt(_progressKey, 0), 0, _requiredScans);
            _initialized = true;
        }

        private readonly string _progressText;
        private readonly Func<int> _targetFactory;
        private readonly Func<string> _progressKeyFactory;
        private readonly int _requiredScans;
        private readonly INode _completedTarget;
        private bool _initialized;
        private int _target;
        private int _completedScans;
        private string _progressKey;
    }

    internal sealed class BeautifulProminenceDefenseNode : INode
    {
        public BeautifulProminenceDefenseNode(
            int id,
            string progressText,
            INode completedTarget)
        {
            Id = id;
            _progressText = progressText;
            _completedTarget = completedTarget;
        }

        public int Id { get; }
        public NodeType Type => NodeType.Condition;
        public bool ActionRequired => false;
        public string GetRequirementsText(ILocalization localization) => localization.GetString(_progressText);

        public bool TryGetBeacons(ICollection<int> beacons) => false;

        public void Initialize() { }

        public bool TryProceed(out INode target)
        {
            target = this;
            return false;
        }

        public bool TryProcessEvent(IQuestEventData data, out INode target)
        {
            target = this;
            if (data.Type == QuestEventType.StarbaseDefenseCompleted)
            {
                target = _completedTarget;
                return true;
            }
            return false;
        }

        // This node only observes the normal starbase-defense completion event.
        // It must not start another combat or replace the enemy fleet assembled
        // by the ordinary starbase interface.
        public bool TryInvokeAction(IQuestActionProcessor processor) => false;

        private readonly string _progressText;
        private readonly INode _completedTarget;
    }

    internal sealed class BeautifulProminenceNextRouteNode : INode
    {
        public BeautifulProminenceNextRouteNode(int id, string progressText, INode completedTarget)
        {
            Id = id;
            _progressText = progressText;
            _completedTarget = completedTarget;
        }

        public int Id { get; }
        public NodeType Type => NodeType.Condition;
        public bool ActionRequired => false;
        public string GetRequirementsText(ILocalization localization) => localization.GetString(_progressText);
        public bool TryGetBeacons(ICollection<int> beacons) => false;
        public void Initialize() { }
        public bool TryProceed(out INode target) { target = this; return false; }
        public bool TryInvokeAction(IQuestActionProcessor processor) => false;

        public bool TryProcessEvent(IQuestEventData data, out INode target)
        {
            target = data.Type == QuestEventType.ArrivedAtStarSystem ? _completedTarget : this;
            return target != this;
        }

        private readonly string _progressText;
        private readonly INode _completedTarget;
    }
}
