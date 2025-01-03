﻿using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BubbetsItems.Helpers;
using HarmonyLib;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace BubbetsItems.Items.VoidLunar
{
	public class AbstractedLocus : ItemBase
	{
		public static ConfigEntry<bool> DisableEnemyDamageInArena = null!;
		public static ConfigEntry<int> ItemLimit = null!;

		protected override void MakeTokens()
		{
			base.MakeTokens();
			var name = GetType().Name.ToUpper();
			SimpleDescriptionToken = name + "_DESC_SIMPLE";
			AddToken(name + "_NAME", "Abstracted Locus");
			var convert = "Converts all Focused Convergences".Style(StyleEnum.VoidLunar) + ".";
			AddToken(name + "_CONVERT", convert);
			AddToken(name + "_DESC", "Teleporter zone is " + "{0:0%} larger.".Style(StyleEnum.Utility) + " Outside of the teleporter radius is filled with "+"Void Fog.".Style(StyleEnum.Void)+" Staying in the "+"Fog".Style(StyleEnum.Void)+" charges the teleporter "+ "{1:0%} faster".Style(StyleEnum.Utility) + " per player outside. ");
			AddToken(name + "_DESC_SIMPLE", "Teleporter zone is " + "50% ".Style(StyleEnum.Utility) + "(+20% per stack)".Style(StyleEnum.Stack) +" bigger.".Style(StyleEnum.Utility) + " Outside of the teleporter radius is filled with " + "Void Fog.".Style(StyleEnum.Void) + " Staying in the " + "Void Fog".Style(StyleEnum.Void) + " charges the teleporters " + "60% ".Style(StyleEnum.Utility) + "(+60% per stack) ".Style(StyleEnum.Stack) + "faster. ".Style(StyleEnum.Utility));
			AddToken(name + "_PICKUP", "Teleporter zone is " + "larger".Style(StyleEnum.Utility) + ", outside of the zone is " + "void fog".Style(StyleEnum.Void) + ", being in the " + "fog".Style(StyleEnum.Void) + " increases teleporter charge speed. ".Style(StyleEnum.Utility) + convert);
			AddToken(name + "_LORE", "");
		}

		protected override void MakeConfigs()
		{
			base.MakeConfigs();
			AddScalingFunction("[r] * ([a] * 0.2 + 1.3)", "Teleporter Radius", desc: "[a] = item count; [r] = current radius;");
			AddScalingFunction("[r] * ([a] * 0.6 * Max(0, [p]) + 1)", "Void Fog Charge Increase", desc: "[a] = item count; [p] = outside players; [r] = charging rate");
			DisableEnemyDamageInArena = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General, "Abstracted Locus Disable Enemy Damage In Arena", false, "Should the void fog hurt the enemies in the Void Fields.");
			ItemLimit = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General, "Abstracted Locus Item Limit", -1, "Limit of locus amongst team, -1 is infinite.");
		}

		public override string GetFormattedDescription(Inventory? inventory, string? token = null, bool forceHideExtended = false)
		{
			ScalingInfos[0].WorkingContext.r = 1;
			ScalingInfos[1].WorkingContext.p = 1;
			ScalingInfos[1].WorkingContext.r = 1;
			return base.GetFormattedDescription(inventory, token, forceHideExtended);
		}

		public override void MakeRiskOfOptions()
		{
			base.MakeRiskOfOptions();
			ModSettingsManager.AddOption(new CheckBoxOption(DisableEnemyDamageInArena));
			ModSettingsManager.AddOption(new IntSliderOption(ItemLimit, new IntSliderConfig {min = -1, max = 40}));
		}

		protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
		{
			base.FillVoidConversions(pairs);
			AddVoidPairing(nameof(RoR2Content.Items.FocusConvergence));
		}

		[HarmonyPostfix, HarmonyPatch(typeof(HoldoutZoneController), nameof(HoldoutZoneController.Start))]
		public static void AddBehaviour(HoldoutZoneController __instance)
		{
			if (__instance.applyFocusConvergence)
			{
				__instance.gameObject.AddComponent<AbstractedLocusController>();
			}
		}
	}

	public class AbstractedLocusController : MonoBehaviour
	{
		private HoldoutZoneController _holdoutZoneController = null!;
		private Run.FixedTimeStamp _enabledTime;
		public int amount;
		private float _currentValue;
		private readonly Color _materialColor =  new Color( 3.9411764f, 0f, 5f, 1f);
		private AbstractedLocus _inst = null!;
		private FogDamageController _fogController = null!;

		private void Awake()
		{
			SharedBase.TryGetInstance(out _inst);
			_holdoutZoneController = GetComponent<HoldoutZoneController>();
			
			var parent = GameObject.Find("AbstractedLocusFog(Clone)");
			if (parent == null)
			{
				var asset = BubbetsItemsPlugin.AssetBundle.LoadAsset<GameObject>("AbstractedLocusFog");
				if (NetworkServer.active)
				{
					parent = Instantiate(asset);
					NetworkServer.Spawn(parent);
				}
			}
			
			_fogController = parent!.GetComponent<FogDamageController>();
			_fogController.AddSafeZone(_holdoutZoneController);
		}
		private void OnEnable()
		{
			_enabledTime = Run.FixedTimeStamp.now;
			_holdoutZoneController.calcRadius += ApplyRadius;
			_holdoutZoneController.calcChargeRate += ApplyRate;
			_holdoutZoneController.calcColor += ApplyColor;
		}
		private void OnDisable()
		{
			_holdoutZoneController.calcColor -= ApplyColor;
			_holdoutZoneController.calcChargeRate -= ApplyRate;
			_holdoutZoneController.calcRadius -= ApplyRadius;
		}

		private void FixedUpdate()
		{
			amount = Util.GetItemCountForTeam(_holdoutZoneController.chargingTeam, _inst.ItemDef.itemIndex, true, false);
			if (_enabledTime.timeSince < HoldoutZoneController.FocusConvergenceController.startupDelay)
			{
				amount = 0;
			}

			if (AbstractedLocus.ItemLimit.Value > -1)
			{
				amount = Mathf.Min(amount, AbstractedLocus.ItemLimit.Value);
			}

			var target = (float) amount > 0f ? 1f : 0f;
			var num = Mathf.MoveTowards(_currentValue, target, 5f * Time.fixedDeltaTime); // TODO replace 5 with configurable cap
			if (_currentValue <= 0f && num > 0f)
			{
				//Util.PlaySound("Play_item_lunar_focusedConvergence", gameObject);
			}
			_currentValue = num;
		}

		private void ApplyRadius(ref float radius)
		{
			if (amount <= 0) return;
			var info = _inst.ScalingInfos[0];
			var context = info.WorkingContext;
			context.r = radius;
			radius = info.ScalingFunction(amount);
		}

		private void ApplyRate(ref float rate)
		{
			if (amount <= 0) return;
			var living = HoldoutZoneController.CountLivingPlayers(_holdoutZoneController.chargingTeam);
			var charging = HoldoutZoneController.CountPlayersInRadius(_holdoutZoneController, transform.position, _holdoutZoneController.currentRadius * _holdoutZoneController.currentRadius, _holdoutZoneController.chargingTeam);
			var outside = living - charging;
			
			rate += Mathf.Pow((float)outside / living, _holdoutZoneController.playerCountScaling) / _holdoutZoneController.baseChargeDuration;
			
			var info = _inst.ScalingInfos[1];
			var context = info.WorkingContext;
			context.p = outside;
			context.r = rate;
			rate = info.ScalingFunction(amount);
		}

		private void ApplyColor(ref Color color)
		{
			color = Color.Lerp(color, _materialColor, HoldoutZoneController.FocusConvergenceController.colorCurve.Evaluate(_currentValue));
		}
	}

	public class AbstractedLocusFogController : MonoBehaviour
	{
		private FogDamageController _fog = null!;

		public void Awake()
		{
			_fog = GetComponent<FogDamageController>();
			_fog.dangerBuffDef = RoR2Content.Buffs.VoidFogMild;

			if (AbstractedLocus.DisableEnemyDamageInArena.Value && SceneManager.GetActiveScene().name == "arena")
			{
				var filter = GetComponent<TeamFilter>();
				filter.teamIndex = TeamIndex.Player;
				_fog.invertTeamFilter = false;
			}
		}

		private bool? _lastEnabled;
		public void FixedUpdate()
		{
			var fogEnabled = _fog.safeZones.Any(x =>
			{
				var hold = x as HoldoutZoneController;
				if (!hold) return false;
				var locus = hold!.GetComponent<AbstractedLocusController>();
				return !hold.wasCharged && locus && locus.amount > 0;
			});
			if (fogEnabled == _lastEnabled) return;
			_fog.enabled = fogEnabled;
			_lastEnabled = fogEnabled;
		}
	} 
}