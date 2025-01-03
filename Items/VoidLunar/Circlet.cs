﻿using System.Collections.Generic;
using System.Linq;
using BubbetsItems.Helpers;
using BubbetsItems.ItemBehaviors;
using HarmonyLib;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;

namespace BubbetsItems.Items.VoidLunar
{
	public class Circlet : ItemBase
	{
		protected override void MakeTokens()
		{
			base.MakeTokens();
			var name = GetType().Name.ToUpper();
			SimpleDescriptionToken = name + "_DESC_SIMPLE";
			AddToken(name + "_NAME", "Deluged Circlet");
			var convert = "Converts all Brittle Crowns".Style(StyleEnum.VoidLunar) + ".";
			AddToken(name + "_CONVERT", convert);
			AddToken(name + "_DESC", "Decrease " + "skill cooldowns by {0:0.##%}".Style(StyleEnum.Utility) + " of gold gained. "+"Stop all gold gain for {1} seconds upon being hit. ".Style(StyleEnum.Health));
			AddToken(name + "_DESC_SIMPLE", "Every " + "10 ".Style(StyleEnum.Utility) + "(-50% per stack)".Style(StyleEnum.Stack) + " gold".Style(StyleEnum.Utility) + " earned" + " reduces skill cooldowns by 1 second".Style(StyleEnum.Utility) + ". " + "Temporarily stops any gold from being gained for 5 ".Style(StyleEnum.Health) +"(+5 per stack)".Style(StyleEnum.Stack) +" seconds upon being hit. ".Style(StyleEnum.Health));
			AddToken(name + "_PICKUP", "Reduce " + "skill cooldowns".Style(StyleEnum.Utility) +" from gold gained… " + "BUT stop gold gain on hit. ".Style(StyleEnum.Health) + convert);
			AddToken(name + "_LORE", "");
		}

		protected override void MakeConfigs()
		{
			base.MakeConfigs();
			AddScalingFunction("([m] / [d]) / (10 / [a]) ", "Recharge Reduction", "[a] = item count; [m] = money earned; [d] = run difficulty coefficient", "[m] * 0.01 * [a]");
			AddScalingFunction("5 * [a]", "No Gold Debuff Duration");
		}

		public override string GetFormattedDescription(Inventory? inventory, string? token = null, bool forceHideExtended = false)
		{
			ScalingInfos[0].WorkingContext.m = 1;
			ScalingInfos[0].WorkingContext.d = 1;
			return base.GetFormattedDescription(inventory, token, forceHideExtended);
		}

		protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
		{
			base.FillVoidConversions(pairs);
			AddVoidPairing(nameof(RoR2Content.Items.GoldOnHit));
		}

		protected override void MakeBehaviours()
		{
			base.MakeBehaviours();
			GlobalEventManager.onServerDamageDealt += DamageDealt;
		}

		protected override void DestroyBehaviours()
		{
			base.DestroyBehaviours();
			GlobalEventManager.onServerDamageDealt -= DamageDealt;
		}
		
		private void DamageDealt(DamageReport obj)
		{
			var body = obj.victimBody;
			if (!body) return;
			var inv = body.inventory;
			if (!inv) return;
			var amount = inv.GetItemCount(ItemDef);
			if (amount <= 0) return;
			var info = ScalingInfos[1];
			body.AddTimedBuff(BuffDef, info.ScalingFunction(amount));
		}


		private static BuffDef? _buffDef;
		public static BuffDef? BuffDef => _buffDef ??= BubbetsItemsPlugin.ContentPack.buffDefs.Find("BuffDefCirclet");
		protected override void FillDefsFromSerializableCP(SerializableContentPack serializableContentPack)
		{
			base.FillDefsFromSerializableCP(serializableContentPack);
			// yeahh code based content because TK keeps fucking freezing
			var buff = ScriptableObject.CreateInstance<BuffDef>();
			buff.isDebuff = true;
			buff.name = "BuffDefCirclet";
			buff.buffColor = new Color(r: 0.5254902f, g: 0, b: 0.79607844f, a: 1);
			buff.iconSprite = BubbetsItemsPlugin.AssetBundle.LoadAsset<Sprite>("textBuffNoMoney-SuckACockIcon");
			serializableContentPack.buffDefs = serializableContentPack.buffDefs.AddItem(buff).ToArray();
		}

		[HarmonyPrefix, HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.money), MethodType.Setter)]
		public static bool DisableMoney(CharacterMaster __instance, uint value)
		{
			var inv = __instance.inventory;
			if (!inv) return true;
			if (!TryGetInstance(out Circlet inst)) return true;
			var amount = inv.GetItemCount(inst.ItemDef);
			if (amount <= 0) return true;
			var change = (int)value - (int)__instance.money;
			if (change < 0) return true;
			var body = __instance.GetBody();
			if (!body) return true;
			if (body.HasBuff(BuffDef)) return false;

			var info = inst.ScalingInfos[0];
			info.WorkingContext.m = change;
			info.WorkingContext.d = Run.instance.difficultyCoefficient;
			var reduction = info.ScalingFunction(amount);

			var locator = body.skillLocator;
			locator.primary.rechargeStopwatch += reduction;
			locator.secondary.rechargeStopwatch += reduction;
			locator.utility.rechargeStopwatch += reduction;
			locator.special.rechargeStopwatch += reduction;
			
			return true;
		}
	}
}