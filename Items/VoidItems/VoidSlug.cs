﻿using System.Collections.Generic;
using BubbetsItems.Helpers;
using HarmonyLib;

using RoR2;

namespace BubbetsItems.Items
{
	public class VoidSlug : ItemBase
	{
		protected override void MakeTokens()
		{
			base.MakeTokens();
			AddToken("VOIDSLUG_NAME", "Adrenaline Sprout");
			var convert = "Corrupts all Cautious Slugs".Style(StyleEnum.Void) + ".";
			AddToken("VOIDSLUG_CONVERT", convert);
			AddToken("VOIDSLUG_DESC", "Gain "+ "{0:0.00} regen".Style(StyleEnum.Heal)+" per missing "+"health".Style(StyleEnum.Health)+". When in "+"danger".Style(StyleEnum.Health)+". ");
			AddToken("VOIDSLUG_DESC_SIMPLE", "Gain "+ "0.03 regen ".Style(StyleEnum.Heal)+ "(scales linearly per stack)".Style(StyleEnum.Stack) + " per missing " + "health ".Style(StyleEnum.Health)+"while in " + "danger".Style(StyleEnum.Health) + ". ");
			SimpleDescriptionToken = "VOIDSLUG_DESC_SIMPLE";
			AddToken("VOIDSLUG_PICKUP", "Gain "+"regen".Style(StyleEnum.Heal)+" for missing "+"health".Style(StyleEnum.Health)+". When in "+"danger".Style(StyleEnum.Health)+". " + convert);
			AddToken("VOIDSLUG_LORE", "");
		}
		protected override void MakeConfigs()
		{
			base.MakeConfigs();
			AddScalingFunction("[h] * (1 - 100/(102+[a]))", "Regen", "[h] = Missing health, [a] = Item count", "[h] * 0.005 * [a] + 0.0196");
		}
		protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
		{
			base.FillVoidConversions(pairs);
			AddVoidPairing(nameof(RoR2Content.Items.HealWhileSafe));
		}

		public override string GetFormattedDescription(Inventory? inventory, string? token = null, bool forceHideExtended = false)
		{
			ScalingInfos[0].WorkingContext.h = 1f;
			return base.GetFormattedDescription(inventory, token, forceHideExtended);
		}
		

		[HarmonyPostfix, HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.Heal))]
		public static void HealServer(HealthComponent __instance)
		{
			if (!__instance || !__instance.body || !__instance.body.inventory) return;
			if (!TryGetInstance(out VoidSlug voidSlug)) return;
			var count = __instance.body.inventory.GetItemCount(voidSlug.ItemDef);
			if (count <= 0 || __instance.missingCombinedHealth < 0.1f) return;
			__instance.body.statsDirty = true;
		}

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

		public static void RecalcStats(CharacterBody __instance, RecalculateStatsAPI.StatHookEventArgs args)
		{
			if (!TryGetInstance(out VoidSlug voidSlug)) return;
			if (voidSlug == null) return;
			var count = __instance.inventory?.GetItemCount(voidSlug.ItemDef) ?? 0;
			if (count <= 0 || __instance.outOfDanger) return;
			var info = voidSlug.ScalingInfos[0];
			info.WorkingContext.h = __instance.healthComponent.missingCombinedHealth;
			__instance.regen += info.ScalingFunction(count);
		}
	}
}