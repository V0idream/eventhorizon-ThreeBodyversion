using System.Collections.Generic;
using System.Linq;
using Constructor;
using Domain.Quests;
using Economy.ItemType;
using Economy.Products;
using GameServices.Player;

namespace GameModel
{
    namespace Quests
    {
        public class QuestInventory : IInventory
        {
            public QuestInventory(ILoot items, ProductFactory productFactory, PlayerSkills playerSkills)
            {
                _items = items;
                _productFactory = productFactory;
                _playerSkills = playerSkills;
            }

            public void Refresh() {}

            public IEnumerable<IProduct> Items
            {
                get
                {
                    if (_products == null)
                    {
                        var pricescale = _playerSkills.PriceScale * 2f;
                        //var extraGoods = _playerSkills.HasMasterTrader ? 1 : 0;

                        _products = _items.Items
                            .Where(item => item.Type is not ComponentItem componentItem ||
                                           !ThreeBodyContentRules.IsRestrictedComponent(componentItem.Component.Data))
                            .Select(item => _productFactory.CreateMarketProduct(item.Type, item.Quantity, pricescale))
                            .ToList();
                    }

                    return _products.Where(item => item.Quantity > 0);
                }
            }

            private List<IProduct> _products;
            private readonly ILoot _items;
            private readonly ProductFactory _productFactory;
            private readonly PlayerSkills _playerSkills;
        }
    }
}
