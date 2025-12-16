using System;
using System.Collections.Generic;
using System.Reflection;
using BubbetsItems.Helpers;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Items;
using UnityEngine;

namespace BubbetsItems.Items.VoidLunar
{
    public class Decent : ItemBase
    {
        protected override void MakeTokens()
        {
            base.MakeTokens();
            var name = GetType().Name.ToUpper();
            SimpleDescriptionToken = name + "_DESC_SIMPLE";
            AddToken(name + "_NAME", "Deep Descent");
            var convert = "Converts all Gestures of the Drowned".Style(StyleEnum.VoidLunar) + ".";
            AddToken(name + "_CONVERT", convert);
            AddToken(name + "_DESC",
                "Equipment effects " + "trigger an additional {0} times".Style(StyleEnum.Utility) + " per use. " +
                "Increases equipment cooldown by {1:0%}. ".Style(StyleEnum.Health));
            AddToken(name + "_DESC_SIMPLE",
                "Equipment effects will " + "trigger an additional 1 ".Style(StyleEnum.Utility) +
                "(+1 per stack)".Style(StyleEnum.Stack) + " times on use.".Style(StyleEnum.Utility) +
                " Increases Equipment cooldown by 50%".Style(StyleEnum.Health) +
                " (+15% per stack)".Style(StyleEnum.Stack) + ". ");
            AddToken(name + "_PICKUP",
                "Equipments " + "trigger more,".Style(StyleEnum.Utility) +
                " increases equipment cooldown. ".Style(StyleEnum.Health) + convert);
            AddToken(name + "_LORE", "");
        }

        protected override void MakeConfigs()
        {
            base.MakeConfigs();
            AddScalingFunction("[a]", "Equipment Activation Amount");
            AddScalingFunction("0.35 + 0.15 * [a]", "Equipment Cooldown");
            AddScalingFunction("[a]", "Executive Card Shop Duplication Amount");
            AddScalingFunction("[a]", "Executive Card Drone Shop Duplication Amount");
        }

        protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
        {
            base.FillVoidConversions(pairs);
            AddVoidPairing(nameof(RoR2Content.Items.AutoCastEquipment));
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Inventory), nameof(Inventory.CalculateEquipmentCooldownScale))]
        public static void ReduceCooldown(Inventory __instance, ref float __result)
        {
            if (!TryGetInstance<Decent>(out var inst)) return;
            var amount = __instance.GetItemCount(inst.ItemDef);
            if (amount <= 0) return;
            __result *= 1f + inst.ScalingInfos[1].ScalingFunction(amount);
        }

        [HarmonyILManipulator, HarmonyPatch(typeof(MultiShopCardUtils), nameof(MultiShopCardUtils.OnPurchase))]
        public static void MultiShopPatch(ILContext il)
        {
            var c = new ILCursor(il);
            var shopIndex = 0;
            if (c.TryGotoNext(x =>
                    x.MatchCallOrCallvirt<MultiShopController>(
                        nameof(MultiShopController.SetCloseOnTerminalPurchase))) &&
                c.TryGotoPrev(x => x.MatchLdloc(out shopIndex)))
            {
                c.Emit(OpCodes.Ldloc, shopIndex);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Action<ShopTerminalBehavior, CostTypeDef.PayCostContext>>(DupeShopPurchase);
            }
            else
            {
                BubbetsItemsPlugin.Log.LogError($"Failed to patch MultiShopCardUtils.OnPurchase");
            }
        }

        private static void DupeShopPurchase(ShopTerminalBehavior shop, CostTypeDef.PayCostContext context)
        {
            if (!context.activatorInventory) return;
            if (!TryGetInstance<Decent>(out var inst)) return;
            var amount = context.activatorInventory.GetItemCount(inst.ItemDef);
            if (amount <= 0) return;
            shop.debt += 1 + Mathf.RoundToInt(inst.ScalingInfos[2].ScalingFunction(amount));
        }

        public static bool suppressDroneDuping = false;
        [HarmonyPrefix,
         HarmonyPatch(typeof(DroneVendorTerminalBehavior), nameof(DroneVendorTerminalBehavior.DispatchDrone),
             typeof(Interactor))]
        public static void DupeDronePurchase(DroneVendorTerminalBehavior __instance, Interactor interactor)
        {
            if (suppressDroneDuping) return;
            if (!TryGetInstance<Decent>(out var inst)) return;
            if (!interactor) return;
            var body = interactor.GetComponent<CharacterBody>();
            if (!body) return;
            var inv = body.inventory;
            if (!inv) return;
            if (!inv.HasEquipment(DLC1Content.Equipment.MultiShopCard.equipmentIndex)) return;
            var amount = inv.GetItemCount(inst.ItemDef);
            if (amount <= 0) return;
            suppressDroneDuping = true;
            for (var i = 0; i < Mathf.FloorToInt(inst.ScalingInfos[3].ScalingFunction(amount)); i++)
            {
                __instance.DispatchDrone(interactor);
            }
            suppressDroneDuping = false;
        }

        protected override void MakeBehaviours()
        {
            base.MakeBehaviours();
            EquipmentSlot.onServerEquipmentActivated += SlotActivated;
        }

        protected override void DestroyBehaviours()
        {
            base.DestroyBehaviours();
            EquipmentSlot.onServerEquipmentActivated -= SlotActivated;
        }

        private void SlotActivated(EquipmentSlot slot, EquipmentIndex index)
        {
            var inv = slot.inventory;
            if (!inv) return;
            var amount = inv.GetItemCount(ItemDef);
            if (amount <= 0) return;
            var def = EquipmentCatalog.GetEquipmentDef(index);
            if (!def) return;
            for (var i = 0; i < Mathf.FloorToInt(ScalingInfos[0].ScalingFunction(amount)); i++)
            {
                slot.PerformEquipmentAction(def);
            }
        }

        //[HarmonyILManipulator, HarmonyPatch(typeof(EquipmentSlot), nameof(EquipmentSlot.ExecuteIfReady))]
        public static void DoDouble(ILContext il)
        {
            var c = new ILCursor(il);
            c.GotoNext(x => x.MatchCallOrCallvirt<EquipmentSlot>(nameof(EquipmentSlot.Execute)));
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<EquipmentSlot>>(DuplicateExecute);
        }

        public static void DuplicateExecute(EquipmentSlot slot)
        {
            var inv = slot.inventory;
            if (!inv) return;
            if (!TryGetInstance<Decent>(out var inst))
                return;

            var amount = inv.GetItemCount(inst.ItemDef);
            if (amount <= 0) return;
            for (var i = 0; i < Mathf.FloorToInt(inst.ScalingInfos[0].ScalingFunction(amount)); i++)
            {
                slot.Execute();
            }
        }
    }
}