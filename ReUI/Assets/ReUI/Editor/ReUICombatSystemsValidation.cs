using System;
using System.IO;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Model;
using UnityEditor;
using UnityEngine;
using DatabaseComponent = GameDatabase.DataModel.Component;

namespace ReUI.Editor
{
    internal static class ReUICombatSystemsValidation
    {
        [MenuItem("Tools/ReUI/Validate Beta8.29 Combat Systems")]
        public static void Validate()
        {
            var database = new GameDatabase.Database();
            database.LoadDefault();

            var field = database.GetComponent(new ItemId<DatabaseComponent>(986));
            if (field == null || field == DatabaseComponent.DefaultValue || field.Device == null ||
                field.Device.Id.Value != 917 || field.Faction == null || field.Faction.Id.Value != 23 ||
                field.Layout.Size != 3)
                throw new InvalidOperationException("The stasis field component definition is invalid.");

            var stats = field.Device.Stats;
            if (Mathf.Abs(stats.EnergyConsumption - 650f) > 0.001f ||
                Mathf.Abs(stats.Size - 4f) > 0.001f)
                throw new InvalidOperationException("The stasis field energy drain or radius is invalid.");

            var technology = database.GetTechnology(new ItemId<Technology>(462));
            if (technology is not Technology_Component componentTechnology ||
                componentTechnology.Component == null || componentTechnology.Component.Id.Value != 986 ||
                componentTechnology.Faction == null || componentTechnology.Faction.Id.Value != 23)
                throw new InvalidOperationException("The stasis field technology is invalid.");

            ValidateSprite("Sprites/Components/shield_stasis.png", 128, 128);
            ValidateSprite("Resources/Textures/GUI/Controls/controls_shield_stasis.png", 256, 256);

            ValidateSource(
                "Modules/BattleSimulator/Scripts/Combat/Factory/ShipFactory.cs",
                "CreateIndependentPointDefensePlatform",
                "weapon.Weapon.Id.Value == 137",
                "weapon.Weapon.Id.Value == 138",
                "weapon.Weapon.Id.Value == 156",
                "weapon.Weapon.Id.Value == 158");
            ValidateSource(
                "Modules/BattleSimulator/Scripts/Combat/Unit/Bullet/Action/SpawnBulletsAction.cs",
                "IUnitTargetingPlatform",
                "_parent.GuidanceTarget");
            ValidateSource(
                "Modules/BattleSimulator/Scripts/Combat/Component/Controller/HomingController.cs",
                "_scene.LockedTarget",
                "_preferredTarget",
                "UpdateGuidanceTarget");
            ValidateSource(
                "Modules/BattleSimulator/Scripts/Combat/Unit/Ship/Effects/EdgeControlEffects.cs",
                "ApplyInitialSlowdown",
                "VelocityMultiplier = 0.2f",
                "ClampToModifiedLimits");
            ValidateSource(
                "Modules/BattleSimulator/Scripts/Combat/Unit/Auxiliary/EnergyShield.cs",
                "EnergyShieldInteractionMode.Stasis",
                "ProjectileStasisStatus.Apply",
                "IgnoresNonDroneShipCollisions");
            ValidateSource(
                "Modules/BattleSimulator/Scripts/Combat/Component/Collider/CommonCollider.cs",
                "ShouldIgnoreElectronicShieldShipCollision");

            Debug.Log(
                "[Beta8.29 Combat Validation] independentPointDefense=true, " +
                "stasisShipSlowdown=true, splitWarheadTargetInheritance=true, " +
                "stasisField=true, electronicShieldShipCollision=false, artwork=true");
        }

        private static void ValidateSource(string relativePath, params string[] tokens)
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, relativePath));
            foreach (var token in tokens)
                if (!source.Contains(token))
                    throw new InvalidOperationException(
                        $"{relativePath} is missing required implementation token: {token}");
        }

        private static void ValidateSprite(string relativePath, int width, int height)
        {
            var path = Path.Combine(Application.dataPath, relativePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)) ||
                    texture.width != width || texture.height != height)
                    throw new InvalidOperationException($"Invalid stasis-field artwork: {relativePath}");

                if (texture.GetPixel(0, 0).a > 0.05f)
                    throw new InvalidOperationException(
                        $"Stasis-field artwork does not have a transparent background: {relativePath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
