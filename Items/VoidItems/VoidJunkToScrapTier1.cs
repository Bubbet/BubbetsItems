using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BubbetsItems.Helpers;
using HarmonyLib;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;

namespace BubbetsItems.Items
{
	public class VoidJunkToScrapTier1 : ItemBase
	{
		public static ConfigEntry<bool>? CanConsumeLastStack;
		protected override void MakeTokens()
		{
			base.MakeTokens();
			AddToken("VOIDJUNKTOSCRAPTIER1_NAME", "Void Scrap");
			AddToken("VOIDJUNKTOSCRAPTIER1_PICKUP", "Prioritized when used with " + "Common ".Style(StyleEnum.White) + "3D Printers. " + "Corrupts all Broken items".Style(StyleEnum.Void) + ".");
			AddToken("VOIDJUNKTOSCRAPTIER1_DESC", "Does nothing. " + "Prioritized when used with " + "Common ".Style(StyleEnum.White) + "3D Printers. {0}" + "Corrupts all Broken items".Style(StyleEnum.Void) + ".");
			AddToken("VOIDJUNKTOSCRAPTIER1_LORE", "");
		}
		protected override void MakeConfigs()
		{
			base.MakeConfigs();
			CanConsumeLastStack = sharedInfo.ConfigFile!.Bind(ConfigCategoriesEnum.General, "Void Scrap Consume Last Stack", false, "Should the void scrap consume the last stack when being used for scrap.");
		}

		public override void MakeRiskOfOptions()
		{
			base.MakeRiskOfOptions();
			ModSettingsManager.AddOption(new CheckBoxOption(CanConsumeLastStack!));
		}

		public override string GetFormattedDescription(Inventory? inventory, string? token = null, bool forceHideExtended = false)
		{
			return Language.GetStringFormatted(ItemDef.descriptionToken, !CanConsumeLastStack!.Value ? "Cannot consume the last stack. " : "");
		}
		protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
		{
			AddVoidPairing("FragileDamageBonusConsumed HealingPotionConsumed ExtraLifeVoidConsumed ExtraLifeConsumed MysticsItems_LimitedArmorBroken DRUMSTICKCONSUMED ITEM_CARTRIDGECONSUMED ITEM_SINGULARITYCONSUMED ITEM_BROKEN_MESS ConsumedGlassShield_ItemDef TreasureCacheConsumed TreasureCacheVoidConsumed", oldDefault: "FragileDamageBonusConsumed HealingPotionConsumed ExtraLifeVoidConsumed ExtraLifeConsumed");
		}

		[HarmonyPrefix, HarmonyPatch(typeof(CostTypeCatalog), "<Init>g__PayCostItems|5_1")]
		public static void PrefixItemPayCost(CostTypeDef.PayCostContext context, CostTypeDef.PayCostResults result)
		{
			if (context.costTypeDef.itemTier != ItemTier.Tier1) return;
			if (!TryGetInstance(out VoidJunkToScrapTier1 voidJunkToScrapTier1)) return;
			var inv = context.activatorBody.inventory;
			var voidAmount = Math.Min(context.cost, Math.Max(0, inv.GetItemCountPermanent(voidJunkToScrapTier1.ItemDef) - (CanConsumeLastStack!.Value ? 0 : 1)));
			for (var i = 0; i < voidAmount; i++)
			{
				var itemTransformation = new Inventory.ItemTransformation
				{
					originalItemIndex = voidJunkToScrapTier1.ItemDef.itemIndex,
					newItemIndex = ItemIndex.None,
					maxToTransform = 1,
					forbidTempItems = true
				};
				if (itemTransformation.TryTransform(inv, out var tryTransformResult))
				{
					result.AddTakenItemsFromTransformation(tryTransformResult);
					context.cost--;
				}
			}
		}
	}
}