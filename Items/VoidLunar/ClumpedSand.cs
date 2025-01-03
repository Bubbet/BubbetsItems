﻿using System.Collections.Generic;
using BubbetsItems.Helpers;
using HarmonyLib;

using RoR2;

namespace BubbetsItems.Items.VoidLunar
{
	public class ClumpedSand : ItemBase
	{
		protected override void MakeTokens()
		{
			base.MakeTokens();
			var name = GetType().Name.ToUpper();
			SimpleDescriptionToken = name + "_DESC_SIMPLE";
			AddToken(name + "_NAME", "Clumped Sand");
			var convert = "Converts all Shaped Glass".Style(StyleEnum.VoidLunar) + ".";
			AddToken(name + "_CONVERT", convert);
			AddToken(name + "_DESC", "All attacks " + "hit {0} more times".Style(StyleEnum.Utility) + " for "+"{2:0%} damage. ".Style(StyleEnum.Damage) + "{1} hp/s to your regen. ".Style(StyleEnum.Health));
			AddToken(name + "_DESC_SIMPLE", "All attacks will " + "hit for an additional 1 ".Style(StyleEnum.Utility) + "(+1 per stack)".Style(StyleEnum.Stack) +" times".Style(StyleEnum.Utility) + " for " + "50% base damage".Style(StyleEnum.Damage) + ", but" + " reduces health regeneration by -3/s".Style(StyleEnum.Health) + " (-3/s per stack)".Style(StyleEnum.Stack) + ". ");
			AddToken(name + "_PICKUP", "Damage is dealt again at a ".Style(StyleEnum.Utility) + "weaker state… ".Style(StyleEnum.Damage) + "BUT gain negative regeneration. ".Style(StyleEnum.Health) + convert);
			AddToken(name + "_LORE", "");
		}

		protected override void MakeConfigs()
		{
			base.MakeConfigs();
			AddScalingFunction("[a]", "Attack Hit Count");
			AddScalingFunction("-3 * [a]", "Regen Add");
			AddScalingFunction("0.5", "Damage Mult");
		}

		protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
		{
			base.FillVoidConversions(pairs);
			AddVoidPairing(nameof(RoR2Content.Items.LunarDagger));
		}


		public static DamageInfo? mostRecentInfo;
		[HarmonyPrefix, HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.TakeDamageProcess))]
		public static void DuplicateDamage(HealthComponent __instance, DamageInfo damageInfo)
		{
			if (mostRecentInfo == null)
			{
				if (!damageInfo.attacker) return;
				var body = damageInfo.attacker.GetComponent<CharacterBody>();
				if (!body) return;
				var inv = body.inventory;
				if (!inv) return;
				if (!TryGetInstance(out ClumpedSand inst)) return;
				var amount = inv.GetItemCount(inst.ItemDef);
				if (amount <= 0) return;
				damageInfo.damage *= inst.ScalingInfos[2].ScalingFunction(amount);
				mostRecentInfo = damageInfo;
				for (var i = 0; i < inst.ScalingInfos[0].ScalingFunction(amount); i++)
				{
					__instance.TakeDamage(damageInfo);
				}
				mostRecentInfo = null;
			}
		}

		protected override void MakeBehaviours()
		{
			base.MakeBehaviours();
			RecalculateStatsAPI.GetStatCoefficients += ReduceRegen;
		}

		protected override void DestroyBehaviours()
		{
			base.DestroyBehaviours();
			RecalculateStatsAPI.GetStatCoefficients -= ReduceRegen;
		}
		
		public static void ReduceRegen(CharacterBody __instance, RecalculateStatsAPI.StatHookEventArgs args)
		{
			if (!__instance) return;
			var inv = __instance.inventory;
			if (!inv) return;
			if (!TryGetInstance(out ClumpedSand inst)) return;
			var amount = inv.GetItemCount(inst.ItemDef);
			if (amount <= 0) return;
			__instance.regen += inst.ScalingInfos[1].ScalingFunction(amount);
		}
	}
}