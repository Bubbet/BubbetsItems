using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BubbetsItems.Helpers;
using HarmonyLib;

using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Items;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace BubbetsItems.Items
{
	public class DecreaseBarrierDecay : ItemBase
	{
		public ConfigEntry<string> SkillBlacklist = null!;
		public ConfigEntry<string> SkillWhitelist = null!;
		private static BuffDef BuffDef = null!;
		private static List<SkillDef> _skillDefBlacklist = null!;
		private static List<SkillDef> _skillDefWhitelist = null!;

		protected override void MakeTokens()
		{
			base.MakeTokens();
			AddToken("DECREASEBARRIERDECAY_NAME", "Mechanical Snail");
			// Using skills with a cooldown gives 10% barrier. Regenerates when out of combat. Having barrier gives 20 armor.
			AddToken("DECREASEBARRIERDECAY_DESC", "Using skills with a cooldown gives " + "{0:0%} temporary barrier per second of cooldown. ".Style(StyleEnum.Heal) + "Having " + "temporary barrier ".Style(StyleEnum.Heal) + "gives " + "{1} armor. ".Style(StyleEnum.Utility) + "Has {2} charges, regenerates when out of combat.");
			AddToken("DECREASEBARRIERDECAY_DESC_SIMPLE", "Using skills with a cooldown gives " + "10% temporary barrier. ".Style(StyleEnum.Heal) + "(+5% per stack) ".Style(StyleEnum.Stack) + "Having " + "temporary barrier ".Style(StyleEnum.Heal) + "gives " + "20 armor. ".Style(StyleEnum.Utility) + "(+10 per stack) ".Style(StyleEnum.Stack) + "Has 1 charge " + "(+1 per stack)".Style(StyleEnum.Stack) + " regenerates when out of combat.");
			SimpleDescriptionToken = "DECREASEBARRIERDECAY_DESC_SIMPLE";
			AddToken("DECREASEBARRIERDECAY_PICKUP", "Gain barrier for using skills. Reduce damage taken for having barrier.");
			AddToken("DECREASEBARRIERDECAY_LORE", "");
		}

		protected override void MakeConfigs()
		{
			base.MakeConfigs();
			AddScalingFunction("[a] * 0.01 * [c]", "Barrier Percent Add", desc: "[a] = item count; [c] = skill cooldown");
			AddScalingFunction("[a] * 10 + 10", "Armor Add");
			AddScalingFunction("[a]", "Charges");
		}

		
		protected override void FillDefsFromSerializableCP(SerializableContentPack serializableContentPack)
		{
			base.FillDefsFromSerializableCP(serializableContentPack);
			BuffDef = ScriptableObject.CreateInstance<BuffDef>();
			BuffDef.name = "BuffDefSnailCharges";
			BuffDef.canStack = true;
			BuffDef.isHidden = false;
			//BuffDef.iconSprite = BubbetsItemsPlugin.AssetBundle.LoadAsset<Sprite>("SnailBuff");
			BuffDef.iconSprite = ItemDef.pickupIconSprite;
			serializableContentPack.buffDefs = serializableContentPack.buffDefs.AddItem(BuffDef).ToArray();
		}
		
		protected override void FillRequiredExpansions()
		{
			base.FillRequiredExpansions();
			
			var defaultValue = "";
			var values = string.Join(" ", SkillCatalog._allSkillDefs.Select(x => x.skillName));
			SkillBlacklist = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General, "Mechanical Snail Blacklist", defaultValue, "Skills to not give barrier for, Valid values: " + values);
			SkillBlacklist.SettingChanged += (_, _) => SettingChanged();
			SkillWhitelist = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General, "Mechanical Snail Whitelist", defaultValue, "Skills to bypass cooldown/stock check and give barrier for, Valid values: " + values);
			SkillWhitelist.SettingChanged += (_, _) => SettingChanged();
			SettingChanged();

			if (BubbetsItemsPlugin.RiskOfOptionsEnabled)
			{
				MakeRiskOfOptionsLate();
			}
		}

		private void MakeRiskOfOptionsLate()
		{
			ModSettingsManager.AddOption(new StringInputFieldOption(SkillWhitelist));
			ModSettingsManager.AddOption(new StringInputFieldOption(SkillBlacklist));
		}

		private void SettingChanged()
		{
			_skillDefBlacklist = SkillBlacklist.Value.Split(' ')
				.Select(SkillCatalog.FindSkillIndexByName)
				.Where(index => index != -1)
				.Select(SkillCatalog.GetSkillDef).ToList();
			_skillDefWhitelist = SkillWhitelist.Value.Split(' ')
				.Select(SkillCatalog.FindSkillIndexByName)
				.Where(index => index != -1)
				.Select(SkillCatalog.GetSkillDef).ToList();
		}

		public override string GetFormattedDescription(Inventory? inventory, string? token = null, bool forceHideExtended = false)
		{
			ScalingInfos[0].WorkingContext.c = 1;
			return base.GetFormattedDescription(inventory, token, forceHideExtended);
		}


		[HarmonyPostfix, HarmonyPatch(typeof(CharacterBody), nameof(CharacterBody.OnSkillActivated))]
		public static void SkillActivated(CharacterBody __instance, GenericSkill skill)
		{
			if (!_skillDefWhitelist.Contains(skill.baseSkill) && (skill.baseSkill.baseRechargeInterval <= 0.001 || skill.baseSkill.stockToConsume == 0) || _skillDefBlacklist.Contains(skill.baseSkill) || !__instance.HasBuff(BuffDef)) return;
			if (!TryGetInstance(out DecreaseBarrierDecay inst)) return;
			var inv = __instance.inventory;
			var amt = inv.GetItemCount(inst!.ItemDef);
			if (amt <= 0) return;
			var info = inst.ScalingInfos[0];
			info.WorkingContext.c = skill.baseSkill.baseRechargeInterval;
			if (NetworkServer.active)
			{
				__instance.GetComponent<OutOfCombatSnailBehavior>().ConsumeStack();
				__instance.healthComponent.AddBarrier(__instance.healthComponent.fullHealth *
				                                      info.ScalingFunction(amt));
			}
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

		public static void RecalcStats(CharacterBody characterBody, RecalculateStatsAPI.StatHookEventArgs args)
		{
			if (!TryGetInstance(out DecreaseBarrierDecay inst)) return;
			var inv = characterBody.inventory;
			if (!inv) return;
			var amt = inv.GetItemCount(inst!.ItemDef);
			if (amt <= 0) return;
			if (characterBody.healthComponent.barrier > 0) args.armorAdd += inst.ScalingInfos[1].ScalingFunction(amt);
		}
		
		internal class OutOfCombatSnailBehavior : BaseItemBodyBehavior
		{
			[ItemDefAssociation(useOnServer = true, useOnClient = false)]
			private static ItemDef? GetItemDef()
			{
				return TryGetInstance(out DecreaseBarrierDecay inst) ? inst.ItemDef : null;
			}

			private void OnDisable()
			{
				body.SetBuffCount(BuffDef.buffIndex,0);
			}
			private void FixedUpdate()
			{
				if (body.outOfDanger && dirty && TryGetInstance(out DecreaseBarrierDecay inst))
				{
					count = Mathf.RoundToInt(inst.ScalingInfos[2].ScalingFunction(stack));
					body.SetBuffCount(BuffDef.buffIndex, count);
					dirty = false;
				}
			}

			private int count;
			private bool dirty = true;

			public void ConsumeStack()
			{
				if (count <= 0) return;
				body.SetBuffCount(BuffDef.buffIndex, --count);
				dirty = true;
			}
		}
	}
}