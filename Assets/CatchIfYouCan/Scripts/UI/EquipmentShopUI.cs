using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Save;
using UnityEngine;
using UnityEngine.UI;

namespace CatchIfYouCan.UI
{
    public class EquipmentShopUI : MonoBehaviour
    {
        [SerializeField] private List<EquipmentDefinition> catalog = new List<EquipmentDefinition>();
        [SerializeField] private Button[] categoryButtons;
        [SerializeField] private Transform itemListParent;
        [SerializeField] private Component detailText;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button backButton;

        private EquipmentCategory _category = EquipmentCategory.Detection;
        private EquipmentDefinition _selected;
        private readonly List<Button> _itemButtons = new List<Button>();

        public Button BackButton => backButton;

        public void BindRuntime(
            Button[] categoryButtons,
            Transform itemListParent,
            Component detailText,
            Button buyButton,
            Button backButton)
        {
            this.categoryButtons = categoryButtons;
            this.itemListParent = itemListParent;
            this.detailText = detailText;
            this.buyButton = buyButton;
            this.backButton = backButton;
            WireButtons();
            LoadCatalog();
            RefreshCategory();
        }

        private void OnEnable()
        {
            LoadCatalog();
            RefreshCategory();
            if (UIManager.Instance != null)
                UIManager.Instance.Show(UIScreen.EquipmentShop, false);
        }

        private void Start()
        {
            WireButtons();
            LoadCatalog();
        }

        private void WireButtons()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(() =>
                {
                    if (UIManager.Instance != null)
                        UIManager.Instance.Show(UIScreen.MainMenu);
                });
            }

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(TryPurchase);
            }

            if (categoryButtons == null) return;
            var categories = new[]
            {
                EquipmentCategory.Detection,
                EquipmentCategory.Visual,
                EquipmentCategory.Audio,
                EquipmentCategory.Protection,
                EquipmentCategory.Utility
            };

            for (int i = 0; i < categoryButtons.Length && i < categories.Length; i++)
            {
                if (categoryButtons[i] == null) continue;
                var cat = categories[i];
                categoryButtons[i].onClick.RemoveAllListeners();
                categoryButtons[i].onClick.AddListener(() =>
                {
                    _category = cat;
                    RefreshCategory();
                });
            }
        }

        private void LoadCatalog()
        {
            if (catalog != null && catalog.Count > 0) return;
            catalog = new List<EquipmentDefinition>();
            var loaded = Resources.LoadAll<EquipmentDefinition>("Equipment");
            if (loaded != null && loaded.Length > 0)
            {
                catalog.AddRange(loaded);
                return;
            }

            catalog.AddRange(EquipmentDefinitionFactory.CreateAllDefaultDefinitions());
        }

        private void RefreshCategory()
        {
            ClearItemButtons();
            _selected = null;

            foreach (var def in catalog)
            {
                if (def == null || def.Category != _category) continue;
                var captured = def;
                var btn = RuntimeUIFactory.CreateButton(itemListParent, def.DisplayName,
                    () => SelectItem(captured), false, 44);
                _itemButtons.Add(btn);
            }

            if (buyButton != null)
                buyButton.interactable = false;
            UITheme.SetText(detailText, "Select equipment to view details.");
        }

        private void SelectItem(EquipmentDefinition def)
        {
            _selected = def;
            if (def == null) return;

            var save = SaveManager.Instance;
            bool owned = save != null && save.Data.IsEquipmentUnlocked(def.Id);
            int tier = save != null ? save.Data.GetEquipmentTier(def.Id) : 0;
            int upgradeCost = Mathf.RoundToInt(def.Price * (tier + 1) * 0.75f);
            int money = save?.Data.Money ?? 0;

            string status = owned ? $"Owned (Tier {Mathf.Max(tier, def.Tier)})" : "Locked";
            string action = owned && tier < 3 ? $"Upgrade Cost: ${upgradeCost:N0}" : $"Buy Cost: ${def.Price:N0}";

            UITheme.SetText(detailText,
                $"{def.DisplayName}\n{def.Description}\n\nCategory: {def.Category}\n{status}\n{action}\nYour Money: ${money:N0}");

            if (buyButton != null)
            {
                bool canAfford = owned ? money >= upgradeCost : money >= def.Price;
                buyButton.interactable = canAfford && (!owned || tier < 3);
                var label = buyButton.GetComponentInChildren<Text>();
                UITheme.SetText(label, owned ? "UPGRADE" : "BUY");
            }
        }

        private void TryPurchase()
        {
            if (_selected == null || SaveManager.Instance == null) return;

            var data = SaveManager.Instance.Data;
            bool owned = data.IsEquipmentUnlocked(_selected.Id);
            int tier = data.GetEquipmentTier(_selected.Id);

            if (!owned)
            {
                if (!SaveManager.Instance.SpendMoney(_selected.Price))
                    return;
                data.UnlockEquipment(_selected.Id);
                data.SetEquipmentTier(_selected.Id, _selected.Tier);
            }
            else if (tier < 3)
            {
                int cost = Mathf.RoundToInt(_selected.Price * (tier + 1) * 0.75f);
                if (!SaveManager.Instance.SpendMoney(cost))
                    return;
                data.SetEquipmentTier(_selected.Id, tier + 1);
            }

            SaveManager.Instance.Save();
            SelectItem(_selected);
        }

        private void ClearItemButtons()
        {
            foreach (var btn in _itemButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            _itemButtons.Clear();
        }
    }
}
