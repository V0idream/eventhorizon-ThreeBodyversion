using GameDatabase.Model;
using Domain.Quests;
using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Services.Localization;
using Services.Resources;

namespace Gui.Quests
{
    public class DescriptionPanel : MonoBehaviour
    {
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly IResourceLocator _resourceLocator;

        [SerializeField] private Text _messageText;
        [SerializeField] private Text _characterName;
        [SerializeField] private Text _characterMessageText;
        [SerializeField] private Image _characterAvatar;
        [SerializeField] private Image _unknownAvatar;
        [SerializeField] private GameObject _characterPanel;
        [SerializeField] private GameObject _messagePanel;

        [Inject] private readonly QuestEventSignal.Trigger _questEventTrigger;

        public void Initialize(string text, string characterName, SpriteId avatar)
        {
            HideStoryView();
            if (string.IsNullOrEmpty(text))
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (string.IsNullOrEmpty(characterName) || !_characterPanel)
            {
                if (_characterPanel) _characterPanel.gameObject.SetActive(false);
                if (_messagePanel) _messagePanel.gameObject.SetActive(true);
                _messageText.text = _localization.GetString(text);
            }
            else
            {
                _characterPanel.gameObject.SetActive(true);
                _messagePanel.gameObject.SetActive(false);
                _characterMessageText.text = _localization.GetString(text);
                _characterName.text = _localization.GetString(characterName);

                var sprite = _resourceLocator.GetSprite(avatar);
                _characterAvatar.sprite = sprite;
                _characterAvatar.gameObject.SetActive(sprite);
                _unknownAvatar.gameObject.SetActive(!sprite);
            }
        }

        public void InitializeStoryImage(string imageResource, UserAction action)
        {
            gameObject.SetActive(true);
            if (_characterPanel) _characterPanel.SetActive(false);
            if (_messagePanel) _messagePanel.SetActive(false);

            EnsureStoryView();
            _storyView.SetActive(true);
            _storyImage.sprite = LoadSprite(imageResource);
            _storyImage.preserveAspect = true;
            _storyFrame.sprite = LoadSprite("Textures/BoundaryStudio/prologue_frame");
            _storyFrame.preserveAspect = true;

            _storyButton.onClick.RemoveAllListeners();
            if (action != null)
            {
                _storyButton.interactable = true;
                _storyButton.onClick.AddListener(() => action.Invoke(_questEventTrigger));
            }
            else
            {
                _storyButton.interactable = false;
            }
        }

        private void EnsureStoryView()
        {
            if (_storyView != null) return;

            _storyView = new GameObject("ThreeBodyPrologueStoryPage", typeof(RectTransform));
            _storyView.transform.SetParent(transform, false);
            var viewRect = (RectTransform)_storyView.transform;
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;

            var frameObject = new GameObject("BackgroundFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameObject.transform.SetParent(_storyView.transform, false);
            var frameRect = (RectTransform)frameObject.transform;
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            _storyFrame = frameObject.GetComponent<Image>();
            _storyFrame.color = Color.white;
            _storyFrame.raycastTarget = false;

            var imageObject = new GameObject("StoryImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(_storyView.transform, false);
            var imageRect = (RectTransform)imageObject.transform;
            imageRect.anchorMin = new Vector2(0.08f, 0.17f);
            imageRect.anchorMax = new Vector2(0.92f, 0.83f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            _storyImage = imageObject.GetComponent<Image>();
            _storyImage.color = Color.white;
            _storyImage.raycastTarget = false;

            var buttonObject = new GameObject("StoryImageTapTarget", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(_storyView.transform, false);
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            var buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(1f, 1f, 1f, 0f);
            buttonImage.raycastTarget = true;
            _storyButton = buttonObject.GetComponent<Button>();
            _storyButton.transition = Selectable.Transition.None;
            _storyView.transform.SetAsLastSibling();
        }

        private void HideStoryView()
        {
            if (_storyView != null) _storyView.SetActive(false);
        }

        private static Sprite LoadSprite(string resourcePath)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        private GameObject _storyView;
        private Image _storyFrame;
        private Image _storyImage;
        private Button _storyButton;
    }
}
