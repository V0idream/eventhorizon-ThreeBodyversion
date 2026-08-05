using System;
using System.Collections.Generic;
using System.Linq;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;
using Services.Localization;
using UnityEngine;
using DatabaseComponent = GameDatabase.DataModel.Component;

namespace Domain.Quests
{
    /// <summary>
    /// Builds the multi-stage "Mother's Tears" storyline with deterministic,
    /// save-stable target systems.  The active node id remains in the normal
    /// quest save data, while each stage stores its generated beacon list in a
    /// game-seed-scoped PlayerPrefs entry so reloading cannot replace already
    /// assigned objectives with new systems.
    /// </summary>
    public static class MothersTearsQuestBuilder
    {
        public const int QuestId = 203;
        public const int OfferNodeId = 5;

        public static bool IsMothersTears(QuestModel model)
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

            var waterdropBlueprint = new LootNode(150, FixedLoot.Create(
                context.LootItemFactory.CreateBlueprint(
                    context.Database.GetTechnology(new ItemId<Technology>(417)))));
            waterdropBlueprint.TargetNode = terminal;
            nodes.Add(waterdropBlueprint.Id, waterdropBlueprint);

            var stage4Battle = new MothersTearsBattleNode(
                140,
                "$MothersTears_Stage4Progress",
                () => GetOrCreateSingleTarget(context, seed, "stage4", () =>
                    SelectUnoccupiedStar(context, context.PlayerDataProvider.CurrentStar.Id, seed ^ 0x4D5434)),
                starId => CreateEnemyData(context, starId, seed ^ 0x4D5434,
                    1145140),
                waterdropBlueprint);
            stage4Battle.BindPlayer(context.PlayerDataProvider);
            nodes.Add(stage4Battle.Id, stage4Battle);

            var stage4Image = CreateImageNode(130, "$MothersTears_Image06", stage4Battle);
            nodes.Add(stage4Image.Id, stage4Image);

            var waterdropReward = new LootNode(120, FixedLoot.Create(
                context.LootItemFactory.CreateShip(
                    context.Database.GetShipBuild(new ItemId<ShipBuild>(417)))));
            waterdropReward.TargetNode = stage4Image;
            nodes.Add(waterdropReward.Id, waterdropReward);

            var captureStage3Station = new MothersTearsCaptureTargetNode(
                110,
                "$MothersTears_Stage3Capture",
                () => GetOrCreateSingleTarget(context, seed, "stage3", () =>
                    SelectNearestTrisolarisStarbases(
                        context,
                        context.PlayerDataProvider.CurrentStar.Id,
                        1,
                        false).DefaultIfEmpty(-1).First()),
                waterdropReward);
            captureStage3Station.BindStarMap(context.StarMapDataProvider);
            nodes.Add(captureStage3Station.Id, captureStage3Station);

            var stage3Battle = new MothersTearsBattleNode(
                100,
                "$MothersTears_Stage3Progress",
                () => GetOrCreateSingleTarget(context, seed, "stage3", () =>
                    SelectNearestTrisolarisStarbases(
                        context,
                        context.PlayerDataProvider.CurrentStar.Id,
                        1,
                        false).DefaultIfEmpty(-1).First()),
                starId => CreateEnemyData(context, starId, seed ^ 0x4D5433,
                    114514,
                    1145142, 1145142,
                    417, 417, 417),
                captureStage3Station);
            stage3Battle.BindPlayer(context.PlayerDataProvider);
            nodes.Add(stage3Battle.Id, stage3Battle);

            var stage3Image = CreateImageNode(90, "$MothersTears_Image05", stage3Battle);
            nodes.Add(stage3Image.Id, stage3Image);

            var simBlueprint = new LootNode(80, FixedLoot.Create(
                context.LootItemFactory.CreateBlueprint(
                    context.Database.GetTechnology(new ItemId<Technology>(390)))));
            simBlueprint.TargetNode = stage3Image;
            nodes.Add(simBlueprint.Id, simBlueprint);

            var stage2Objectives = new MothersTearsCaptureTargetsNode(
                70,
                "$MothersTears_Stage2Progress",
                () => GetOrCreateTargets(context, seed, "stage2", 5, () =>
                    SelectNearestTrisolarisStarbases(
                        context,
                        context.PlayerDataProvider.CurrentStar.Id,
                        5,
                        false)),
                simBlueprint);
            stage2Objectives.BindStarMap(context.StarMapDataProvider);
            nodes.Add(stage2Objectives.Id, stage2Objectives);

            var stage2BattleImage = CreateImageNode(60, "$MothersTears_Image04", stage2Objectives);
            nodes.Add(stage2BattleImage.Id, stage2BattleImage);

            var stage2IntelImage = CreateImageNode(50, "$MothersTears_Image03", stage2BattleImage);
            nodes.Add(stage2IntelImage.Id, stage2IntelImage);

            var simReward = new LootNode(40, FixedLoot.Create(
                context.LootItemFactory.CreateComponent(
                    context.Database.GetComponent(new ItemId<DatabaseComponent>(314)), 5)));
            simReward.TargetNode = stage2IntelImage;
            nodes.Add(simReward.Id, simReward);

            var stage1Objectives = new MothersTearsCaptureTargetsNode(
                30,
                "$MothersTears_Stage1Progress",
                () => GetOrCreateTargets(context, seed, "stage1", 3, () =>
                    SelectStage1Targets(context, originStarId, seed, 3)),
                simReward);
            stage1Objectives.BindStarMap(context.StarMapDataProvider);
            nodes.Add(stage1Objectives.Id, stage1Objectives);

            var declarationImage = CreateImageNode(20, "$MothersTears_Image02", stage1Objectives);
            nodes.Add(declarationImage.Id, declarationImage);

            var openingImage = CreateImageNode(10, "$MothersTears_Image01", declarationImage);
            nodes.Add(openingImage.Id, openingImage);

            var offer = new MothersTearsOfferNode(
                OfferNodeId,
                "$MothersTears_Offer",
                openingImage);
            nodes.Add(offer.Id, offer);

            if (!nodes.TryGetValue(activeNodeId, out var activeNode))
                activeNode = offer;

            var quest = new Quest(model, originStarId, seed);
            quest.Initialize(activeNode);
            return quest;
        }

        private static TextNode CreateImageNode(int id, string message, INode target)
        {
            var node = new TextNode(
                id,
                message,
                null,
                SpriteId.Empty,
                default,
                EmptyLoot.Instance,
                RequiredViewMode.Any);
            node.AddAction("$MothersTears_Advance", Severity.Info, EmptyRequirements.Instance, target);
            return node;
        }

        private static QuestEnemyData CreateEnemyData(
            IQuestBuilderContext context,
            int starId,
            int seed,
            params int[] shipBuildIds)
        {
            var level = Math.Max(1, context.StarMapDataProvider.GetStarData(starId).Level);
            var ships = shipBuildIds
                .Select(id => context.Database.GetShipBuild(new ItemId<ShipBuild>(id)))
                .Where(item => item != null && item != ShipBuild.DefaultValue)
                .ToArray();

            return new QuestEnemyData
            {
                EnemyFleet = ships,
                Rules = context.Database.CombatSettings.DefaultCombatRules,
                StarLevel = level,
                EnemyLevel = level,
                Seed = seed,
            };
        }

        private static int[] SelectStage1Targets(
            IQuestBuilderContext context,
            int centerStarId,
            int seed,
            int count)
        {
            var candidates = GetTrisolarisStarbases(context, centerStarId, 100, false);
            if (candidates.Count < count)
                candidates = GetTrisolarisStarbases(context, centerStarId, 180, false);
            if (candidates.Count < count)
                candidates = GetTrisolarisStarbases(context, centerStarId, 300, false);

            var random = new System.Random(seed ^ 0x4D5431);
            return candidates
                .OrderBy(id => GameModel.StarLayout.Distance(centerStarId, id))
                .ThenBy(_ => random.Next())
                .ThenBy(id => id)
                .Take(count)
                .ToArray();
        }

        private static int[] SelectNearestTrisolarisStarbases(
            IQuestBuilderContext context,
            int centerStarId,
            int count,
            bool includeCaptured)
        {
            var candidates = GetTrisolarisStarbases(context, centerStarId, 120, includeCaptured);
            if (candidates.Count < count)
                candidates = GetTrisolarisStarbases(context, centerStarId, 220, includeCaptured);
            if (candidates.Count < count)
                candidates = GetTrisolarisStarbases(context, centerStarId, 350, includeCaptured);

            return candidates
                .OrderBy(id => GameModel.StarLayout.Distance(centerStarId, id))
                .ThenBy(id => id)
                .Take(count)
                .ToArray();
        }

        private static List<int> GetTrisolarisStarbases(
            IQuestBuilderContext context,
            int centerStarId,
            int maxDistance,
            bool includeCaptured)
        {
            return context.StarMapDataProvider
                .GetRegionsNearby(centerStarId, 0, maxDistance)
                .Where(region => region != null &&
                                 region.HomeStarId != centerStarId &&
                                 region.Faction != null &&
                                 region.Faction.Id.Value == 22 &&
                                 (includeCaptured || !region.IsCaptured))
                .Select(region => region.HomeStarId)
                .Distinct()
                .ToList();
        }

        private static int SelectUnoccupiedStar(
            IQuestBuilderContext context,
            int centerStarId,
            int seed)
        {
            var random = new System.Random(seed);
            for (var attempt = 0; attempt < 512; ++attempt)
            {
                var distance = random.Next(6, 81);
                var candidate = context.StarMapDataProvider.RandomStarAtDistance(centerStarId, distance, random);
                if (candidate == centerStarId)
                    continue;

                var region = context.StarMapDataProvider.GetStarData(candidate).Region;
                if (region == null || region.IsHome)
                    continue;

                if (region.Faction == null || region.Faction.Id.Value == 0)
                    return candidate;
            }

            return context.StarMapDataProvider.RandomStarAtDistance(centerStarId, 40, random);
        }

        private static int[] GetOrCreateTargets(
            IQuestBuilderContext context,
            int seed,
            string stage,
            int expectedCount,
            Func<int[]> factory)
        {
            var key = GetTargetKey(context, seed, stage);
            var stored = ParseTargets(PlayerPrefs.GetString(key, string.Empty));
            if (stored.Length == expectedCount)
                return stored;

            var created = (factory() ?? Array.Empty<int>())
                .Where(id => id >= 0)
                .Distinct()
                .Take(expectedCount)
                .ToArray();
            PlayerPrefs.SetString(key, string.Join(",", created));
            PlayerPrefs.Save();
            return created;
        }

        private static int GetOrCreateSingleTarget(
            IQuestBuilderContext context,
            int seed,
            string stage,
            Func<int> factory)
        {
            return GetOrCreateTargets(context, seed, stage, 1, () => new[] { factory() })
                .FirstOrDefault();
        }

        private static string GetTargetKey(IQuestBuilderContext context, int seed, string stage)
        {
            // Beta8 deliberately changes the key namespace so active saves made
            // by earlier builds discard their 300-2000 light-year destinations
            // and regenerate targets with the new nearby-station rules.
            return $"ThreeBody.MothersTears.Beta8.{context.GameDataProvider.GameSeed}.{seed}.{stage}";
        }

        private static int[] ParseTargets(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<int>();

            return value.Split(',')
                .Select(item => int.TryParse(item, out var id) ? id : -1)
                .Where(id => id >= 0)
                .Distinct()
                .ToArray();
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

    internal sealed class MothersTearsOfferNode : INode
    {
        public MothersTearsOfferNode(int id, string requirementsText, INode acceptedTarget)
        {
            Id = id;
            _requirementsText = requirementsText;
            _acceptedTarget = acceptedTarget;
        }

        public int Id { get; }
        public NodeType Type => NodeType.Condition;
        public bool ActionRequired => false;

        public string GetRequirementsText(ILocalization localization)
        {
            return localization.GetString(_requirementsText);
        }

        public bool TryGetBeacons(ICollection<int> beacons) => false;
        public void Initialize() { }
        public bool TryProceed(out INode target)
        {
            target = null;
            return false;
        }

        public bool TryProcessEvent(IQuestEventData data, out INode target)
        {
            target = data.Type == QuestEventType.MothersTearsAccepted ? _acceptedTarget : null;
            return target != null;
        }

        public bool TryInvokeAction(IQuestActionProcessor processor) => false;

        private readonly string _requirementsText;
        private readonly INode _acceptedTarget;
    }

    internal sealed class MothersTearsCaptureTargetsNode : INode
    {
        public MothersTearsCaptureTargetsNode(
            int id,
            string progressText,
            Func<int[]> targets,
            INode targetNode)
        {
            Id = id;
            _progressText = progressText;
            _targetsFactory = targets;
            _targetNode = targetNode;
        }

        public int Id { get; }
        public NodeType Type => NodeType.Condition;
        public bool ActionRequired => false;

        public string GetRequirementsText(ILocalization localization)
        {
            EnsureTargets();
            return localization.GetString(_progressText, CompletedCount, _targets.Length);
        }

        public bool TryGetBeacons(ICollection<int> beacons)
        {
            EnsureTargets();
            foreach (var id in _targets)
                if (!_starMap.GetStarData(id).Region.IsCaptured)
                    beacons.Add(id);
            return _targets.Length > 0;
        }

        public void Initialize()
        {
            EnsureTargets();
        }

        public bool TryProceed(out INode target)
        {
            EnsureTargets();
            target = IsComplete ? _targetNode : this;
            return IsComplete;
        }

        public bool TryProcessEvent(IQuestEventData data, out INode target)
        {
            target = this;
            if (data.Type != QuestEventType.NewStarSystemSecured && data.Type != QuestEventType.Timer)
                return false;

            if (!IsComplete)
                return data.Type == QuestEventType.NewStarSystemSecured;

            target = _targetNode;
            return true;
        }

        public bool TryInvokeAction(IQuestActionProcessor processor) => false;

        public void BindStarMap(IStarMapDataProvider starMap)
        {
            _starMap = starMap;
        }

        private void EnsureTargets()
        {
            _targets ??= _targetsFactory() ?? Array.Empty<int>();
            if (_starMap == null)
                throw new InvalidOperationException("Mother's Tears objective has no star-map provider.");
        }

        private int CompletedCount => _targets.Count(id => _starMap.GetStarData(id).Region.IsCaptured);
        private bool IsComplete => _targets.Length == 0 || CompletedCount >= _targets.Length;

        private readonly string _progressText;
        private readonly Func<int[]> _targetsFactory;
        private readonly INode _targetNode;
        private int[] _targets;
        private IStarMapDataProvider _starMap;
    }

    internal sealed class MothersTearsCaptureTargetNode : INode
    {
        public MothersTearsCaptureTargetNode(
            int id,
            string progressText,
            Func<int> target,
            INode targetNode)
        {
            Id = id;
            _progressText = progressText;
            _targetFactory = target;
            _targetNode = targetNode;
        }

        public int Id { get; }
        public NodeType Type => NodeType.CaptureStarBase;
        public bool ActionRequired { get; private set; }

        public string GetRequirementsText(ILocalization localization)
        {
            return localization.GetString(_progressText);
        }

        public bool TryGetBeacons(ICollection<int> beacons)
        {
            EnsureTarget();
            if (!IsComplete)
                beacons.Add(_target);
            return _target >= 0;
        }

        public void Initialize()
        {
            EnsureTarget();
            ActionRequired = !IsComplete;
        }

        public bool TryProceed(out INode target)
        {
            target = IsComplete ? _targetNode : this;
            return IsComplete;
        }

        public bool TryProcessEvent(IQuestEventData data, out INode target)
        {
            target = this;
            if (data.Type != QuestEventType.NewStarSystemSecured && data.Type != QuestEventType.Timer)
                return false;

            if (IsComplete)
            {
                target = _targetNode;
                return true;
            }

            return data.Type == QuestEventType.NewStarSystemSecured;
        }

        public bool TryInvokeAction(IQuestActionProcessor processor)
        {
            EnsureTarget();
            if (IsComplete)
                return false;

            processor.CaptureStarBase(_target, true);
            ActionRequired = false;
            return true;
        }

        private void EnsureTarget()
        {
            if (_targetInitialized)
                return;
            _target = _targetFactory();
            _targetInitialized = true;
        }

        private bool IsComplete => _target < 0 || _starMap.GetStarData(_target).Region.IsCaptured;

        public void BindStarMap(IStarMapDataProvider starMap)
        {
            _starMap = starMap;
        }

        private readonly string _progressText;
        private readonly Func<int> _targetFactory;
        private readonly INode _targetNode;
        private IStarMapDataProvider _starMap;
        private int _target = -1;
        private bool _targetInitialized;
    }

    internal sealed class MothersTearsBattleNode : INode
    {
        public MothersTearsBattleNode(
            int id,
            string progressText,
            Func<int> target,
            Func<int, QuestEnemyData> enemyFactory,
            INode victoryNode)
        {
            Id = id;
            _progressText = progressText;
            _targetFactory = target;
            _enemyFactory = enemyFactory;
            _victoryNode = victoryNode;
        }

        public int Id { get; }
        public NodeType Type => NodeType.AttackFleet;
        public bool ActionRequired { get; private set; }

        public string GetRequirementsText(ILocalization localization)
        {
            return localization.GetString(_progressText);
        }

        public bool TryGetBeacons(ICollection<int> beacons)
        {
            EnsureTarget();
            if (_target >= 0)
                beacons.Add(_target);
            return _target >= 0;
        }

        public void Initialize()
        {
            EnsureTarget();
            ActionRequired = _player.CurrentStar.Id == _target;
        }

        public bool TryProceed(out INode target)
        {
            target = this;
            return false;
        }

        public bool TryProcessEvent(IQuestEventData data, out INode target)
        {
            target = this;
            if (data.Type == QuestEventType.ArrivedAtStarSystem)
            {
                var wasRequired = ActionRequired;
                ActionRequired = _player.CurrentStar.Id == _target;
                return wasRequired != ActionRequired || ActionRequired;
            }

            if (data.Type != QuestEventType.CombatCompleted || !_battleInProgress)
                return false;

            _battleInProgress = false;
            var result = (CombatEventData)data;
            if (result.IsVictory)
            {
                ActionRequired = false;
                target = _victoryNode;
            }
            else
            {
                ActionRequired = _player.CurrentStar.Id == _target;
            }

            return true;
        }

        public bool TryInvokeAction(IQuestActionProcessor processor)
        {
            EnsureTarget();
            if (!ActionRequired || _player.CurrentStar.Id != _target)
                return false;

            _battleInProgress = true;
            ActionRequired = false;
            processor.StartMothersTearsCombat(_enemyFactory(_target), EmptyLoot.Instance, _target);
            return true;
        }

        public void BindPlayer(IPlayerDataProvider player)
        {
            _player = player;
        }

        private void EnsureTarget()
        {
            if (_targetInitialized)
                return;
            _target = _targetFactory();
            _targetInitialized = true;
        }

        private readonly string _progressText;
        private readonly Func<int> _targetFactory;
        private readonly Func<int, QuestEnemyData> _enemyFactory;
        private readonly INode _victoryNode;
        private IPlayerDataProvider _player;
        private int _target = -1;
        private bool _targetInitialized;
        private bool _battleInProgress;
    }
}
