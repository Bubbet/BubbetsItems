﻿using System.Collections.Generic;
using System.Linq;
using BubbetsItems.Helpers;
using HarmonyLib;
using RoR2;
using RoR2.Items;

namespace BubbetsItems.Items.VoidLunar
{
    public class SeepingOcean : ItemBase
    {
        protected override void MakeTokens()
        {
            base.MakeTokens();
            var name = GetType().Name.ToUpper();
            SimpleDescriptionToken = name + "_DESC_SIMPLE";
            AddToken(name + "_NAME", "Seeping Ocean");
            var convert = "Converts all Eulogy Zeros".Style(StyleEnum.Void) + ".";
            AddToken(name + "_CONVERT", convert);
            AddToken(name + "_DESC",
                "Items have a " + "{0:0%} chance".Style(StyleEnum.Utility) + " to become " +
                "Void Lunar".Style(StyleEnum.VoidLunar) + " items instead. ");
            AddToken(name + "_DESC_SIMPLE",
                "Items have a " + "4% ".Style(StyleEnum.Utility) + "(+4% per stack)".Style(StyleEnum.Stack) +
                " chance to become a " + "Void Lunar".Style(StyleEnum.VoidLunar) + " item instead. " +
                "Unaffected by luck".Style(StyleEnum.Utility) + ". ");
            AddToken(name + "_PICKUP",
                "Items and equipment have a " + "small chance".Style(StyleEnum.Utility) + " to transform into a " +
                "Void Lunar ".Style(StyleEnum.VoidLunar) + "item instead. " + convert);
            AddToken(name + "_LORE", "");
        }

        protected override void MakeConfigs()
        {
            base.MakeConfigs();
            AddScalingFunction("[a] * 0.04", "Void Lunar Chance", oldDefault: "[a] * 0.01");
        }

        protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
        {
            base.FillVoidConversions(pairs);
            AddVoidPairing(nameof(DLC1Content.Items.RandomlyLunar));
        }

        public static bool CanReplace(PickupDef def)
        {
            return def.itemIndex != ItemIndex.None &&
                   ItemCatalog.GetItemDef(def.itemIndex).tier != BubbetsItemsPlugin.VoidLunarTier.tier;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(RandomlyLunarUtils), nameof(RandomlyLunarUtils.CheckForLunarReplacement))]
        public static bool CheckForVoidReplacement(PickupIndex pickupIndex, Xoroshiro128Plus rng,
            // ReSharper disable once InconsistentNaming
            ref PickupIndex __result)
        {
            if (!TryGetInstance(out SeepingOcean inst)) return true;
            var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
            if (pickupDef == null || !CanReplace(pickupDef)) return true;
            var itemCountGlobal = Util.GetItemCountGlobal(inst.ItemDef.itemIndex, false, false);
            if (itemCountGlobal <= 0) return true;
            List<PickupIndex>? list = null;
            if (pickupDef.itemIndex != ItemIndex.None)
            {
                list = BubbetsItemsPlugin.VoidLunarItems.ToList();
            }

            if (list is not { Count: > 0 } ||
                !(rng.nextNormalizedFloat < inst.ScalingInfos[0].ScalingFunction(itemCountGlobal))) return true;
            var index = rng.RangeInt(0, list.Count);
            __result = list[index];
            return false;
        }

        [HarmonyPrefix,
         HarmonyPatch(typeof(RandomlyLunarUtils), nameof(RandomlyLunarUtils.CheckForLunarReplacementUniqueArray))]
        public static bool CheckForVoidReplacementUniqueArray(PickupIndex[] pickupIndices, Xoroshiro128Plus rng)
        {
            if (!TryGetInstance<SeepingOcean>(out var inst)) return true;
            var itemCountGlobal = Util.GetItemCountGlobal(inst.ItemDef.itemIndex, false, false);
            if (itemCountGlobal <= 0) return true;
            List<PickupIndex>? list = null;
            var any = false;
            for (var i = 0; i < pickupIndices.Length; i++)
            {
                PickupDef? pickupDef = PickupCatalog.GetPickupDef(pickupIndices[i]);
                if (pickupDef == null || !CanReplace(pickupDef) ||
                    !(rng.nextNormalizedFloat < inst.ScalingInfos[0].ScalingFunction(itemCountGlobal))) continue;
                List<PickupIndex>? list3 = null;
                if (pickupDef.itemIndex != ItemIndex.None)
                {
                    if (list == null)
                    {
                        list = BubbetsItemsPlugin.VoidLunarItems.ToList();
                        Util.ShuffleList<PickupIndex>(list, rng);
                    }

                    list3 = list;
                }

                if (list3 == null || list3.Count <= 0) continue;

                pickupIndices[i] = list3[i % list3.Count];
                any = true;
            }

            return !any;
        }
    }
}