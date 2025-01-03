﻿using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BubbetsItems.Helpers;
using HarmonyLib;
using InLobbyConfig.Fields;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;
using UnityEngine;

namespace BubbetsItems.Items
{
    public class RepulsionPlateMk2 : ItemBase
    {
        public static ConfigEntry<bool> ReductionOnTrue = null!;
        private static ScalingInfo _reductionScalingConfig = null!;
        private static ScalingInfo _armorScalingConfig = null!;

        protected override void MakeTokens()
        {
            base.MakeTokens();
            AddToken("REPULSION_ARMOR_MK2_NAME", "Repulsion Armor Plate Mk2");
            //AddToken("REPULSION_ARMOR_MK2_DESC", "Placeholder, swapped out with config value at runtime."); //pickup);

            // this mess #,###;#,###;0 is responsible for throwing away the negative sign when in the tooltip from the scaling function
            AddToken("REPULSION_ARMOR_MK2_DESC_REDUCTION", "Reduce all " + "incoming damage ".Style(StyleEnum.Damage) + "by " + "{0:#,###;#,###;0}".Style(StyleEnum.Damage) + ". Cannot be reduced below " + "1".Style(StyleEnum.Damage) + ". Scales with how much " + "Repulsion Armor Plates ".Style(StyleEnum.Utility) + "you have.");
            AddToken("REPULSION_ARMOR_MK2_DESC_ARMOR", "Increase armor ".Style(StyleEnum.Heal) + "by " + "{0} ".Style(StyleEnum.Heal) + ". Scales with how much " + "Repulsion Armor Plates ".Style(StyleEnum.Utility) + "you have.");
            
            AddToken("REPULSION_ARMOR_MK2_DESC_REDUCTION_SIMPLE", "Reduce all " + "incoming damage ".Style(StyleEnum.Damage) + "by " + "20 ".Style(StyleEnum.Damage) + "(+amount of repulsion plates per stack)".Style(StyleEnum.Stack) + ". Cannot be reduced below " + "1".Style(StyleEnum.Damage) + ". Scales with how much " + "Repulsion Armor Plates ".Style(StyleEnum.Utility) + "you have.");
            AddToken("REPULSION_ARMOR_MK2_DESC_ARMOR_SIMPLE", "Increase armor ".Style(StyleEnum.Heal) + "by " + "20 ".Style(StyleEnum.Heal) + "(+amount of repulsion plates per stack)".Style(StyleEnum.Stack) + ". Scales with how much " + "Repulsion Armor Plates ".Style(StyleEnum.Utility) + "you have.");

            // <style=cIsDamage>incoming damage</style> by <style=cIsDamage>5<style=cStack> (+5 per stack)</style></style>
            AddToken("REPULSION_ARMOR_MK2_PICKUP", "Receive damage reduction from all attacks depending on each " + "Repulsion Plate".Style(StyleEnum.Utility) + ".");
            AddToken("REPULSION_ARMOR_MK2_LORE", @"Order: Experimental Repulsion Armour Augments - Mk. 2
Tracking number: 07 **
Estimated Delivery: 10/23/2058
Shipping Method: Secure, High Priority
Shipping Address: System Police Station 13/ Port of Marv, Ganymede
Shipping Details:

The order contains cutting-edge experimental technology aimed at reducing risk of harm for the users even in the most harsh of conditions. On top of providing protection Mk. 2's smart nano-bot network enhances already existing protection that the user has installed. This kind of equipment might prove highly necessary as crime rates had seen a rise in the Port of Marv area around station 13, higher risk of injury for stationing officers necessitates an increase in measures used to ensure their safety.

The cost of purchase and production associated with Mk2 is considerably higher than that of its prior iterations, however the considerable step-up in efficiency covers for the costs, as drastic as they might be.");
        }
        protected override void MakeConfigs()
        {
            base.MakeConfigs();
            ReductionOnTrue = sharedInfo.ConfigFile!.Bind(ConfigCategoriesEnum.General, "Reduction On True", true,  "Makes the item behave more like mk1 and give a flat reduction in damage taken if set to true.");
            ReductionOnTrue.SettingChanged += (_, _) => UpdateScalingFunction(); 
            
            var name = GetType().Name;;
            AddScalingFunction("[d] - (20 + [p] * (4 + [a]))", name + " Reduction", "[a] = amount, [p] = plate amount, [d] = damage");
            AddScalingFunction("20 + [p] * (4 + [a])", name + " Armor", "[a] = amount, [p] = plate amount");
            _reductionScalingConfig = ScalingInfos[0];
            _armorScalingConfig = ScalingInfos[1];
            
            //_reductionScalingConfig = configFile.Bind(ConfigCategoriesEnum.BalancingFunctions, name + " Reduction", "[d] - (20 + [p] * (4 + [a]))", "Scaling function for item. ;");
            //_armorScalingConfig = configFile.Bind(ConfigCategoriesEnum.BalancingFunctions, name + " Armor", "", "Scaling function for item. ;");
            UpdateScalingFunction();
        }

        public override void MakeRiskOfOptions()
        {
            base.MakeRiskOfOptions();
            ModSettingsManager.AddOption(new CheckBoxOption(ReductionOnTrue));
        }

        private void UpdateScalingFunction()
        {
            ScalingInfos.Clear();
            ScalingInfos.Add(ReductionOnTrue.Value ? _reductionScalingConfig : _armorScalingConfig);
        }
        public override string GetFormattedDescription(Inventory? inventory, string? token = null, bool forceHideExtended = false)
        {
            //ItemDef.descriptionToken = _reductionOnTrue.Value ? "BUB_REPULSION_ARMOR_MK2_DESC_REDUCTION" :  "BUB_REPULSION_ARMOR_MK2_DESC_ARMOR"; Cannot do this, it breaks the token matching from the tooltip patch
            var context = ScalingInfos[0].WorkingContext;
            context.p = inventory?.GetItemCount(RoR2Content.Items.ArmorPlate) ?? 0;
            context.d = 0f;

            var tokenChoice = ReductionOnTrue.Value
                ? "BUB_REPULSION_ARMOR_MK2_DESC_REDUCTION"
                : "BUB_REPULSION_ARMOR_MK2_DESC_ARMOR";
            
            SimpleDescriptionToken = ReductionOnTrue.Value
                ? "REPULSION_ARMOR_MK2_DESC_REDUCTION_SIMPLE"
                : "REPULSION_ARMOR_MK2_DESC_ARMOR_SIMPLE";
            
            return base.GetFormattedDescription(inventory, tokenChoice, forceHideExtended);
        }

        public override void MakeInLobbyConfig(Dictionary<ConfigCategoriesEnum, List<object>> scalingFunctions)
        {
            base.MakeInLobbyConfig(scalingFunctions);
            scalingFunctions[ConfigCategoriesEnum.BalancingFunctions].Add(ConfigFieldUtilities.CreateFromBepInExConfigEntry(ReductionOnTrue));
        }

        /*
        public void UpdateScalingFunction()
        {
            scalingFunction = _reductionOnTrue.Value ? new Expression(_reductionScalingConfig.Value).ToLambda<ExpressionContext, float>() : new Expression(_armorScalingConfig.Value).ToLambda<ExpressionContext, float>();
        }

        public override float GraphScalingFunction(int itemCount)
        {
            return _reductionOnTrue.Value ? -ScalingFunction(itemCount,1) : ScalingFunction(itemCount);
        }*/

        protected override void MakeBehaviours()
        {
            base.MakeBehaviours();
            RecalculateStatsAPI.GetStatCoefficients += RecalcStats;
        }

        protected override void DestroyBehaviours()
        {
            base.DestroyBehaviours();
            RecalculateStatsAPI.GetStatCoefficients -= RecalcStats;
        }

        // ReSharper disable once InconsistentNaming
        public static void RecalcStats(CharacterBody __instance, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (ReductionOnTrue.Value) return;
            if (!TryGetInstance(out RepulsionPlateMk2 repulsionPlateMk2)) return;
            var inv = __instance.inventory;
            if (!inv) return;
            var amount = inv.GetItemCount(repulsionPlateMk2.ItemDef);
            if (amount <= 0) return;
            
            var plateAmount = inv.GetItemCount(RoR2Content.Items.ArmorPlate);
            // 20 + inv.GetItemCount(RoR2Content.Items.ArmorPlate) * (4 + amount);
            var info = repulsionPlateMk2.ScalingInfos[0];
            info.WorkingContext.p = plateAmount; 
            args.armorAdd += info.ScalingFunction(amount);
        }
        
        public static float DoMk2ArmorPlates(float damage, HealthComponent hc)
        {
            if (!ReductionOnTrue.Value) return damage;
            if (hc == null) return damage;
            if (hc.body == null) return damage;
            if (hc.body.inventory == null) return damage;
            if (!TryGetInstance(out RepulsionPlateMk2 repulsionPlateMk2)) return damage;
            var amount = hc.body.inventory.GetItemCount(repulsionPlateMk2.ItemDef);
            if (amount <= 0) return damage;
            var plateAmount = hc.body.inventory.GetItemCount(RoR2Content.Items.ArmorPlate);
            //damage = Mathf.Max(1f, damage - (20 + plateAmount * (4 + amount)));
            var info = repulsionPlateMk2.ScalingInfos[0];
            info.WorkingContext.p = plateAmount;
            info.WorkingContext.d = damage;
            return info.ScalingFunction(amount);
        }

        [HarmonyILManipulator, HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.TakeDamageProcess))]
        public static void TakeDamageHook(ILContext il)
        {
            var c = new ILCursor(il);
            c.GotoNext(
                x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetProperty(nameof(CharacterBody.armor))!.GetGetMethod())
            );
            c.GotoNext(x => x.MatchCallOrCallvirt(typeof(Mathf), nameof(Mathf.Max)));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<float, HealthComponent, float>>(DoMk2ArmorPlates);
        }
    }
}