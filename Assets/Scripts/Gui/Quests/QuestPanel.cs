using System;
using System.Collections.Generic;
using Domain.Quests;
using Services.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace Gui.Quests
{
    public class QuestPanel : MonoBehaviour
    {
        [SerializeField] private Text _name;
        [SerializeField] private Text _description;
        [SerializeField] private Button _focusButton;
        [SerializeField] private Button _cancelButton;

        public IQuest Quest => _quest;

        public void Update()
        {
            if (_quest == null) return;

            _timeFromLastUpdate += Time.deltaTime;
            if (_timeFromLastUpdate < 5) return;
            _timeFromLastUpdate = 0;

            _description.text = _quest.GetRequirementsText(_localization);
        }

        public void Initialize(IQuest quest, ILocalization localization)
        {
            _timeFromLastUpdate = 0;
            _localization = localization;
            _quest = quest;

            _name.text = localization.GetString(quest.Model.Name);
            _description.text = quest.GetRequirementsText(localization);
            _focusButton.gameObject.SetActive(quest.TryGetBeacons(new List<int>()));
            _cancelButton.gameObject.SetActive(quest.Model.QuestType.IsCancellable());
            if (_acceptButton != null)
                _acceptButton.gameObject.SetActive(false);
        }

        public void ConfigureOffer(string label, Action accept)
        {
            EnsureAcceptButton();
            if (_acceptButton == null)
                return;

            _focusButton.gameObject.SetActive(false);
            _cancelButton.gameObject.SetActive(false);
            _acceptLabel.text = label;
            _acceptButton.onClick.RemoveAllListeners();
            _acceptButton.onClick.AddListener(() => accept?.Invoke());
            _acceptButton.gameObject.SetActive(true);
        }

        private void EnsureAcceptButton()
        {
            if (_acceptButton != null || _cancelButton == null)
                return;

            var sourceRect = _cancelButton.GetComponent<RectTransform>();
            var sourceImage = _cancelButton.GetComponent<Image>();
            var sourceText = _cancelButton.GetComponentInChildren<Text>(true);
            var buttonObject = new GameObject("AcceptQuestButton", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.layer = _cancelButton.gameObject.layer;
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(_cancelButton.transform.parent, false);
            if (sourceRect != null)
            {
                rect.anchorMin = sourceRect.anchorMin;
                rect.anchorMax = sourceRect.anchorMax;
                rect.pivot = sourceRect.pivot;
                rect.anchoredPosition = sourceRect.anchoredPosition;
                rect.sizeDelta = sourceRect.sizeDelta;
                rect.offsetMin = sourceRect.offsetMin;
                rect.offsetMax = sourceRect.offsetMax;
            }

            var image = buttonObject.GetComponent<Image>();
            if (sourceImage != null)
            {
                image.sprite = sourceImage.sprite;
                image.type = sourceImage.type;
                image.color = sourceImage.color;
                image.material = sourceImage.material;
            }

            _acceptButton = buttonObject.GetComponent<Button>();
            _acceptButton.targetGraphic = image;
            _acceptButton.transition = _cancelButton.transition;
            _acceptButton.colors = _cancelButton.colors;
            _acceptButton.spriteState = _cancelButton.spriteState;
            _acceptButton.navigation = _cancelButton.navigation;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.layer = buttonObject.layer;
            labelObject.transform.SetParent(buttonObject.transform, false);
            _acceptLabel = labelObject.GetComponent<Text>();
            if (sourceText != null)
            {
                _acceptLabel.font = sourceText.font;
                _acceptLabel.fontSize = sourceText.fontSize;
                _acceptLabel.fontStyle = sourceText.fontStyle;
                _acceptLabel.alignment = sourceText.alignment;
                _acceptLabel.color = sourceText.color;
            }
            else
            {
                _acceptLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _acceptLabel.fontSize = 20;
                _acceptLabel.alignment = TextAnchor.MiddleCenter;
                _acceptLabel.color = Color.white;
            }
            _acceptLabel.resizeTextForBestFit = true;
            _acceptLabel.resizeTextMinSize = 12;
            _acceptLabel.resizeTextMaxSize = Mathf.Max(14, _acceptLabel.fontSize);
            var labelRect = _acceptLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            buttonObject.SetActive(false);
        }

        private float _timeFromLastUpdate;
        private IQuest _quest;
        private ILocalization _localization;
        private Button _acceptButton;
        private Text _acceptLabel;
    }
}
