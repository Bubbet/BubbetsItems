﻿using System;
using BubbetsItems.Items;
using EntityStates;
using RoR2;
using RoR2.Items;
using UnityEngine;

namespace BubbetsItems.ItemBehaviors
{
	public class BunnyFootBehavior : BaseItemBodyBehavior
	{
		public Vector3 hitGroundVelocity;
		public float hitGroundTime;
		private EntityStateMachine _bodyMachine = null!;

		[ItemDefAssociation(useOnServer = true, useOnClient = true)]
		private static ItemDef? GetItemDef()
		{
			return SharedBase.TryGetInstance<BunnyFoot>(out var inst) ? inst.ItemDef : null;
		}

		private void OnEnable()
		{
			if (!body.characterMotor) return;
			body.characterMotor.onHitGroundAuthority += HitGround;
			_bodyMachine = EntityStateMachine.FindByCustomName(gameObject, "Body");
		}

		private void OnDisable()
		{
			if (!body.characterMotor) return;
			body.characterMotor.onHitGroundAuthority -= HitGround;
		}

		private void HitGround(ref CharacterMotor.HitGroundInfo hitgroundinfo)
		{
			if (!SharedBase.TryGetInstance<BunnyFoot>(out var inst)) return;
			hitGroundVelocity = hitgroundinfo.velocity;
			hitGroundTime = Time.time;
			if (body.hasEffectiveAuthority && body.inputBank.jump.down &&
			    stack >= inst.ScalingInfos[4].ScalingFunction(stack))
				shouldJump = true;
		}

		public bool shouldJump;

		public void Update()
		{
			if (!shouldJump || !body.characterMotor.isGrounded) return;
			if (_bodyMachine.state is not GenericCharacterMain state) return;
			state.jumpInputReceived = true;
			state.ProcessJump();
			shouldJump = false;
		}
	}
}