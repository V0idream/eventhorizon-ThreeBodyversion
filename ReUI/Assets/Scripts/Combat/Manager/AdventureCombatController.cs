using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Component.Ship;
using Combat.Component.Unit.Classification;
using Combat.Domain;
using Combat.Factory;
using Combat.Scene;
using Combat.Unit;
using Combat.Unit.Object;
using Constructor;
using Constructor.Extensions;
using Constructor.Ships;
using Economy;
using Economy.ItemType;
using Economy.Products;
using Game.Adventure;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameServices.Gui;
using Gui.Combat;
using Gui.Common;
using Services.GameApplication;
using Services.Localization;
using Services.Messenger;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using CombatShip = Combat.Component.Ship.IShip;

namespace Combat.Manager
{
    /// <summary>
    /// Adventure-specific runtime layer over the regular CombatScene.  It owns
    /// wave/supply spawning and progression, while CombatManager keeps all normal
    /// flight, weapon, radar and pause-menu behaviour.
    /// </summary>
    public sealed class AdventureCombatController : IInitializable, ITickable, IDisposable
    {
        [Inject]
        public AdventureCombatController(
            AdventureRun run,
            AdventureCombatModel model,
            IDatabase database,
            CombatManager combatManager,
            IScene scene,
            SpaceObjectFactory spaceObjectFactory,
            RadarPanel radarPanel,
            IMessenger messenger,
            IApplication application,
            ILocalization localization,
            GuiHelper guiHelper,
            ItemTypeFactory itemTypeFactory)
        {
            _run = run;
            _model = model;
            _database = database;
            _combatManager = combatManager;
            _scene = scene;
            _spaceObjectFactory = spaceObjectFactory;
            _radarPanel = radarPanel;
            _messenger = messenger;
            _application = application;
            _localization = localization;
            _guiHelper = guiHelper;
            _itemTypeFactory = itemTypeFactory;
            _random = new System.Random(run.Seed ^ Environment.TickCount);
        }

        public void Initialize()
        {
            if (!_run.Active) return;

            _messenger.AddListener<CombatShip>(EventType.CombatShipDestroyed, OnShipDestroyed);
            CreateHud();
            StartNextWave();
            SpawnSupply();
            _supplyTimer = SupplySpawnInterval * 0.5f;
            RefreshHud(true);
        }

        public void Dispose()
        {
            _messenger.RemoveListener<CombatShip>(EventType.CombatShipDestroyed, OnShipDestroyed);
            if (_upgradePaused)
            {
                _application.Resume(this);
                _upgradePaused = false;
            }
            if (_waveLootPaused)
            {
                _application.Resume(this);
                _waveLootPaused = false;
            }
            if (_finalLootPaused)
            {
                _application.Resume(this);
                _finalLootPaused = false;
            }
            if (_upgradeOverlay != null) UnityEngine.Object.Destroy(_upgradeOverlay);
            if (_hud != null) UnityEngine.Object.Destroy(_hud);
            if (_supplySearchPrompt != null) UnityEngine.Object.Destroy(_supplySearchPrompt);
            if (_supplyCollectedAnimation != null) UnityEngine.Object.Destroy(_supplyCollectedAnimation);
        }

        public void Tick()
        {
            if (!_run.Active || _ending) return;

            if (_run.Defeat)
            {
                FinishDefeat();
                return;
            }

            TickSupplyCollectedAnimation();

            if (_run.BossStage)
            {
                if (!_bossStageInitialized && !_startBossAfterWaveRewards && !_waveLootPaused && !_waveCompletionPending)
                    StartBossStage();
            }
            else
            {
                _bossStageInitialized = false;
                _supplyTimer += Time.deltaTime;

                if (_betweenWaves && !_waveLootPaused && _upgradeOverlay == null)
                {
                    _interWaveTimer += Time.deltaTime;
                    if (_interWaveTimer >= InterWaveDelay)
                        StartNextWave();
                }

                if (_supplyTimer >= SupplySpawnInterval && _supplies.Count < MaxSupplies)
                {
                    _supplyTimer = 0f;
                    SpawnSupply();
                }
            }

            TickSupplySearch();
            _hudTimer += Time.unscaledDeltaTime;
            if (_hudTimer >= 0.25f)
            {
                _hudTimer = 0f;
                RefreshHud(false);
            }
        }

        private void StartNextWave()
        {
            if (_run.BossStage || _ending) return;

            _betweenWaves = false;
            _interWaveTimer = 0f;
            _waveNumber++;
            SpawnEnemyWave(GetWaveEnemyCount(_waveNumber), GetWaveEnemyLevel(_waveNumber));

            // Broken/missing faction data must not leave Adventure permanently
            // stuck between waves. Retrying after the normal inter-wave delay
            // is safer than continuously spawning in Tick().
            if (_waveEnemies.Count == 0)
                _betweenWaves = true;
        }

        private void SpawnEnemyWave(int count, int level)
        {
            if (count <= 0 || _run.BossStage) return;
            var playerRank = _run.OwnedShips.Count == 0 ? 0 : _run.OwnedShips.Max(AdventureRun.GetAdventureRank);
            for (var i = 0; i < count; i++)
            {
                var build = _run.GetRandomEnemyBuild(_random, Mathf.Clamp(playerRank, 0, 3));
                if (build == null) continue;
                var data = new CommonShip(build, _database);
                data.SetLevel(level);
                var info = _model.Enemies.Add(data);
                if (info != null && info.Status == ShipStatus.Ready)
                {
                    _waveEnemies.Add(info);
                    _combatManager.CreateShip(info);
                }
            }
        }

        internal static int GetWaveEnemyCount(int wave)
        {
            return Mathf.Min(10, 5 + Mathf.Max(0, wave - 1) / 5);
        }

        internal static int GetWaveEnemyLevel(int wave)
        {
            return Mathf.Min(200, Mathf.Max(1, wave) * 10);
        }

        private void SpawnSupply()
        {
            if (_run.BossStage || _supplies.Count >= MaxSupplies) return;
            var position = _scene.FindFreePlace(35f, UnitSide.Neutral);
            var size = 4.5f + (float)_random.NextDouble() * 2f;
            var unit = _spaceObjectFactory.CreatePlanetaryFloatingContainer(position, size,
                new Color(0.45f, 0.82f, 1f, 0.95f));
            _radarPanel.AddBeacon(unit);
            _supplies.Add(new SupplyNode(unit));
        }

        private void TickSupplySearch()
        {
            var player = _scene.PlayerShip;
            if (player == null || !player.IsActive())
            {
                _searching = null;
                SetSupplySearchPrompt(null);
                return;
            }

            SupplyNode nearest = null;
            var nearestDistance = float.MaxValue;
            foreach (var supply in _supplies.ToArray())
            {
                if (supply.Unit == null || !supply.Unit.IsActive())
                {
                    _supplies.Remove(supply);
                    continue;
                }

                var range = Mathf.Max(10f, (player.Body.WorldScale() + supply.Unit.Body.WorldScale()) * 0.9f);
                var distance = BattlefieldGeometry.SqrDistance(player.Body.WorldPosition(), supply.Unit.Body.WorldPosition());
                if (distance <= range * range && distance < nearestDistance)
                {
                    nearest = supply;
                    nearestDistance = distance;
                }
            }

            if (nearest == null)
            {
                if (_searching != null) _searching.Progress = 0f;
                _searching = null;
                SetSupplySearchPrompt(null);
                return;
            }

            if (!ReferenceEquals(_searching, nearest))
            {
                if (_searching != null) _searching.Progress = 0f;
                _searching = nearest;
            }

            nearest.Progress += Time.deltaTime;
            SetSupplySearchPrompt(nearest);
            if (nearest.Progress < SupplySearchTime) return;

            SetSupplySearchPrompt(null);
            CollectSupply(nearest);
            _searching = null;
        }

        private void CollectSupply(SupplyNode supply)
        {
            if (supply == null || !_supplies.Remove(supply)) return;
            supply.Unit?.Vanish();
            _run.RegisterSupply();

            var roll = _random.NextDouble();
            if (roll < 0.52)
            {
                var credits = _random.Next(800, 2601) * (1 + Math.Max(0, _run.OwnedShips.Max(AdventureRun.GetAdventureRank)));
                RecordWaveCredits(credits);
                ShowSupplyCollectedAnimation("物资采集完成\n+" + credits.ToString("N0") + " 信用点");
                NotifyPlayer("物资采集完成：获得 " + credits.ToString("N0") + " 信用点。");
            }
            else if (roll < 0.93)
            {
                var component = GrantTemporaryComponent();
                if (component)
                {
                    var componentName = _localization.GetString(component.Data.Name);
                    ShowSupplyCollectedAnimation("物资采集完成\n获得临时组件：" + componentName);
                    NotifyPlayer("物资采集完成：获得临时组件“" + componentName + "”。");
                }
                else
                {
                    ShowSupplyCollectedAnimation("物资采集完成\n未发现可用组件");
                    NotifyPlayer("物资采集完成，但没有发现可用组件。");
                }
            }
            else
            {
                if (OfferUpgrade())
                {
                    ShowSupplyCollectedAnimation("物资采集完成\n发现舰船升级");
                    NotifyPlayer("物资采集完成：发现舰船升级。");
                }
                else
                {
                    var component = GrantTemporaryComponent();
                    var componentName = component
                        ? _localization.GetString(component.Data.Name)
                        : "备用物资";
                    ShowSupplyCollectedAnimation("物资采集完成\n获得：" + componentName);
                    NotifyPlayer("物资采集完成：当前无可升级舰级，改为获得“" + componentName + "”。");
                }
            }
        }

        private void OnShipDestroyed(CombatShip ship)
        {
            if (!_run.Active || _ending || ship == null || ship.Type.Class != UnitClass.Ship ||
                ship.State != UnitState.Destroyed)
                return;

            if (ship.Type.Side == UnitSide.Player)
            {
                HandlePlayerDeath(ship);
                return;
            }

            if (ship.Type.Side != UnitSide.Enemy || _transitioningBoss)
                return;

            var info = _model.Enemies.GetInfo(ship);
            if (_bossInfo != null && ReferenceEquals(info, _bossInfo))
            {
                FinishVictory();
                return;
            }

            var wasWaveEnemy = info != null && _waveEnemies.Remove(info);
            _run.RegisterKill();
            if (_random.NextDouble() < UpgradeDropChance)
                OfferUpgrade();

            if (_random.NextDouble() < 0.56)
            {
                var rank = Math.Max(0, _run.OwnedShips.Max(AdventureRun.GetAdventureRank));
                var credits = _random.Next(250, 1001) * (rank + 1);
                RecordWaveCredits(credits);
            }
            else
            {
                GrantTemporaryComponent();
            }

            if (wasWaveEnemy && _waveEnemies.Count == 0)
                CompleteWave();
        }

        private void HandlePlayerDeath(CombatShip unit)
        {
            var deadInfo = _model.Player.GetInfo(unit);
            if (deadInfo == null) return;

            var wasBossStage = _run.BossStage;
            var removed = _run.ApplyDeathPenalty();
            foreach (var ship in removed)
                _model.Player.Remove(ship);
            _model.Player.Sync(_run.OwnedShips);

            // Ships below the removed top tier remain owned. If one of those was
            // the craft that died, restore it as a ready replacement rather than
            // silently losing an additional tier.
            foreach (var item in _model.Player.Ships)
            {
                if (item is Combat.Domain.ShipInfo concrete && item.Status == ShipStatus.Destroyed)
                    concrete.RestoreForNextActivation(1f);
            }

            if (_run.Defeat || !_run.OwnedShips.Any(item => item.Model.SizeClass == SizeClass.Frigate))
            {
                _run.MarkDefeat();
                return;
            }

            var removedRank = removed.Count == 0 ? -1 : removed.Max(AdventureRun.GetAdventureRank);
            NotifyPlayer("舰船损失：最高阶舰队已移除，当前降至 " + GetTierName(_run.OwnedShips.Max(AdventureRun.GetAdventureRank)));

            if (wasBossStage && !_run.BossStage)
            {
                AbortBossStage();
                _betweenWaves = true;
                _interWaveTimer = 0f;
                _supplyTimer = SupplySpawnInterval * 0.5f;
            }
        }

        private void RecordWaveCredits(int credits)
        {
            if (credits <= 0) return;
            _run.AwardCredits(credits);
            _waveCredits += credits;
        }

        private ComponentInfo GrantTemporaryComponent()
        {
            var component = _run.GetRandomTemporaryComponent(_random);
            if (!component) return ComponentInfo.Empty;
            _run.AddComponent(component);
            _waveComponents.Add(component);
            return component;
        }

        private bool OfferUpgrade()
        {
            if (_upgradeOverlay != null || _run.BossStage || !_run.Active)
                return false;
            var choices = _run.GetUpgradeChoices();
            if (choices.Count == 0)
                return false;

            var canvas = _radarPanel.GetComponentInParent<Canvas>();
            if (canvas == null) return false;

            _application.Pause(this);
            _upgradePaused = true;

            _upgradeOverlay = CreateUiObject("AdventureUpgradeChoice", canvas.transform, typeof(Image));
            var root = _upgradeOverlay.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.SetAsLastSibling();
            _upgradeOverlay.GetComponent<Image>().color = new Color(0.005f, 0.008f, 0.025f, 0.94f);

            var panel = CreateUiObject("Panel", root, typeof(Image));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.2f, 0.18f);
            panelRect.anchorMax = new Vector2(0.8f, 0.82f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = ThreeBodyUiPalette.Panel;

            var currentRank = _run.OwnedShips.Count == 0 ? -1 : _run.OwnedShips.Max(AdventureRun.GetAdventureRank);
            var targetRank = choices.Max(AdventureRun.GetAdventureRank);
            var fallbackFinal = targetRank <= currentRank;
            var title = CreateText("Title", panelRect,
                fallbackFinal
                    ? "获得升级组件 - 选择旗舰等价单位"
                    : "获得升级组件 - 选择下一可用舰级",
                30);
            title.rectTransform.anchorMin = new Vector2(0.05f, 0.82f);
            title.rectTransform.anchorMax = new Vector2(0.95f, 0.97f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;

            var scrollObject = CreateUiObject("ChoicesScroll", panelRect, typeof(Image), typeof(ScrollRect));
            var scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.08f, 0.1f);
            scrollRect.anchorMax = new Vector2(0.92f, 0.8f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;
            scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);

            var viewport = CreateUiObject("Viewport", scrollRect, typeof(Image), typeof(Mask));
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(6f, 6f);
            viewportRect.offsetMax = new Vector2(-6f, -6f);
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = CreateUiObject("Choices", viewportRect, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            foreach (var build in choices)
            {
                var buttonObject = CreateUiObject("UpgradeShip", contentRect, typeof(Image), typeof(Button), typeof(LayoutElement));
                buttonObject.GetComponent<Image>().color = ThreeBodyUiPalette.ButtonDim;
                var buttonLayout = buttonObject.GetComponent<LayoutElement>();
                buttonLayout.minHeight = 56f;
                buttonLayout.preferredHeight = 56f;
                var label = CreateText("Label", buttonObject.transform,
                    _localization.GetString(build.Ship.Name) + "  /  " +
                    (fallbackFinal ? "旗舰等价" : GetTierName(AdventureRun.GetAdventureRank(build))), 23);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = new Vector2(10f, 4f);
                label.rectTransform.offsetMax = new Vector2(-10f, -4f);
                var selected = build;
                buttonObject.GetComponent<Button>().onClick.AddListener(() => ChooseUpgrade(selected));
            }

            return true;
        }

        private void ChooseUpgrade(ShipBuild build)
        {
            var ship = _run.AddShip(build);
            if (ship != null)
                _model.Player.Add(ship);

            var enteredBossStage = _run.BossStage;
            CloseUpgradeOverlay();
            if (enteredBossStage)
            {
                _startBossAfterWaveRewards = true;
                CompleteWave(true);
            }
            else if (_waveCompletionPending)
            {
                CompleteWave();
            }
        }

        private void CloseUpgradeOverlay()
        {
            if (_upgradeOverlay != null)
                UnityEngine.Object.Destroy(_upgradeOverlay);
            _upgradeOverlay = null;
            if (_upgradePaused)
            {
                _application.Resume(this);
                _upgradePaused = false;
            }
        }

        private void CompleteWave(bool forceBossTransition = false)
        {
            if (_ending || _waveLootPaused) return;
            if (!forceBossTransition && _waveEnemies.Count > 0) return;

            if (_upgradeOverlay != null)
            {
                _waveCompletionPending = true;
                if (forceBossTransition)
                    _startBossAfterWaveRewards = true;
                return;
            }

            _waveCompletionPending = false;
            if (forceBossTransition)
                _startBossAfterWaveRewards = true;

            var products = BuildRewardProducts(_waveCredits, _waveComponents, 0);
            _waveCredits = 0;
            _waveComponents.Clear();

            if (products.Count == 0)
            {
                OnWaveLootClosed();
                return;
            }

            _waveLootPaused = true;
            _application.Pause(this);
            _guiHelper.ShowLootWindow(products, OnWaveLootClosed, requireContinue: true);
        }

        private void OnWaveLootClosed()
        {
            if (_waveLootPaused)
            {
                _application.Resume(this);
                _waveLootPaused = false;
            }

            if (_ending) return;
            if (_startBossAfterWaveRewards || _run.BossStage)
            {
                _startBossAfterWaveRewards = false;
                StartBossStage();
                return;
            }

            _betweenWaves = true;
            _interWaveTimer = 0f;
        }

        private List<IProduct> BuildRewardProducts(long credits, IEnumerable<ComponentInfo> components, int researchPoints)
        {
            var result = new List<IProduct>();
            if (credits > 0)
                result.Add(CommonProduct.Create(_itemTypeFactory.CreateCurrencyItem(Currency.Credits),
                    (int)Math.Min(int.MaxValue, credits)));

            if (researchPoints > 0 && _run.Faction != null)
                result.Add(CommonProduct.Create(_itemTypeFactory.CreateResearchItem(_run.Faction), researchPoints));

            if (components != null)
            {
                foreach (var group in components.Where(item => item).GroupBy(item => item.SerializeToInt64()))
                    result.Add(CommonProduct.Create(_itemTypeFactory.CreateComponentItem(group.First()), group.Count()));
            }

            return result;
        }

        private void StartBossStage()
        {
            if (_bossStageInitialized || !_run.BossStage || _ending) return;
            _bossStageInitialized = true;
            _transitioningBoss = true;

            foreach (var enemy in _model.Enemies.Ships.ToArray())
                enemy.Destroy();
            _model.Enemies.Ships.Clear();
            _waveEnemies.Clear();

            foreach (var supply in _supplies)
                supply.Unit?.Vanish();
            _supplies.Clear();

            foreach (var ally in _model.Allies.Ships.ToArray())
                ally.Destroy();
            _model.Allies.Ships.Clear();

            var activePlayerData = _scene.PlayerShip != null && _scene.PlayerShip.IsActive()
                ? _model.Player.GetInfo(_scene.PlayerShip)?.ShipData
                : null;
            foreach (var ship in _run.OwnedShips)
            {
                if (ReferenceEquals(ship, activePlayerData)) continue;
                // Use the same run-local ship data object in both fleet models.
                // CombatManager can then retire this AI copy if the player takes
                // manual control of that hull through the pause-menu selector.
                var ally = _model.Allies.Add(ship, collaborative: true);
                if (ally != null) _combatManager.CreateShip(ally);
            }

            var bossBuild = _run.GetBossBuild();
            if (bossBuild != null)
            {
                var bossShip = new CommonShip(bossBuild, _database);
                _bossInfo = _model.Enemies.Add(bossShip);
                if (_bossInfo != null) _combatManager.CreateShip(_bossInfo);
                NotifyPlayer("旗舰已获得：全舰队投入最终战，击败对应阵营旗舰！");
            }
            else
            {
                // A faction with no flagship build cannot produce a valid final
                // encounter. Treat the obtained flagship as completion rather
                // than trapping the player in an unwinnable run.
                FinishVictory();
            }

            _transitioningBoss = false;
        }

        private void AbortBossStage()
        {
            _transitioningBoss = true;
            _bossInfo?.Destroy();
            _bossInfo = null;
            foreach (var ally in _model.Allies.Ships.ToArray())
                ally.Destroy();
            _model.Allies.Ships.Clear();
            foreach (var enemy in _model.Enemies.Ships.ToArray())
                enemy.Destroy();
            _model.Enemies.Ships.Clear();
            _waveEnemies.Clear();
            _bossStageInitialized = false;
            _transitioningBoss = false;
        }

        private void FinishVictory()
        {
            if (_ending) return;
            _ending = true;
            var reward = _run.AwardVictoryRewards();
            if (reward == null)
            {
                _combatManager.Exit();
                return;
            }

            var products = BuildRewardProducts(reward.Credits, reward.Components, reward.ResearchPoints);
            if (products.Count == 0)
            {
                _combatManager.Exit();
                return;
            }

            _finalLootPaused = true;
            _application.Pause(this);
            _guiHelper.ShowLootWindow(products, () =>
            {
                if (_finalLootPaused)
                {
                    _application.Resume(this);
                    _finalLootPaused = false;
                }
                _combatManager.Exit();
            }, requireContinue: true);
        }

        private void FinishDefeat()
        {
            if (_ending) return;
            _ending = true;
            _run.MarkDefeat();
            _guiHelper.ShowMessageBox("冒险模式失败：已失去全部护卫舰。冒险中的临时舰船与组件不会影响正常存档。");
            _combatManager.Exit();
        }

        private void NotifyPlayer(string message)
        {
            var player = _scene.PlayerShip;
            if (player != null && player.IsActive())
                player.Broadcast(message, ThreeBodyUiPalette.AccentSoft);
            UnityEngine.Debug.Log("[Adventure] " + message);
        }

        private void CreateHud()
        {
            var canvas = _radarPanel.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _hud = CreateUiObject("AdventureHud", canvas.transform, typeof(Image));
            var rect = _hud.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.015f, 0.78f);
            rect.anchorMax = new Vector2(0.39f, 0.985f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _hud.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.08f, 0.72f);
            _hudText = CreateText("Text", rect, string.Empty, 20);
            _hudText.alignment = TextAnchor.UpperLeft;
            _hudText.rectTransform.anchorMin = Vector2.zero;
            _hudText.rectTransform.anchorMax = Vector2.one;
            _hudText.rectTransform.offsetMin = new Vector2(14f, 10f);
            _hudText.rectTransform.offsetMax = new Vector2(-14f, -10f);
            CreateSupplySearchPrompt(canvas);
            CreateSupplyCollectedAnimation(canvas);
        }

        private void CreateSupplySearchPrompt(Canvas canvas)
        {
            _supplySearchPrompt = CreateUiObject("AdventureSupplySearchPrompt", canvas.transform,
                typeof(Image), typeof(CanvasGroup));
            var rect = _supplySearchPrompt.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.33f, 0.12f);
            rect.anchorMax = new Vector2(0.67f, 0.19f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _supplySearchPrompt.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.09f, 0.92f);
            _supplySearchGroup = _supplySearchPrompt.GetComponent<CanvasGroup>();
            _supplySearchGroup.interactable = false;
            _supplySearchGroup.blocksRaycasts = false;

            _supplySearchText = CreateText("Text", rect, "正在采集物资  0%", 26);
            _supplySearchText.color = new Color(0.55f, 0.95f, 1f, 1f);
            _supplySearchText.fontStyle = FontStyle.Bold;
            _supplySearchText.rectTransform.anchorMin = Vector2.zero;
            _supplySearchText.rectTransform.anchorMax = Vector2.one;
            _supplySearchText.rectTransform.offsetMin = new Vector2(16f, 6f);
            _supplySearchText.rectTransform.offsetMax = new Vector2(-16f, -6f);
            _supplySearchPrompt.SetActive(false);
        }

        private void SetSupplySearchPrompt(SupplyNode supply)
        {
            if (_supplySearchPrompt == null)
                return;

            if (supply == null)
            {
                _supplySearchPrompt.SetActive(false);
                return;
            }

            var percent = Mathf.Clamp(Mathf.RoundToInt(supply.Progress / SupplySearchTime * 100f), 0, 100);
            if (_supplySearchText != null)
                _supplySearchText.text = "正在采集物资  " + percent + "%\n保持在蓝色物资箱附近";
            _supplySearchGroup.alpha = 1f;
            _supplySearchPrompt.SetActive(true);
            _supplySearchPrompt.transform.SetAsLastSibling();
        }

        private void CreateSupplyCollectedAnimation(Canvas canvas)
        {
            _supplyCollectedAnimation = CreateUiObject("AdventureSupplyCollected", canvas.transform, typeof(Image), typeof(CanvasGroup));
            var rect = _supplyCollectedAnimation.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.32f, 0.70f);
            rect.anchorMax = new Vector2(0.68f, 0.82f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _supplyCollectedBasePosition = rect.anchoredPosition;
            _supplyCollectedAnimation.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.09f, 0.9f);
            _supplyCollectedGroup = _supplyCollectedAnimation.GetComponent<CanvasGroup>();
            _supplyCollectedGroup.interactable = false;
            _supplyCollectedGroup.blocksRaycasts = false;

            _supplyCollectedText = CreateText("Text", rect, "物资采集完成", 30);
            _supplyCollectedText.color = new Color(0.55f, 0.95f, 1f, 1f);
            _supplyCollectedText.fontStyle = FontStyle.Bold;
            _supplyCollectedText.rectTransform.anchorMin = Vector2.zero;
            _supplyCollectedText.rectTransform.anchorMax = Vector2.one;
            _supplyCollectedText.rectTransform.offsetMin = new Vector2(18f, 8f);
            _supplyCollectedText.rectTransform.offsetMax = new Vector2(-18f, -8f);
            _supplyCollectedAnimation.SetActive(false);
        }

        private void ShowSupplyCollectedAnimation(string message)
        {
            if (_supplyCollectedAnimation == null) return;
            _supplyCollectedAnimationTime = 0f;
            if (_supplyCollectedText != null)
                _supplyCollectedText.text = string.IsNullOrEmpty(message) ? "物资采集完成" : message;
            _supplyCollectedAnimation.SetActive(true);
            _supplyCollectedGroup.alpha = 0f;
            var rect = _supplyCollectedAnimation.GetComponent<RectTransform>();
            rect.localScale = Vector3.one * 0.76f;
            rect.anchoredPosition = _supplyCollectedBasePosition;
            rect.SetAsLastSibling();
        }

        private void TickSupplyCollectedAnimation()
        {
            if (_supplyCollectedAnimation == null || !_supplyCollectedAnimation.activeSelf) return;

            _supplyCollectedAnimationTime += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_supplyCollectedAnimationTime / SupplyCollectedAnimationDuration);
            var fadeIn = Mathf.Clamp01(t / 0.16f);
            var fadeOut = Mathf.Clamp01((1f - t) / 0.30f);
            _supplyCollectedGroup.alpha = Mathf.Min(fadeIn, fadeOut);

            var rect = _supplyCollectedAnimation.GetComponent<RectTransform>();
            var pop = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 3.2f));
            rect.localScale = Vector3.one * Mathf.Lerp(0.76f, 1.03f, pop);
            rect.anchoredPosition = _supplyCollectedBasePosition + Vector2.up * Mathf.Lerp(0f, 44f, t);
            if (t >= 1f)
                _supplyCollectedAnimation.SetActive(false);
        }

        private void RefreshHud(bool force)
        {
            if (_hudText == null) return;
            var highest = _run.OwnedShips.Count == 0 ? -1 : _run.OwnedShips.Max(AdventureRun.GetAdventureRank);
            var tempParts = _run.Components.Sum(item => _run.GetQuantity(item));
            var search = _searching != null
                ? "\n正在搜索物资：" + Mathf.Clamp(Mathf.RoundToInt(_searching.Progress / SupplySearchTime * 100f), 0, 100) + "%"
                : string.Empty;
            var boss = _run.BossStage ? "\n最终战：对应阵营旗舰已进入战场" : string.Empty;
            _hudText.text =
                "冒险模式  |  第 " + _waveNumber + " 波  Lv." + GetWaveEnemyLevel(_waveNumber) + "  |  " + GetTierName(highest) +
                "\n舰船 " + _run.OwnedShips.Count + "  临时零件 " + tempParts +
                "  击杀 " + _run.Kills + "  物资 " + _run.SuppliesCollected +
                "\n靠近蓝色物资箱并停留以搜索；暂停菜单可随时更换舰船。" + search + boss;
        }

        private static string GetTierName(int rank)
        {
            return rank switch
            {
                0 => "护卫舰",
                1 => "驱逐舰",
                2 => "巡洋舰",
                3 => "战列舰",
                4 => "旗舰",
                _ => "无舰船",
            };
        }

        private GameObject CreateUiObject(string name, Transform parent, params Type[] components)
        {
            var types = new List<Type> { typeof(RectTransform), typeof(CanvasRenderer) };
            foreach (var component in components)
                if (component != null && !types.Contains(component)) types.Add(component);
            var obj = new GameObject(name, types.ToArray());
            obj.layer = parent.gameObject.layer;
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private Text CreateText(string name, Transform parent, string value, int size)
        {
            var obj = CreateUiObject(name, parent, typeof(Text));
            var text = obj.GetComponent<Text>();
            if (_uiFont == null)
            {
                var canvas = _radarPanel.GetComponentInParent<Canvas>();
                _uiFont = canvas != null
                    ? canvas.GetComponentsInChildren<Text>(true).FirstOrDefault(item => item != null && item.font != null)?.font
                    : null;
            }
            text.font = _uiFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = ThreeBodyUiPalette.AccentSoft;
            return text;
        }

        private sealed class SupplyNode
        {
            public SupplyNode(SpaceObject unit) { Unit = unit; }
            public SpaceObject Unit { get; }
            public float Progress { get; set; }
        }

        private readonly AdventureRun _run;
        private readonly AdventureCombatModel _model;
        private readonly IDatabase _database;
        private readonly CombatManager _combatManager;
        private readonly IScene _scene;
        private readonly SpaceObjectFactory _spaceObjectFactory;
        private readonly RadarPanel _radarPanel;
        private readonly IMessenger _messenger;
        private readonly IApplication _application;
        private readonly ILocalization _localization;
        private readonly GuiHelper _guiHelper;
        private readonly ItemTypeFactory _itemTypeFactory;
        private readonly System.Random _random;
        private readonly List<SupplyNode> _supplies = new();
        private readonly HashSet<IShipInfo> _waveEnemies = new();
        private readonly List<ComponentInfo> _waveComponents = new();

        private IShipInfo _bossInfo;
        private SupplyNode _searching;
        private GameObject _hud;
        private Text _hudText;
        private Font _uiFont;
        private GameObject _upgradeOverlay;
        private GameObject _supplySearchPrompt;
        private CanvasGroup _supplySearchGroup;
        private Text _supplySearchText;
        private GameObject _supplyCollectedAnimation;
        private CanvasGroup _supplyCollectedGroup;
        private Text _supplyCollectedText;
        private Vector2 _supplyCollectedBasePosition;
        private long _waveCredits;
        private int _waveNumber;
        private float _supplyTimer;
        private float _hudTimer;
        private float _interWaveTimer;
        private float _supplyCollectedAnimationTime;
        private bool _upgradePaused;
        private bool _waveLootPaused;
        private bool _finalLootPaused;
        private bool _betweenWaves;
        private bool _waveCompletionPending;
        private bool _startBossAfterWaveRewards;
        private bool _bossStageInitialized;
        private bool _transitioningBoss;
        private bool _ending;

        private const float InterWaveDelay = 3f;
        private const float SupplySpawnInterval = 17f;
        private const float SupplySearchTime = 1.5f;
        private const float SupplyCollectedAnimationDuration = 2.2f;
        private const float UpgradeDropChance = 0.06f;
        private const int MaxSupplies = 4;
    }
}
