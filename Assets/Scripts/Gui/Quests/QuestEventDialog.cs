using Domain.Quests;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Services.Gui;
using ViewModel.Quests;
using Zenject;

namespace Gui.Quests
{
    public class QuestEventDialog : MonoBehaviour
    {
        [Inject] private readonly QuestCombatModelFacctory _questCombatModelFacctory;

        [SerializeField] private DescriptionPanel _description;
        [SerializeField] private FleetPanel _fleet;
        [SerializeField] private ItemsPanel _items;
        [SerializeField] private ActionsPanel _actions;

        public void Initialize(WindowArgs args)
        {
            var data = args.Get<IUserInteraction>();
            if (!string.IsNullOrEmpty(data.StoryImageResource))
            {
                _description.InitializeStoryImage(data.StoryImageResource, data.Actions?.FirstOrDefault());
                _actions.gameObject.SetActive(false);
            }
            else
            {
                _description.Initialize(data.Message, data.CharacterName, data.CharacterAvatar);
                _actions.gameObject.SetActive(true);
                _actions.Initialize(data.Actions);
            }
            if (_fleet)
            {
                var hasEnemyPreview = data.EnemyData.EnemyFleet != null &&
                    data.EnemyData.EnemyFleet.Any(item =>
                        item != null && item != GameDatabase.DataModel.ShipBuild.DefaultValue);
                if (hasEnemyPreview)
                    _fleet.Initialize(_questCombatModelFacctory.CreateEnemyFleet(data.EnemyData));
                else
                    _fleet.gameObject.SetActive(false);
            }
            if (_items) _items.Initialize(data.Loot);

            var scrollRect = GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1;
            }
        }
    }
}
