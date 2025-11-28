using System;
using System.Collections.Generic;
using BubbetsItems.Helpers;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;

using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbetsItems.Items.VoidLunar
{
	public class Imperfect : ItemBase
	{
		protected override void MakeTokens()
		{
			base.MakeTokens();
			var name = GetType().Name.ToUpper();
			SimpleDescriptionToken = name + "_DESC_SIMPLE";
			AddToken(name + "_NAME", "Imperfection");
			var convert = "Converts all Transcendence".Style(StyleEnum.VoidLunar) + ".";
			AddToken(name + "_CONVERT", convert);
			AddToken(name + "_DESC", "Converts all but 1 shield into maximum health. Gain "+"{0:0%} shield".Style(StyleEnum.Utility) +" and " + "{1} armor. ".Style(StyleEnum.Health));
			AddToken(name + "_DESC_SIMPLE", "Converts all current " + "shield".Style(StyleEnum.Utility) + " into " + "maximum health".Style(StyleEnum.Health) + ". Reduce " + "armor".Style(StyleEnum.Health) + " by " + "-25".Style(StyleEnum.Health) + " (-25 per stack)".Style(StyleEnum.Stack) + " but gain an additional " + "25% shield".Style(StyleEnum.Utility) + " (+25% per stack)".Style(StyleEnum.Stack) + ". ");
			AddToken(name + "_PICKUP", "Convert all your shield into health. "+"Increase maximum shield…".Style(StyleEnum.Utility) +" BUT your armor is frail. ".Style(StyleEnum.Health) + convert);
			AddToken(name + "_LORE", "");
		}

		protected override void MakeConfigs()
		{
			base.MakeConfigs();
			AddScalingFunction("0.25 * [a]", "Shield Gain");
			AddScalingFunction("-25 * [a]", "Armor Add");
		}

		protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
		{
			base.FillVoidConversions(pairs);
			AddVoidPairing(nameof(RoR2Content.Items.ShieldOnly));
		}

		//[HarmonyPostfix, HarmonyPatch(typeof(CharacterBody), nameof(CharacterBody.RecalculateStats))] //ODO i need to write il for this because at the bottom of recalc stats there is some code that heals/removes health based on the max hp and that might be what is causing this weird ass behavior
		public static void DoEffect(CharacterBody __instance)
		{
			if (!__instance) return;
			var inv = __instance.inventory;
			if (!inv) return;
			if (!TryGetInstance<Imperfect>(out var inst)) return;
			var amount = inv.GetItemCount(inst.ItemDef);
			if (amount <= 0) return;
			__instance.maxShield *= 1 + inst.ScalingInfos[0].ScalingFunction(amount);
			__instance.maxHealth += __instance.maxShield - 1;
			__instance.maxShield = 1;
			__instance.armor += inst.ScalingInfos[1].ScalingFunction(amount);
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

		private void RecalcStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
		{
			var inv = sender.inventory;
			if (!inv) return;
			if (!TryGetInstance(out Imperfect inst)) return;
			var amount = inv.GetItemCount(inst.ItemDef);
			if (amount <= 0) return;
			args.armorAdd += inst.ScalingInfos[1].ScalingFunction(amount);
			args.shieldMultAdd += inst.ScalingInfos[0].ScalingFunction(amount);
		}

		
		[HarmonyILManipulator, HarmonyPatch(typeof(CharacterBody), nameof(CharacterBody.RecalculateStats))]
		public static void PatchIl(ILContext il)
		{
			var c = new ILCursor(il);
			c.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt<CharacterBody>("set_" + nameof(CharacterBody.maxBarrier)));
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Action<CharacterBody>>(cb =>
			{
				var inv = cb.inventory;
				if (!inv) return;
				if (!TryGetInstance(out Imperfect inst)) return;
				var amount = inv.GetItemCount(inst.ItemDef);
				if (amount <= 0) return;
				cb.maxHealth += cb.maxShield - 1;
				cb.maxShield = 1;
			});
		}
		

		//[HarmonyPostfix, HarmonyPatch(typeof(CharacterBody), nameof(CharacterBody.RecalculateStats))]
		public static void UsedToBePatchIL(CharacterBody __instance)
		{
			if (!TryGetInstance(out Imperfect inst)) return;
			var inv = __instance.inventory;
			if (inv && inv.GetItemCount(inst.ItemDef) > 0 && __instance.maxShield > 1)
			{
				var maxShield = __instance.maxShield;
				__instance.maxHealth += __instance.maxShield - 1;
				__instance.maxShield = 1;
				
				if (NetworkServer.active)
				{
					float num118 = __instance.maxShield - maxShield;
					if (num118 > 0f)
					{
						__instance.healthComponent.RechargeShield(num118);
					}
					else if (__instance.healthComponent.shield > __instance.maxShield)
					{
						__instance.healthComponent.Networkshield = Mathf.Max(__instance.healthComponent.shield + num118, __instance.maxShield);
					}
				}
            }
        }
	}
}