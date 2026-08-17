using System;
using System.IO;
using Constructor;
using Economy.Products;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Model;
using UnityEditor;
using UnityEngine;
using DatabaseComponent = GameDatabase.DataModel.Component;

namespace ReUI.Editor
{
    internal static class ReUIMerchantBalanceValidation
    {
        private static readonly int[] ExplicitlyRestrictedComponents =
        {
            288, 289, 290, 291, 292, 293, 294,
            295, 296, 297, 298, 299,
            311, 936, 937,
            948, 949, 950, 951, 952, 953, 954, 955, 956, 957, 958, 959,
            965, 966, 980, 982, 983, 986,
        };

        [MenuItem("Tools/ReUI/Validate Merchant Balance")]
        public static void Validate()
        {
            var database = new GameDatabase.Database();
            database.LoadDefault();

            foreach (var id in ExplicitlyRestrictedComponents)
            {
                var component = database.GetComponent(new ItemId<DatabaseComponent>(id));
                AssertValid(component, id);
                if (!ThreeBodyContentRules.IsRestrictedComponent(component) ||
                    ThreeBodyContentRules.IsAvailableInRandomMarket(component))
                    throw new InvalidOperationException(
                        $"Restricted component {id} is still available in random markets.");

                var componentInfo = new ComponentInfo(component);
                if (componentInfo.Price.Amount != ThreeBodyContentRules.RestrictedTradePrice ||
                    componentInfo.PremiumPrice.Amount != ThreeBodyContentRules.RestrictedTradePrice)
                    throw new InvalidOperationException(
                        $"Restricted component {id} does not have the maximum trade price.");
            }

            var emptyDream = database.GetShip(new ItemId<Ship>(160));
            if (emptyDream == null || emptyDream == Ship.DefaultValue ||
                !ThreeBodyContentRules.IsRestrictedShip(emptyDream))
                throw new InvalidOperationException("Empty Dream is still available in random ship markets.");

            // These are excluded by their faction's HideFromMerchants flag,
            // proving that faction-agnostic component rolls now respect it.
            foreach (var id in new[] { 960, 975 })
            {
                var component = database.GetComponent(new ItemId<DatabaseComponent>(id));
                AssertValid(component, id);
                if (ThreeBodyContentRules.IsAvailableInRandomMarket(component))
                    throw new InvalidOperationException(
                        $"Hidden-faction component {id} is still available in random markets.");
            }

            // Ordinary faction progression remains eligible; the fix must not
            // empty the caravan pool or disable legitimate low-level goods.
            foreach (var id in new[] { 300, 303, 304 })
            {
                var component = database.GetComponent(new ItemId<DatabaseComponent>(id));
                AssertValid(component, id);
                if (!ThreeBodyContentRules.IsAvailableInRandomMarket(component))
                    throw new InvalidOperationException(
                        $"Ordinary progression component {id} was removed from random markets.");
            }

            for (var seed = 0; seed < 2048; ++seed)
            {
                if (!ComponentInfo.TryCreateRandomComponent(
                        database,
                        1000,
                        null,
                        new System.Random(seed),
                        true,
                        ComponentQuality.P3,
                        out var componentInfo))
                    throw new InvalidOperationException("The random merchant component pool is empty.");

                if (!ThreeBodyContentRules.IsAvailableInRandomMarket(componentInfo.Data))
                    throw new InvalidOperationException(
                        $"Random merchant roll leaked component {componentInfo.Data.Id.Value}.");
            }

            var restrictedPriceProvider = RestrictedTradePriceProvider.Instance;
            if (restrictedPriceProvider.Price.Amount != ThreeBodyContentRules.RestrictedTradePrice ||
                restrictedPriceProvider.TryBuy(1) || restrictedPriceProvider.TrySell(1))
                throw new InvalidOperationException(
                    "The restricted trade provider does not deny both purchase and sale.");

            ValidateTradeSource(
                "Scripts/Legacy/Game/Quests/Inventory/SantaInventory.cs",
                "ThreeBodyContentRules.IsAvailableInRandomMarket");
            ValidateTradeSource(
                "Scripts/Legacy/Game/Quests/Inventory/ArenaInventory.cs",
                "ThreeBodyContentRules.IsAvailableInRandomMarket");
            ValidateTradeSource(
                "Scripts/Domain/Economy/ItemType/XmasBoxItem.cs",
                "ThreeBodyContentRules.IsAvailableInRandomMarket");
            ValidateTradeSource(
                "Scripts/Legacy/Game/Quests/Inventory/QuestInventory.cs",
                "ThreeBodyContentRules.IsRestrictedComponent");
            ValidateTradeSource(
                "Scripts/Legacy/Game/Quests/Inventory/PlayerInventory.cs",
                "ThreeBodyContentRules.IsRestrictedComponent");
            ValidateTradeSource(
                "Scripts/Legacy/Game/Quests/Inventory/CargoHoldInventory.cs",
                "ThreeBodyContentRules.IsRestrictedComponent");

            var productFactorySource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts/Domain/Economy/Products/ProductFactory.cs"));
            if (CountOccurrences(productFactorySource, "RestrictedTradePriceProvider.Instance") < 8)
                throw new InvalidOperationException(
                    "Not every ProductFactory trade path is guarded against restricted components.");

            Debug.Log(
                "[Merchant Balance Validation] emptyDreamBlocked=true, " +
                "strategicComponentsBlocked=true, hiddenFactionsBlocked=true, " +
                "ordinaryProgressionAvailable=true, randomRolls=2048, " +
                "price=2147483647, purchaseDenied=true, saleDenied=true, " +
                "allTradeSourcesFiltered=true");
        }

        private static void ValidateTradeSource(string relativePath, string requiredToken)
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
            if (!source.Contains(requiredToken))
                throw new InvalidOperationException(
                    $"Trade source {relativePath} is missing its restricted-content filter.");
        }

        private static int CountOccurrences(string source, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                ++count;
                index += token.Length;
            }

            return count;
        }

        private static void AssertValid(DatabaseComponent component, int id)
        {
            if (component == null || component == DatabaseComponent.DefaultValue)
                throw new InvalidOperationException($"Component {id} is missing from the database.");
        }
    }
}
