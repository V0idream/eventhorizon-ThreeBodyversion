using System.Collections.Generic;
using System.Linq;
using Economy;
using GameServices.Gui;
using GameServices.Player;
using GameStateMachine.States;
using UnityEngine;
using UnityEngine.UI;
using Services.Audio;
using Services.Localization;
using Services.Messenger;
using Session;
using Zenject;

namespace ViewModel.Skills
{
    public class SkillTree : MonoBehaviour
    {
        [Inject] private readonly ISoundPlayer _soundPlayer;
        [Inject] private readonly PlayerSkills _playerSkills;
        [Inject] private readonly PlayerResources _playerResources;
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly IMessenger _messenger;
        [Inject] private readonly ISessionData _session;
        [Inject] private readonly ExitSignal.Trigger _exitTrigger;
        [Inject] private readonly GuiHelper _guiHelper;

        [SerializeField] private Transform _content;
        [SerializeField] private UiLine _linkPrefab;
        [SerializeField] private SkillTreeNode _root;
        [SerializeField] private ObjectList _nodeList;
        [SerializeField] private ToggleGroup _toggleGroup;
        [SerializeField] private InformationPanel _informationPanel;
        [SerializeField] private Text _pointsLeft;
        [SerializeField] private AudioClip _unlockSound;
        [SerializeField] private ViewModel.Common.PricePanel _resetPricePanel;
        [SerializeField] private Button _resetButton;

        public void ToggleValueChanged(bool enabled)
        {
			var node = CurrentNode;
			if (node == null)
				_informationPanel.Cleanup();
			else
			{
				var id = NodeIds[node];
				_informationPanel.Initialize(node, CanUnlockShortestPath(node), _playerSkills.HasSkill(id));
			}
        }

        public void UnlockButtonClicked()
        {
            var node = CurrentNode;
			if (node == null)
                return;

            var path = FindShortestUnlockPath(node);
            if (path == null || path.Count > _playerSkills.AvailablePoints)
                return;

            var changed = false;
            foreach (var item in path)
            {
                var id = NodeIds[item];
                if (_playerSkills.HasSkill(id))
                    continue;
                if (!_playerSkills.TryAdd(id))
                    return;

                item.State = SkillTreeNode.NodeState.EnabledAndConnected;
                UpdateLinkedNodes(item);
                changed = true;
            }

            if (!changed)
                return;

            _soundPlayer.Play(_unlockSound);
			ToggleValueChanged(true);

            UpdateResetPanel();
        }

        public void ResetSkills()
        {
            if (_playerSkills.PointsSpent == 0)
                return;

            _guiHelper.ShowConfirmation(_localization.GetString("$CommonConfirmation"), ResetSkillsImpl);
        }

        private void ResetSkillsImpl()
        {
            var price = ResetPrice;
            if (!price.TryWithdraw(_playerResources))
                return;

            _playerSkills.Reset();

            _connectedNodes.Clear();
            _toggleGroup.SetAllTogglesOff();

            RebuildTree();
            UpdateResetPanel();
            UpdateAvailablePoints();
        }

        public void Exit()
        {
            _exitTrigger.Fire();
        }

        private SkillTreeNode CurrentNode
        {
            get
            {
                var toggle = _toggleGroup.ActiveToggles().FirstOrDefault();
                return toggle ? toggle.GetComponent<SkillTreeNode>() : null;
            }
        }

        private void Start()
        {
            _messenger.AddListener(EventType.PlayerSkillsChanged, UpdateAvailablePoints);
            _messenger.AddListener(EventType.EscapeKeyPressed, OnCancel);
            UpdateAvailablePoints();
            RebuildTree();
            UpdateResetPanel();
            _informationPanel.Cleanup();
        }

        private void RebuildTree()
        {
            foreach (var item in NodeIds)
                item.Key.State = _playerSkills.HasSkill(item.Value) ? SkillTreeNode.NodeState.Enabled : SkillTreeNode.NodeState.Disabled;
            UpdateLinkedNodes(_root);
        }

        private void UpdateResetPanel()
        {
            var price = ResetPrice;
            var isEnough = price.IsEnough(_playerResources);

            _resetPricePanel.gameObject.SetActive(price.Amount > 0);
            _resetPricePanel.Initialize(null, price, !isEnough);
            _resetButton.interactable = isEnough && _playerSkills.PointsSpent > 0;
        }

        private Price ResetPrice { get { return Economy.Price.Premium(_session.Upgrades.ResetCounter*10); } }

        private void OnCancel()
        {
            if (!this) return;
            Exit();
        }

        private void UpdateAvailablePoints()
        {
            _pointsLeft.text = _localization.GetString("$ResearchPointsAvailable", _playerSkills.AvailablePoints.ToString());
        }

        private void UpdateLinkedNodes(SkillTreeNode node)
        {
			foreach (var item in node.LinkedNodes) 
			{
				if (!_connectedNodes.Add(item))
					continue;

				if (_playerSkills.HasSkill(NodeIds[item])) 
				{
					item.State = SkillTreeNode.NodeState.EnabledAndConnected;
					UpdateLinkedNodes(item);
				}
			}
        }

        private bool CanUnlockShortestPath(SkillTreeNode target)
        {
            if (_playerSkills.HasSkill(NodeIds[target]))
                return false;

            var path = FindShortestUnlockPath(target);
            return path != null && path.Count <= _playerSkills.AvailablePoints;
        }

        private List<SkillTreeNode> FindShortestUnlockPath(SkillTreeNode target)
        {
            var sources = NodeIds.Keys
                .Where(node => node == _root ||
                               (_connectedNodes.Contains(node) && _playerSkills.HasSkill(NodeIds[node])))
                .ToArray();

            var queue = new Queue<SkillTreeNode>();
            var previous = new Dictionary<SkillTreeNode, SkillTreeNode>();
            foreach (var source in sources)
            {
                if (previous.ContainsKey(source))
                    continue;
                previous[source] = null;
                queue.Enqueue(source);
            }

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (node == target)
                    break;

                foreach (var linked in node.LinkedNodes)
                {
                    if (linked == null || !NodeIds.ContainsKey(linked) || previous.ContainsKey(linked))
                        continue;
                    previous[linked] = node;
                    queue.Enqueue(linked);
                }
            }

            if (!previous.ContainsKey(target))
                return null;

            var path = new List<SkillTreeNode>();
            for (var current = target; current != null && !sources.Contains(current); current = previous[current])
                path.Add(current);
            path.Reverse();
            return path.Where(node => !_playerSkills.HasSkill(NodeIds[node])).ToList();
        }

        private Dictionary<SkillTreeNode, int> NodeIds
        {
            get
            {
                if (_nodeIds == null)
                {
                    _nodeIds = new Dictionary<SkillTreeNode, int>();
                    for (var i = 0; i < _nodeList.Children.Length; ++i)
                    {
                        var child = _nodeList.Children[i];
                        var node = child ? child.GetComponent<SkillTreeNode>() : null;
                        if (node != null)
                            _nodeIds.Add(node, i);
                    }
                }

                return _nodeIds;
            }
        }

        private Dictionary<SkillTreeNode, int> _nodeIds;
		private readonly HashSet<SkillTreeNode> _connectedNodes = new HashSet<SkillTreeNode>();
    }
}
