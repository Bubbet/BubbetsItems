﻿using System;
using System.Linq;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using RoR2.UI.LogBook;

namespace BubbetsItems
{
    [HarmonyPatch]
    public class PickupTooltipFormat
    {
        /*
        public static void Init(Harmony harmony)
        {
            if (Chainloader.PluginInfos.ContainsKey("com.xoxfaby.BetterUI"))
            {
                //InitBetterUIPatches(harmony);
            }
        }

        private static void InitBetterUIPatches(Harmony harmony)
        {
            var methodInfo = typeof(AdvancedIcons).GetMethod("EquipmentIcon_Update", BindingFlags.Static);
            Debug.Log(methodInfo);
            var harmonyMethod = new HarmonyMethod()
            {
                declaringType = typeof(AdvancedIcons),
                methodName = "EquipmentIcon_Update"
            };
            var bae = (MethodBase) AccessTools.DeclaredMethod(typeof(AdvancedIcons), "EquipmentIcon_Update");
            harmony.Patch(bae, null, null, null, null, new HarmonyMethod(typeof(PickupTooltipFormat).GetMethod("FixBetterUIsGarbage")));
        }*/

        [HarmonyPostfix, HarmonyPatch(typeof(TooltipProvider), "get_bodyText")]
        // ReSharper disable twice InconsistentNaming
        public static void FixToken(TooltipProvider __instance, ref string __result)
        {
            try
            {
                //if (!string.IsNullOrEmpty(__instance.overrideBodyText)) return true;

                var s = __result;
                var item = ItemBase.Items.FirstOrDefault(x =>
                {
                    if (x?.ItemDef == null)
                    {
                        // This is a really bad way of doing this
                        BubbetsItemsPlugin.Log.LogWarning(
                            $"ItemDef is null for {x} in tooltipProvider, this will throw errors.");
                        return false;
                    }

                    return __instance.bodyToken == x.ItemDef.descriptionToken ||
                           __instance.bodyToken == x.ItemDef.pickupToken ||
                           __instance.titleToken == x.ItemDef.nameToken ||
                           Language.GetString(x.ItemDef.descriptionToken) == s;
                });
                var equipment = EquipmentBase.Equipments.FirstOrDefault(x =>
                {
                    if (x?.EquipmentDef == null)
                    {
                        BubbetsItemsPlugin.Log.LogWarning(
                            $"EquipmentDef is null for {x} in tooltipProvider, this will throw errors.");
                        return false;
                    }

                    return __instance.bodyToken == x.EquipmentDef.descriptionToken ||
                           __instance.bodyToken == x.EquipmentDef.pickupToken ||
                           __instance.titleToken == x.EquipmentDef.nameToken ||
                           Language.GetString(x.EquipmentDef.descriptionToken) == s;
                });
                var titleEquipment = EquipmentBase.Equipments.FirstOrDefault(x =>
                {
                    if (x?.EquipmentDef == null)
                        BubbetsItemsPlugin.Log.LogWarning(
                            $"EquipmentDef is null for {x} in tooltipProvider, this will throw errors.");
                    return __instance.titleToken == x?.EquipmentDef.nameToken;
                });

                var inventoryDisplay = __instance.transform.parent.GetComponent<ItemInventoryDisplay>();

                // ReSharper disable twice Unity.NoNullPropagation
                if (item != null)
                {
                    __result = item.GetFormattedDescription(inventoryDisplay?.inventory);
                    return;
                }

                if (equipment != null)
                {
                    __result = equipment.GetFormattedDescription(inventoryDisplay?.inventory);
                    return;
                }

                if (titleEquipment != null
                   ) // This is only a half measure for betterui if the advanced tooltips for equipment is turned off this will fuck up and i dont care
                {
                    // TODO this also doesnt work very well without betterui, infact it probably throws an exception
                    __result = titleEquipment.GetFormattedDescription(inventoryDisplay?.inventory) +
                               __instance.overrideBodyText.Substring(Language
                                   .GetString(titleEquipment.EquipmentDef.descriptionToken).Length);
                    return;
                }
            }
            catch (Exception e)
            {
                BubbetsItemsPlugin.Log.LogError(e);
            }
        }

        [HarmonyILManipulator, HarmonyPatch(typeof(PageBuilder), nameof(PageBuilder.AddSimplePickup))]
        public static void PagebuilderPatch(ILContext il)
        {
            var c = new ILCursor(il);
            c.GotoNext(MoveType.After,
                x => x.MatchLdfld<ItemDef>("descriptionToken")
            );
            c.Index -= 1;
            c.Emit(OpCodes.Dup);
            c.Index += 2;
            c.EmitDelegate<Func<ItemDef, string, string>>((def, str) =>
            {
                var item = ItemBase.Items.FirstOrDefault(x => x.ItemDef == def);
                return item != null ? item.GetFormattedDescription(null) : str;
            });

            c.GotoNext(MoveType.After,
                x => x.MatchLdfld<EquipmentDef>("descriptionToken")
            );
            c.Index -= 1;
            c.Emit(OpCodes.Dup);
            c.Index += 2;
            c.EmitDelegate<Func<EquipmentDef, string, string>>((def, str) =>
            {
                var equipment = EquipmentBase.Equipments.FirstOrDefault(x => x.EquipmentDef == def);
                return equipment != null ? equipment.GetFormattedDescription() : str;
            });
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GenericNotification), nameof(GenericNotification.SetItem))]
        public static void NotifItemPostfix(GenericNotification __instance, ItemDef itemDef)
        {
            try
            {
                var item = ItemBase.Items.FirstOrDefault(x => x.ItemDef == itemDef);
                if (item != null && item.sharedInfo.DescInPickup.Value)
                    __instance.descriptionText.token = item.GetFormattedDescription(null,
                        forceHideExtended: item.sharedInfo.ForceHideScalingInfoInPickup.Value);
            }
            catch (Exception e)
            {
                BubbetsItemsPlugin.Log.LogError(e);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GenericNotification), nameof(GenericNotification.SetEquipment))]
        public static void NotifEquipmentPostfix(GenericNotification __instance, EquipmentDef equipmentDef)
        {
            try
            {
                var equipment = EquipmentBase.Equipments.FirstOrDefault(x => x.EquipmentDef == equipmentDef);
                if (equipment != null && equipment.sharedInfo.DescInPickup.Value)
                    __instance.descriptionText.token = equipment.GetFormattedDescription();
            }
            catch (Exception e)
            {
                BubbetsItemsPlugin.Log.LogError(e);
            }
        }

        /*
        //[HarmonyILManipulator, HarmonyPatch(typeof(AdvancedIcons), nameof(AdvancedIcons.EquipmentIcon_Update))]
        // Patched in awake because fuck me
        public static void FixBetterUIsGarbage(ILContext il)
        {
            var c = new ILCursor(il);
            c.GotoNext( MoveType.After,
                x => x.MatchLdflda<EquipmentIcon>("currentDisplayData"),
                x => x.MatchLdfld<EquipmentIcon.DisplayData>("equipmentDef"),
                x => x.MatchLdfld<EquipmentDef>("descriptionToken"),
                x => x.MatchCall<Language>("GetString") 
            );
            c.Index-=2;
            c.RemoveRange(2);
            c.EmitDelegate<Func<EquipmentDef, string>>(def =>
            {
                var equipment = EquipmentBase.Equipments.FirstOrDefault(x => x.EquipmentDef == def);
                return equipment != null ? equipment.GetFormattedDescription() : Language.GetString(def.descriptionToken);
            });
        }
        */
    }
}