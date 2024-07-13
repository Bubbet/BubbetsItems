using System;
using BubbetsItems.Items.VoidLunar;
using RoR2;
using RoR2.Items;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbetsItems.ItemBehaviors
{
    public class TarnishedBehavior : BaseItemBodyBehavior
    {
        [ItemDefAssociationAttribute(useOnServer = true, useOnClient = true)]
        private static ItemDef? GetItemDef()
        {
            var instance = SharedBase.GetInstance<Tarnished>();
            return instance?.ItemDef;
        }

        private int _oldStack;
        private Tarnished? _instance;

        public void Update()
        {
            if (_oldStack == stack) return;

            OnStackChange();
            if (body)
                _oldStack = stack;
        }

        private void OnStackChange()
        {
            if (stack > _oldStack && body)
            {
                var toAdd = Mathf.FloorToInt(_instance!.scalingInfos[0].ScalingFunction(stack) -
                                             _instance.scalingInfos[0].ScalingFunction(_oldStack));
                if (NetworkServer.active)
                    body.SetBuffCount(Tarnished.BuffDef!.buffIndex, body.GetBuffCount(Tarnished.BuffDef!.buffIndex) + toAdd);
                body.master.OnInventoryChanged();
            }
        }

        private void Awake()
        {
            base.Awake();
            _instance = SharedBase.GetInstance<Tarnished>();
        }

        private void OnDisable()
        {
            if (body.HasBuff(Tarnished.BuffDef)) body.SetBuffCount(Tarnished.BuffDef!.buffIndex, 0);
            if (body.HasBuff(Tarnished.BuffDef2))
                body.RemoveOldestTimedBuff(Tarnished.BuffDef2);
            
            if (body && body.master)
                body.master.OnInventoryChanged();
        }
    }
}