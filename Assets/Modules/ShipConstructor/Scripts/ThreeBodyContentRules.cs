using GameDatabase.DataModel;
using DatabaseComponent = GameDatabase.DataModel.Component;

namespace Constructor
{
    /// <summary>
    /// Content that is intentionally developer/quest-only.  Keeping the
    /// exclusion in one place prevents a special ThreeBody item from leaking
    /// into a random shop or exploration reward through a new code path.
    /// </summary>
    public static class ThreeBodyContentRules
    {
        public static bool IsRestrictedComponent(DatabaseComponent component)
        {
            if (component == null) return false;
            switch (component.Id.Value)
            {
                case 295: // 撕裂星辰 (空幻之梦装备)
                case 296: // 零元素装甲
                case 297: // 量子借贷发生器
                case 298: // 曲率引擎
                case 299: // 电子压缩器
                case 311: // 维度跃升装置
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsRestrictedShip(Ship ship)
        {
            if (ship == null) return false;
            switch (ship.Id.Value)
            {
                case 160:    // 空幻之梦
                case 166:    // 水滴
                case 114514: // 三体模组旗舰
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsRestrictedSatellite(Satellite satellite)
        {
            return satellite != null && satellite.Id.Value == 950; // 实验性装备搭载平台
        }
    }
}
