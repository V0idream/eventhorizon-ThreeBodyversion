using System;
using System.Linq;
using Constructor;
using Constructor.Satellites;
using Constructor.Ships;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Extensions;
using GameDatabase.Model;
using UnityEngine;
using DatabaseComponent = GameDatabase.DataModel.Component;

namespace Game.Exploration
{
    public class EnemyShipBuilder : IEnemyShipBuilder
    {
        public EnemyShipBuilder(ItemId<ShipBuild> id, IDatabase database, int level, int seed,
            bool randomizeColor = false, bool allowSatellites = true, bool restrictRandomEquipment = true)
        {
            _database = database;
            _shipId = id.Value;
            _level = level;
            _seed = seed;
            _allowSatellites = allowSatellites;
            _randomizeColor = randomizeColor;
            _restrictRandomEquipment = restrictRandomEquipment;
        }

        public Combat.Component.Ship.Ship Build(Combat.Factory.ShipFactory shipFactory, Combat.Factory.SpaceObjectFactory objectFactory, Vector2 position, float rotation)
        {
            var model = CreateShip();
            var spec = model.CreateBuilder().Build(_database.ShipSettings);
            var ship = shipFactory.CreateEnemyShip(spec, position, rotation, Maths.Distance.AiLevel(_level));
            ship.Type.FactionId = model.Model.Faction.Id.Value;
            return ship;
        }

        private IShip CreateShip()
        {
            var random = new System.Random(_seed);

            var build = _database.GetShipBuild(new ItemId<ShipBuild>(_shipId));
            var ship = new EnemyShip(build, _database);

            if (_restrictRandomEquipment)
                SanitizeExplorationEquipment(ship, random);

            var shipLevel = _database.GalaxySettings.EnemyLevel(_level);
            shipLevel -= random.Next(shipLevel/3);
            ship.Experience = Maths.Experience.FromLevel(shipLevel);

            var satelliteClass = Maths.Distance.MaxShipClass(_level);
            if (_allowSatellites && ship.Model.ShipType == ShipType.Common && satelliteClass != DifficultyClass.Default)
            {
                var satellites = _database.SatelliteBuildList.LimitClass(satelliteClass).SuitableFor(build.Ship);
                if (_restrictRandomEquipment)
                    satellites = satellites.Where(IsAllowedExplorationSatellite);
                if (satellites.Any())
                {
                    if (random.Next(3) != 0)
                        ship.FirstSatellite = new CommonSatellite(satellites.RandomElement(random));
                    if (random.Next(3) != 0)
                        ship.SecondSatellite = new CommonSatellite(satellites.RandomElement(random));
                }
            }

            if (_randomizeColor)
            {
                ship.ColorScheme.Type = ShipColorScheme.SchemeType.Hsv;
                ship.ColorScheme.Hue = random.NextFloat();
            }

            return ship;
        }

        private void SanitizeExplorationEquipment(EnemyShip ship, System.Random random)
        {
            var componentLevel = Maths.Distance.ComponentLevel(_level);
            var allowed = _database.ComponentList.Available()
                .Where(Constructor.ThreeBodyContentRules.IsAvailableInExplorationRandomEquipment)
                .ToArray();

            for (var index = ship.Components.Count - 1; index >= 0; --index)
            {
                var installed = ship.Components[index];
                if (Constructor.ThreeBodyContentRules.IsAvailableInExplorationRandomEquipment(installed.Info.Data))
                    continue;

                var replacement = FindCompatibleReplacement(installed.Info.Data, componentLevel, allowed, random);
                if (replacement == null)
                {
                    ship.Components.RemoveAt(index);
                    continue;
                }

                ship.Components[index] = new IntegratedComponent(
                    new ComponentInfo(replacement),
                    installed.X,
                    installed.Y,
                    installed.BarrelId,
                    installed.KeyBinding,
                    installed.Behaviour,
                    installed.Locked,
                    installed.Rotation);
            }
        }

        private static DatabaseComponent FindCompatibleReplacement(DatabaseComponent source, int componentLevel,
            DatabaseComponent[] allowed, System.Random random)
        {
            if (source == null || source == DatabaseComponent.DefaultValue)
                return null;

            var candidates = allowed.Where(candidate =>
                    candidate != null &&
                    candidate.Layout.Data == source.Layout.Data &&
                    candidate.DisplayCategory == source.DisplayCategory &&
                    candidate.WeaponSlotType == source.WeaponSlotType &&
                    candidate.Level <= componentLevel)
                .OrderBy(candidate => Math.Abs(candidate.Level - Math.Min(componentLevel, source.Level)))
                .Take(8)
                .ToArray();

            if (candidates.Length == 0)
                return null;

            return candidates[random.Next(candidates.Length)];
        }

        private static bool IsAllowedExplorationSatellite(SatelliteBuild build)
        {
            return build != null && build != SatelliteBuild.DefaultValue &&
                   build.Components.All(item =>
                       Constructor.ThreeBodyContentRules.IsAvailableInExplorationRandomEquipment(item.Component));
        }

        private readonly bool _randomizeColor;
        private readonly int _seed;
        private readonly int _shipId;
        private readonly int _level;
        private readonly bool _allowSatellites;
        private readonly bool _restrictRandomEquipment;
        private readonly IDatabase _database;
    }
}
