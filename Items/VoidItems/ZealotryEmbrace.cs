using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BubbetsItems.Helpers;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;

namespace BubbetsItems.Items
{
    public class ZealotryEmbrace : ItemBase
    {
        public static ConfigEntry<bool> OnlyMyDots = null!;
        public static ConfigEntry<bool> OnlyOneDot = null!;

        protected override void MakeTokens()
        {
            base.MakeTokens();

            AddToken("ZEALOTRYEMBRACE_NAME", "Zealotry Embrace");
            var convert = "Corrupts all Death Marks".Style(StyleEnum.Void) + ".";
            AddToken("ZEALOTRYEMBRACE_CONVERT", convert);
            AddToken("ZEALOTRYEMBRACE_PICKUP",
                "Deal more damage to enemies with barely any debuffs inflicted. " + convert);
            AddToken("ZEALOTRYEMBRACE_DESC",
                "Deal " + "{0:0%} more damage ".Style(StyleEnum.Damage) + "on enemies with less than " +
                "{1} ".Style(StyleEnum.Damage) + "debuffs. ");
            AddToken("ZEALOTRYEMBRACE_DESC_SIMPLE",
                "Deal " + "25% more damage ".Style(StyleEnum.Damage) + "on enemies with less than " +
                "3 ".Style(StyleEnum.Damage) + "(+1 per stack) ".Style(StyleEnum.Stack) + "debuffs. ");
            SimpleDescriptionToken = "ZEALOTRYEMBRACE_DESC_SIMPLE";
            AddToken("ZEALOTRYEMBRACE_LORE", "");
        }

        protected override void MakeConfigs()
        {
            base.MakeConfigs();
            AddScalingFunction("0.25", "Damage Increase");
            AddScalingFunction("2 + [a]", "Debuff Amount");
            OnlyMyDots = sharedInfo.ConfigFile!.Bind(ConfigCategoriesEnum.General,
                "Zealotry Embrace: Only track my debuffs", true,
                "Should only your dots track to the total");
            OnlyOneDot = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General,
                "Zealotry Embrace: Only one dot stack", false,
                "Should each dot stack count towards the total, else treat all stacks as one buff.");
        }

        public override void MakeRiskOfOptions()
        {
            base.MakeRiskOfOptions();
            ModSettingsManager.AddOption(new CheckBoxOption(OnlyMyDots));
            ModSettingsManager.AddOption(new CheckBoxOption(OnlyOneDot));
        }

        protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
        {
            AddVoidPairing("DeathMark");
        }

        [HarmonyILManipulator, HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.TakeDamageProcess))]
        public static void IlTakeDamage(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.MatchNearbyDamage(out var masterNum, out var num2))
            {
                BubbetsItemsPlugin.Log.LogError("Failed to match nearby damage.");
                return;
            }
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, masterNum);
            c.Emit(OpCodes.Ldarg_1);
            c.Emit(OpCodes.Ldloc, num2);
            c.EmitDelegate<Func<HealthComponent, CharacterMaster, DamageInfo, float, float>>(
                (hc, master, damageInfo, amount) =>
                {
                    if (!TryGetInstance<ZealotryEmbrace>(out var inst)) return amount;
                    var count = master.inventory.GetItemCount(inst.ItemDef);
                    if (count <= 0) return amount;
                    
                    var bo = master.GetBodyObject();
                    var debuffCount = BuffCatalog.debuffBuffIndices.Sum(buffType => hc.body.GetBuffCount(buffType));
                    var dotController = DotController.FindDotController(hc.gameObject);
                    if (dotController)
                        if (OnlyOneDot.Value)
                        {
                            var list = from dotStack in OnlyMyDots.Value
                                    ? dotController.dotStackList.Where(x => x.attackerObject == bo)
                                    : dotController.dotStackList
                                select dotStack.dotIndex;
                            debuffCount += list.Distinct().Count();
                        }
                        else
                        {
                            if (OnlyMyDots.Value)
                                debuffCount +=
                                    dotController.dotStackList.Count(x => x.attackerObject == bo);
                            else
                                debuffCount += dotController.dotStackList.Count;
                        }

                    if (debuffCount < inst.ScalingInfos[1].ScalingFunction(count))
                    {
                        damageInfo.damageColorIndex = DamageColorIndex.Void;
                        amount *= 1f + inst.ScalingInfos[0].ScalingFunction(count);
                    }

                    return amount;
                });
            c.Emit(OpCodes.Stloc, num2);
        }
    }
}