﻿using System.Linq;
using BepInEx.Configuration;
using EntityStates;
using HarmonyLib;
using KinematicCharacterController;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbetsItems.Equipments
{
	public class GyroscopicWhisk : EquipmentBase
	{
		private GameObject _indicator = null!;
		private ConfigEntry<bool> _filterOutBosses = null!;
		private ConfigEntry<bool> _filterOutPlayers = null!;

		protected override void MakeTokens()
		{
			base.MakeTokens();
			AddToken("GYROSCOPICWHISK_NAME", "SOM's Gyroscopic Whisk");
			AddToken("GYROSCOPICWHISK_DESC", "Spin enemies until they fly to the sky and die.");
			AddToken("GYROSCOPICWHISK_PICKUP", "Spin enemies until they die.");
			AddToken("GYROSCOPICWHISK_LORE", "SOM told Julienne to stop thinking of bad ideas, they said no.");
		}

		protected override void MakeConfigs()
		{
			base.MakeConfigs();
			_filterOutBosses = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General, "Gyroscopic Whisk Filter Bosses", true, "Should gyroscopic whisk filter out bosses.");
			_filterOutPlayers = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General, "Gyroscopic Whisk Filter Players", false, "Should gyroscopic whisk filter out players.");
			_indicator = BubbetsItemsPlugin.AssetBundle.LoadAsset<GameObject>("WhiskIndicator");
		}

		public override void MakeRiskOfOptions()
		{
			base.MakeRiskOfOptions();
			ModSettingsManager.AddOption(new CheckBoxOption(_filterOutBosses));
			ModSettingsManager.AddOption(new CheckBoxOption(_filterOutPlayers));
		}

		public override EquipmentActivationState PerformEquipment(EquipmentSlot equipmentSlot)
		{
			var trg = equipmentSlot.currentTarget.hurtBox;
			if (!trg) return EquipmentActivationState.DidNothing;
			var hc = trg.healthComponent;
			if (!hc) return EquipmentActivationState.DidNothing;
			if (NetworkServer.active)
				hc.gameObject.AddComponent<WhiskBehavior>();
			return EquipmentActivationState.ConsumeStock;
		}

		public override bool UpdateTargets(EquipmentSlot equipmentSlot)
		{
			base.UpdateTargets(equipmentSlot);
            
			if (equipmentSlot.stock <= 0) return false;

			equipmentSlot.ConfigureTargetFinderForEnemies();
			equipmentSlot.currentTarget = new EquipmentSlot.UserTargetInfo(equipmentSlot.targetFinder.GetResults().FirstOrDefault(x => x.healthComponent && !x.healthComponent.GetComponent<WhiskBehavior>() && !(_filterOutBosses.Value && x.healthComponent.body.isBoss) && !(_filterOutPlayers.Value && x.healthComponent.body.isPlayerControlled)));

			if (!equipmentSlot.currentTarget.transformToIndicateAt) return false;
            
			equipmentSlot.targetIndicator.visualizerPrefab = _indicator;
			return true;
		}

		[HarmonyPrefix, HarmonyPatch(typeof(MapZone), nameof(MapZone.TeleportBody))]
		public static bool KillOnOOB(CharacterBody characterBody)
		{
			if (!characterBody.GetComponent<WhiskBehavior>()) return true;
			characterBody.master.TrueKill();
			return false;
		}
	}

	public class WhiskBehavior : MonoBehaviour
	{
		private float _speed = 10f;
		private CharacterBody _body = null!;

		private void Awake()
		{
			//kcm = GetComponent<KinematicCharacterMotor>();
			_body = GetComponent<CharacterBody>();
			foreach (var entityStateMachine in GetComponents<EntityStateMachine>())
			{
				if (entityStateMachine.state is GenericCharacterMain)
				{
					entityStateMachine.SetNextState(new Uninitialized());
					break;
				}

				if (entityStateMachine.state is FlyState)
				{
					entityStateMachine.SetNextState(new Uninitialized());
					break;
				}
			}
		}

		private void FixedUpdate()
		{
			_speed += Time.fixedDeltaTime * 5f;
			if (_body.characterMotor)
			{
				_body.characterDirection.forward = Quaternion.Euler(0, _speed, 0) * _body.characterDirection.forward;
				if (_speed > 50f)
				{
					var motor = _body.characterMotor;
					BrokenClock.Velocity.SetValue(motor, ((IPhysMotor) motor).velocity + Vector3.up * 10f);
				}
			}
			else
			{
				var dir = _body.GetComponent<RigidbodyDirection>();
				dir.enabled = false;
				var transf1 = _body.GetComponent<Transform>();
				
				transf1.forward = Quaternion.Euler(0, _speed, 0) * transf1.forward;
				
				var mot = _body.GetComponent<RigidbodyMotor>() as IPhysMotor;
				if (_speed > 50f)
				{
					var motVelocityAuthority = mot.velocityAuthority;
					motVelocityAuthority.y += 10f;
					mot.velocityAuthority = motVelocityAuthority;
				}
			}

			/*
			kcm.CharacterForward = Quaternion.Euler(0, speed, 0) * kcm.CharacterForward;
			if (speed > 10f)
			{
				var kcmVelocity = kcm.BaseVelocity;
				kcmVelocity.y += 10f;
				kcm.BaseVelocity = kcmVelocity;
			}*/
		}
	}
}