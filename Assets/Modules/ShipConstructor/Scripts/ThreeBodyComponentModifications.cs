using System;
using System.Collections.Generic;
using Constructor.Model;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;
using UnityEngine;

namespace Constructor
{
    // The database's ordinary component mods are intentionally generic and
    // quality-scaled.  The Three Body refit catalogue is different: every
    // option is a fixed, named doctrine.  Keep its ids and eligibility rules
    // in one place so UI, save validation and combat construction agree.
    public static class ThreeBodyComponentModifications
    {
        public const int RapidFire = 101;
        public const int Overclock = 102;
        public const int ArmorBreak = 103;
        public const int SelfSharpening = 104;
        public const int AcidDissolution = 105;
        public const int ArmoredProjectile = 106;
        public const int Guidance = 107;
        public const int Pierce = 108;
        public const int Sweep = 109;
        public const int EngineOverload = 110;
        public const int AdaptiveArmor = 111;
        public const int EnergyLeech = 112;
        public const int EnergyGuard = 113;
        public const int ShieldRecirculation = 114;

        public static bool IsCustom(ComponentMod modification)
        {
            if (modification == null || modification == ComponentMod.Empty)
                return false;

            return IsCustom(modification.Id.Value);
        }

        public static bool IsCustom(int id) => id >= RapidFire && id <= ShieldRecirculation;

        public static bool IsEligible(GameDatabase.DataModel.Component component, ComponentMod modification)
        {
            if (component == null || modification == null || modification == ComponentMod.Empty)
                return modification == null || modification == ComponentMod.Empty;

            return IsEligible(component, modification.Id.Value);
        }

        public static bool IsEligible(GameDatabase.DataModel.Component component, int modificationId)
        {
            if (component == null)
                return false;

            if (!IsCustom(modificationId))
                return false;

            if (IsPointDefense(component))
                return false;

            switch (modificationId)
            {
                case RapidFire:
                case Overclock:
                case ArmorBreak:
                    return IsWeapon(component);
                case SelfSharpening:
                case AcidDissolution:
                    return IsWeapon(component) && IsSlot(component, 'C', 'T');
                case ArmoredProjectile:
                case Guidance:
                    return IsMissileWeapon(component) && IsSlot(component, 'M');
                case Pierce:
                case Sweep:
                    return IsBeamWeapon(component) && IsSlot(component, 'L');
                case EngineOverload:
                    return component.DisplayCategory == ComponentCategory.Engine;
                case AdaptiveArmor:
                case EnergyLeech:
                case EnergyGuard:
                    return component.DisplayCategory == ComponentCategory.Defense && component.Stats.ArmorPoints > 0;
                case ShieldRecirculation:
                    return component.DisplayCategory == ComponentCategory.Defense && component.Stats.ShieldPoints > 0;
                default:
                    return false;
            }
        }

        public static IReadOnlyList<int> GetOptions(GameDatabase.DataModel.Component component)
        {
            var options = new List<int> { 0 };
            foreach (var id in _allOptions)
                if (IsEligible(component, id))
                    options.Add(id);
            return options;
        }

        public static string GetName(int modificationId)
        {
            switch (modificationId)
            {
                case 0: return "无";
                case RapidFire: return "速射";
                case Overclock: return "超频";
                case ArmorBreak: return "破甲";
                case SelfSharpening: return "自锐";
                case AcidDissolution: return "酸溶";
                case ArmoredProjectile: return "装甲";
                case Guidance: return "制导";
                case Pierce: return "贯穿";
                case Sweep: return "横扫";
                case EngineOverload: return "过载";
                case AdaptiveArmor: return "自塑";
                case EnergyLeech: return "吸能";
                case EnergyGuard: return "加护";
                case ShieldRecirculation: return "自充";
                default: return "未知改装";
            }
        }

        public static string GetDescription(int modificationId)
        {
            switch (modificationId)
            {
                case 0: return "不安装改装。";
                case RapidFire: return "装填时间 -20%，伤害 -20%。";
                case Overclock: return "能耗 +30%，伤害 +25%。";
                case ArmorBreak: return "造成生命值伤害后，削弱目标对应伤害抗性 10%，持续 5 秒，最多叠加 3 层。";
                case SelfSharpening: return "C、T 槽限定。若目标对应抗性为 X% 且 X＞30%，本次攻击按 30% 抗性结算，同时伤害降低（X-70）%；X＞100 时按 100 计算。";
                case AcidDissolution: return "C、T 槽限定。伤害转化为腐蚀伤害，伤害 -33%。";
                case ArmoredProjectile: return "M 槽限定。弹体不可摧毁，但射程和飞行速度均 -40%。";
                case Guidance: return "M 槽限定。强制火箭弹获得制导追踪，能耗 +80%。";
                case Pierce: return "L 槽限定。激光可穿过敌人，基础伤害 -20%；每穿过一个目标再降低 20%，最多穿过 3 名敌人。";
                case Sweep: return "L 槽限定。光束宽度 +50%，射程 -20%，伤害 -20%。";
                case EngineOverload: return "引擎限定。速度和转向速度 +50%，能耗 +100%。";
                case AdaptiveArmor: return "装甲限定。生命值每低于满血 10%，每件提供 1% 四类伤害独立减免；同类改装可叠加，减免总上限 90%。";
                case EnergyLeech: return "装甲限定。生命值受损时，按损血比例的一半回复能量；最多 10 件，满层时损失 1% 生命回复 5% 能量。";
                case EnergyGuard: return "装甲限定。每 100 点全舰能量生产速度，每件提供 1% 护甲值；每件最多提供 25%。";
                case ShieldRecirculation: return "盾容装甲限定。生命值受损时，回复损失生命数值五分之一的盾容；最多 5 件，满层时损失 1 点生命回复 1 点盾容。";
                default: return string.Empty;
            }
        }

        public static bool Apply(ComponentMod modification, ref ShipEquipmentStats stats)
        {
            if (!IsCustom(modification))
                return false;

            switch (modification.Id.Value)
            {
                case EngineOverload:
                    stats.EnginePower *= 1.5f;
                    stats.EnginePowerWithoutEnergy *= 1.5f;
                    stats.TurnRate *= 1.5f;
                    stats.TurnRateWithoutEnergy *= 1.5f;
                    stats.EnergyConsumption *= 2f;
                    stats.EngineEnergyConsumption *= 2f;
                    break;
            }

            return true;
        }

        public static bool Apply(ComponentMod modification, ref WeaponStatModifier stats)
        {
            if (!IsCustom(modification))
                return false;

            switch (modification.Id.Value)
            {
                case RapidFire:
                    stats.DamageMultiplier *= 0.8f;
                    stats.FireRateMultiplier *= 1.25f;
                    break;
                case Overclock:
                    stats.DamageMultiplier *= 1.25f;
                    stats.EnergyCostMultiplier *= 1.3f;
                    break;
                case ArmorBreak:
                    stats.ArmorBreaking = true;
                    break;
                case SelfSharpening:
                    stats.SelfSharpening = true;
                    break;
                case AcidDissolution:
                    stats.DamageMultiplier *= 0.67f;
                    stats.ConvertDamageToCorrosive = true;
                    break;
                case ArmoredProjectile:
                    stats.RangeMultiplier *= 0.6f;
                    stats.VelocityMultiplier *= 0.6f;
                    stats.ProjectileIndestructible = true;
                    break;
                case Guidance:
                    stats.EnergyCostMultiplier *= 1.8f;
                    stats.ForceHoming = true;
                    break;
                case Pierce:
                    stats.PiercingBeam = true;
                    break;
                case Sweep:
                    stats.DamageMultiplier *= 0.8f;
                    stats.RangeMultiplier *= 0.8f;
                    stats.SweepingBeam = true;
                    break;
            }

            return true;
        }

        public static bool Apply(ComponentMod modification, ref WeaponStats weapon, ref AmmunitionObsoleteStats ammunition)
        {
            if (!IsCustom(modification))
                return false;

            switch (modification.Id.Value)
            {
                case RapidFire:
                    ammunition.Damage *= 0.8f;
                    weapon.FireRate *= 1.25f;
                    break;
                case Overclock:
                    ammunition.Damage *= 1.25f;
                    ammunition.EnergyCost *= 1.3f;
                    break;
                case ArmoredProjectile:
                    ammunition.Range *= 0.6f;
                    ammunition.Velocity *= 0.6f;
                    break;
                case Guidance:
                    ammunition.EnergyCost *= 1.8f;
                    break;
                case Pierce:
                case Sweep:
                    ammunition.Damage *= 0.8f;
                    if (modification.Id.Value == Sweep)
                        ammunition.Range *= 0.8f;
                    break;
            }

            return true;
        }

        public static bool Apply(ComponentMod modification, ref DeviceStats device) => IsCustom(modification);
        public static bool Apply(ComponentMod modification, ref DroneBayStats droneBay) => IsCustom(modification);

        public static void AddToSummary(ref ThreeBodyModificationSummary summary, ComponentMod modification)
        {
            if (!IsCustom(modification))
                return;

            switch (modification.Id.Value)
            {
                case AdaptiveArmor: ++summary.AdaptiveArmorCount; break;
                case EnergyLeech: ++summary.EnergyLeechCount; break;
                case EnergyGuard: ++summary.EnergyGuardCount; break;
                case ShieldRecirculation: ++summary.ShieldRecirculationCount; break;
            }
        }

        public static void ApplyShipWide(
            ref ShipEquipmentStats stats,
            in ThreeBodyModificationSummary summary,
            float wholeShipEnergyProduction)
        {
            if (summary.EnergyGuardCount <= 0)
                return;

            var perComponentBonus = Mathf.Clamp(
                Mathf.Max(0f, wholeShipEnergyProduction) / 100f * 0.01f,
                0f,
                0.25f);
            stats.ThreeBodyArmorMultiplier += perComponentBonus * summary.EnergyGuardCount;
        }

        private static bool IsWeapon(GameDatabase.DataModel.Component component) => component.Weapon != null && component.Ammunition != null;
        private static bool IsMissileWeapon(GameDatabase.DataModel.Component component) =>
            IsWeapon(component) && component.Ammunition.Body.HitPoints > 0;
        private static bool IsBeamWeapon(GameDatabase.DataModel.Component component) =>
            IsWeapon(component) && component.Ammunition.Controller is BulletController_Beam;
        private static bool IsPointDefense(GameDatabase.DataModel.Component component) =>
            component.Weapon != null && (component.Weapon.Id.Value == 137 || component.Weapon.Id.Value == 138);
        private static bool IsSlot(GameDatabase.DataModel.Component component, params char[] slots)
        {
            foreach (var slot in slots)
                if (component.WeaponSlotType == slot)
                    return true;
            return false;
        }

        private static readonly int[] _allOptions =
        {
            RapidFire, Overclock, ArmorBreak, SelfSharpening, AcidDissolution,
            ArmoredProjectile, Guidance, Pierce, Sweep, EngineOverload,
            AdaptiveArmor, EnergyLeech, EnergyGuard, ShieldRecirculation,
        };
    }

    public struct ThreeBodyModificationSummary
    {
        public int AdaptiveArmorCount;
        public int EnergyLeechCount;
        public int EnergyGuardCount;
        public int ShieldRecirculationCount;
    }

}
