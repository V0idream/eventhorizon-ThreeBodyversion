using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Domain;
using Constructor.Ships;
using Game.Adventure;
using GameDatabase;
using GameServices.Audio;
using GameServices.Player;
using GameServices.SceneManager;
using Services.Audio;
using Zenject;

namespace GameStateMachine.States
{
    public sealed class AdventureState : BaseState
    {
        [Inject]
        public AdventureState(
            IStateMachine stateMachine,
            GameStateFactory stateFactory,
            AdventureRun run,
            IDatabase database,
            PlayerSkills playerSkills,
            IMusicPlayer musicPlayer,
            DatabaseMusicPlaylist playlist,
            ExitSignal exitSignal,
            AdventureEditShipSignal editShipSignal)
            : base(stateMachine, stateFactory)
        {
            _run = run;
            _database = database;
            _playerSkills = playerSkills;
            _musicPlayer = musicPlayer;
            _playlist = playlist;
            _exitSignal = exitSignal;
            _editShipSignal = editShipSignal;
            _exitSignal.Event += OnExit;
            _editShipSignal.Event += OnEditShip;
        }

        public override StateType Type => StateType.Adventure;
        public override IEnumerable<GameScene> RequiredScenes { get { yield return GameScene.Combat; } }

        public override void InstallBindings(DiContainer container)
        {
            var level = Math.Min(100, 15 + _run.Kills * 2);
            _combatModel = new AdventureCombatModel(_run, _database, _playerSkills, level);
            container.Bind<AdventureCombatModel>().FromInstance(_combatModel);
            container.Bind<ICombatModel>().FromInstance(_combatModel);
        }

        protected override void OnLoad()
        {
            var rules = _database.GalaxySettings.QuickCombatRules ?? _database.CombatSettings.DefaultCombatRules;
            _playlist.SetCustomCombatPlaylist(rules.CustomSoundtrack);
            _musicPlayer.Play(AudioTrackType.Combat);
        }

        private void OnEditShip(IShip ship)
        {
            if (Condition != GameStateCondition.Active || !_run.Active || ship == null || !_run.OwnedShips.Contains(ship))
                return;

            // ShipEditor is a separate scene/state, so leaving CombatScene
            // destroys the runtime ShipInfo objects. Preserve the current hull
            // percentage for every owned hull before the scene is rebuilt; the
            // edited ship must not receive a free repair by opening the editor.
            if (_combatModel != null)
            {
                foreach (var info in _combatModel.Player.Ships)
                    if (info?.ShipData != null && _run.OwnedShips.Contains(info.ShipData))
                        _run.RememberHullCondition(info.ShipData, info.Condition);
            }

            var context = new ShipEditorState.Context
            {
                Ship = ship,
                DatabaseMode = false,
                AdventureMode = true,
                NextState = this,
            };
            LoadState(StateFactory.CreateShipEditorState(context));
        }

        private void OnExit()
        {
            if (Condition != GameStateCondition.Active)
                return;

            _run.End();
            LoadState(StateFactory.CreateMainMenuState());
        }

        private AdventureCombatModel _combatModel;
        private readonly AdventureRun _run;
        private readonly IDatabase _database;
        private readonly PlayerSkills _playerSkills;
        private readonly IMusicPlayer _musicPlayer;
        private readonly DatabaseMusicPlaylist _playlist;
        private readonly ExitSignal _exitSignal;
        private readonly AdventureEditShipSignal _editShipSignal;

        public class Factory : PlaceholderFactory<AdventureState> { }
    }

    public class StartAdventureSignal : CommonComponents.Signals.SmartWeakSignal<StartAdventureSignal, IShip> { }
    public class AdventureEditShipSignal : CommonComponents.Signals.SmartWeakSignal<AdventureEditShipSignal, IShip> { }
}
