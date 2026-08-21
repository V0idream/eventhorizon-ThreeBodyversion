using System;
using System.Collections.Generic;
using System.Linq;
using Constructor.Ships;
using Economy;
using Economy.ItemType;
using Economy.Products;
using Domain.Quests;
using Services.Gui;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace GameServices.Gui
{
    public class GuiHelper
    {
        [Inject] private readonly IGuiManager _guiManager;
        [Inject] private readonly ShowMessageSignal.Trigger _showMessageTrigger;

#if !EDITOR_MODE
        [Inject] private readonly ItemTypeFactory _itemFactory;
        [Inject] private readonly IQuestManager _questManager;
#endif

        public void ShowItemInfoWindow(IProduct item)
        {
            _guiManager.OpenWindow(global::Gui.Notifications.WindowNames.ItemInfoWindow, new WindowArgs(item));
        }

#if !EDITOR_MODE
        public void ShowItemInfoWindow(IShip ship)
        {
            var item = CommonProduct.Create(_itemFactory.CreateMarketShipItem(ship));
            _guiManager.OpenWindow(global::Gui.Notifications.WindowNames.ItemInfoWindow, new WindowArgs(item));
        }
#else
        public void ShowItemInfoWindow(IShip ship)
        {
            throw new NotImplementedException();
        }
#endif

        public void ShowLootWindow(IEnumerable<IProduct> items, System.Action onClosed = null, bool requireContinue = false)
        {
            var id = global::Gui.Notifications.WindowNames.LootWindow;
            _guiManager.OpenWindow(id, new WindowArgs(items.Cast<object>().ToArray()), _ =>
            {
                if (requireContinue)
                    RestoreLootWindowDefaults();
                onClosed?.Invoke();
            });

            if (requireContinue)
                ConfigureLootWindowContinueButton();
        }

        private void ConfigureLootWindowContinueButton()
        {
            var window = _guiManager.FindWindow(global::Gui.Notifications.WindowNames.LootWindow);
            var component = window as Component;
            if (component == null) return;

            var root = component.gameObject;
            var timer = root.GetComponent<global::Gui.Utils.WindowCloseTimer>();
            if (timer != null)
                timer.enabled = false;

            var parent = root.transform.parent;
            if (parent == null) return;
            var existing = parent.Find("AdventureContinueButton");
            GameObject buttonObject;
            if (existing != null)
            {
                buttonObject = existing.gameObject;
            }
            else
            {
                buttonObject = new GameObject("AdventureContinueButton",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
                buttonObject.layer = root.layer;
                buttonObject.transform.SetParent(parent, false);

                var layout = buttonObject.GetComponent<LayoutElement>();
                layout.minHeight = 54f;
                layout.preferredHeight = 54f;
                layout.flexibleHeight = 0f;
                buttonObject.GetComponent<Image>().color = new Color(0.10f, 0.36f, 0.56f, 0.94f);

                var buttonRect = buttonObject.GetComponent<RectTransform>();
                var rootRect = root.GetComponent<RectTransform>();
                buttonRect.anchorMin = rootRect != null ? rootRect.anchorMin : new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = rootRect != null ? rootRect.anchorMax : new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.sizeDelta = new Vector2(200f, 54f);
                var rootPosition = rootRect != null ? rootRect.anchoredPosition : Vector2.zero;
                var halfHeight = rootRect != null ? Mathf.Max(88f, rootRect.rect.height * 0.5f) : 88f;
                buttonRect.anchoredPosition = rootPosition + Vector2.down * (halfHeight + 38f);

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.layer = root.layer;
                labelObject.transform.SetParent(buttonObject.transform, false);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 4f);
                labelRect.offsetMax = new Vector2(-8f, -4f);

                var canvas = root.GetComponentInParent<Canvas>();
                var font = canvas != null
                    ? canvas.GetComponentsInChildren<Text>(true).FirstOrDefault(item => item != null && item.font != null)?.font
                    : null;
                var label = labelObject.GetComponent<Text>();
                label.font = font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.text = "继续";
                label.fontSize = 26;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 14;
                label.resizeTextMaxSize = 26;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.raycastTarget = false;
            }

            var button = buttonObject.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(window.Close);
            buttonObject.SetActive(true);
            buttonObject.transform.SetAsLastSibling();
            window.Enabled = true;
        }

        private void RestoreLootWindowDefaults()
        {
            var window = _guiManager.FindWindow(global::Gui.Notifications.WindowNames.LootWindow);
            var component = window as Component;
            if (component == null) return;

            var timer = component.GetComponent<global::Gui.Utils.WindowCloseTimer>();
            if (timer != null)
            {
                timer.enabled = true;
                timer.ResetTimer();
            }

            var button = component.transform.parent?.Find("AdventureContinueButton");
            if (button != null)
                button.gameObject.SetActive(false);
        }

        public void ShowConfirmation(string text, System.Action action)
        {
            _guiManager.OpenWindow(global::Gui.Common.WindowNames.ConfirmationDialog, new WindowArgs(text), result =>
            {
                if (result == WindowExitCode.Ok)
                    action.Invoke();
            });
        }

        public void ShowConfirmation(string text, Price price, System.Action action)
        {
            _guiManager.OpenWindow(global::Gui.Common.WindowNames.BuyConfirmationDialog, new WindowArgs(text, price), result =>
            {
                if (result == WindowExitCode.Ok)
                    action.Invoke();
            });
        }

        public void ShowMessageBox(string text)
        {
            _guiManager.OpenWindow(global::Gui.Common.WindowNames.MessageBoxWindow, new WindowArgs(text));
        }

        public void ShowMessage(string message)
        {
            _showMessageTrigger.Fire(message);
        }
    }
}
