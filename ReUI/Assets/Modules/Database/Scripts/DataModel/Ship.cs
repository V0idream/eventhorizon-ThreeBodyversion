using System;
using GameDatabase.Model;
using GameDatabase.Serializable;
using GameDatabase.Utils;

namespace GameDatabase.DataModel
{
    public partial class Ship
    {
        partial void OnDataDeserialized(ShipSerializable serializable, Database.Loader loader)
        {
            var barrels = serializable.Barrels ?? Array.Empty<BarrelSerializable>();
            if (TryAddSingerWarpMissileSlots(serializable.Id, barrels.Length, out var slotLayout))
            {
                Layout = slotLayout;
                var expandedBarrels = new BarrelSerializable[barrels.Length + 2];
                Array.Copy(barrels, expandedBarrels, barrels.Length);
                expandedBarrels[barrels.Length] = CreateSingerWarpMissileBarrel();
                expandedBarrels[barrels.Length + 1] = CreateSingerWarpMissileBarrel();
                barrels = expandedBarrels;
            }

            if (TryAddEdgeCounterElectronSlot(serializable.Id, out var counterElectronLayout))
                Layout = counterElectronLayout;

            Barrels = new ImmutableCollection<Barrel>(BarrelConverter.Convert(Layout, barrels, serializable.Id));
        }

        private bool TryAddEdgeCounterElectronSlot(int shipId, out Layout layout)
        {
            layout = Layout;
            int x;
            int y;
            switch (shipId)
            {
                case 11022: // Edge World flagship: Terminate Process
                    x = 39;
                    y = 27;
                    break;
                case 11023: // Edge World titan: Highest Privilege
                    x = 46;
                    y = 28;
                    break;
                default:
                    return false;
            }

            var data = Layout.Data.ToCharArray();
            var size = Layout.Size;
            if (!CanConvertToSpecialRect(data, size, x, y, 3, 3))
                return false;

            for (var row = 0; row < 3; ++row)
            for (var column = 0; column < 3; ++column)
                data[(y + row) * size + x + column] = (char)Enums.CellType.Special;

            layout = new Layout(new string(data));
            return true;
        }

        private static bool CanConvertToSpecialRect(char[] data, int size, int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x + width > size || y + height > size)
                return false;

            for (var row = 0; row < height; ++row)
            for (var column = 0; column < width; ++column)
            {
                var cell = data[(y + row) * size + x + column];
                if (cell == (char)Enums.CellType.Empty || cell == (char)Enums.CellType.Weapon ||
                    cell == (char)Enums.CellType.Engine || cell == (char)Enums.CellType.Special)
                    return false;
            }

            return true;
        }

        private bool TryAddSingerWarpMissileSlots(int shipId, int barrelCount, out Layout layout)
        {
            layout = Layout;

            int leftX;
            int rightX;
            int y;
            int expectedBarrels;
            switch (shipId)
            {
                case 11001: // Singer cruiser
                    leftX = 12;
                    rightX = 41;
                    y = 24;
                    expectedBarrels = 3;
                    break;
                case 11002: // Singer battleship
                    leftX = 11;
                    rightX = 56;
                    y = 32;
                    expectedBarrels = 4;
                    break;
                case 11003: // Singer flagship
                    leftX = 16;
                    rightX = 69;
                    y = 38;
                    expectedBarrels = 7;
                    break;
                case 11004: // Singer titan
                    leftX = 20;
                    rightX = 78;
                    y = 45;
                    expectedBarrels = 7;
                    break;
                default:
                    return false;
            }

            // If the source data is later updated with explicit warp slots,
            // do not duplicate them here. The expected count also protects
            // custom/imported variants which happen to reuse a Singer ship id.
            if (barrelCount != expectedBarrels)
                return false;

            var data = Layout.Data.ToCharArray();
            var size = Layout.Size;
            if (!CanAddWeaponRect(data, size, leftX, y, 3, 6) ||
                !CanAddWeaponRect(data, size, rightX, y, 3, 6))
                return false;

            AddWeaponRect(data, size, leftX, y, 3, 6);
            AddWeaponRect(data, size, rightX, y, 3, 6);
            layout = new Layout(new string(data));
            return true;
        }

        private static bool CanAddWeaponRect(char[] data, int size, int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x + width > size || y + height > size)
                return false;

            for (var row = 0; row < height; ++row)
            for (var column = 0; column < width; ++column)
                if (data[(y + row) * size + x + column] != (char)Enums.CellType.Empty)
                    return false;

            return true;
        }

        private static void AddWeaponRect(char[] data, int size, int x, int y, int width, int height)
        {
            for (var row = 0; row < height; ++row)
            for (var column = 0; column < width; ++column)
                data[(y + row) * size + x + column] = (char)Enums.CellType.Weapon;
        }

        private static BarrelSerializable CreateSingerWarpMissileBarrel()
        {
            return new BarrelSerializable
            {
                Position = UnityEngine.Vector2.zero,
                Rotation = 0f,
                AutoAimingArc = 360f,
                WeaponClass = "M",
                Size = 0.12f,
            };
        }
    }
}
