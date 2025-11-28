using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BubbetsItems.Helpers;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using RoR2.ContentManagement;
using RoR2.ExpansionManagement;
using RoR2.Networking;
using UnityEngine.Networking;

namespace BubbetsItems
{
    [HarmonyPatch]
    public abstract class EquipmentBase : SharedBase
    {
        protected override void MakeConfigs()
        {
            var name = GetType().Name;
            Enabled = sharedInfo.ConfigFile.Bind("Disable Equipments", name, true, "Should this equipment be enabled.");
        }

        //[SystemInitializer(typeof(EquipmentCatalog))]
        public static void ApplyPostEquipments()
        {
            foreach (var equip in Equipments) equip.PostEquipmentDef();
        }

        private void CooldownChanged(object sender, EventArgs e)
        {
            EquipmentDef.cooldown = Cooldown.Value;
        }

        public override void MakeRiskOfOptions()
        {
            base.MakeRiskOfOptions();
            var config = new SliderConfig() { min = 0, max = 80 };
            ModSettingsManager.AddOption(new SliderOption(Cooldown, config));
        }

        public virtual void AuthorityEquipmentPress(EquipmentSlot equipmentSlot)
        {
        }

        public virtual void PerformClientAction(EquipmentSlot equipmentSlot, EquipmentActivationState state)
        {
        }

        public virtual EquipmentActivationState PerformEquipment(EquipmentSlot equipmentSlot)
        {
            return EquipmentActivationState.DidNothing;
        }

        public virtual void OnUnEquip(Inventory inventory, EquipmentState newEquipmentState)
        {
        }

        public virtual void OnEquip(Inventory inventory, EquipmentState? oldEquipmentState)
        {
        }

        public virtual bool UpdateTargets(EquipmentSlot equipmentSlot)
        {
            return false;
        }

        protected virtual void PostEquipmentDef()
        {
            var name = GetType().Name;
            Cooldown = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.EquipmentCooldowns, name,
                EquipmentDef ? EquipmentDef.cooldown : 15f, "Cooldown for this equipment.");
            Cooldown.SettingChanged += CooldownChanged;
            CooldownChanged(null, null);
        }

        public EquipmentDef EquipmentDef = null!;

        private static IEnumerable<EquipmentBase>? _equipments;
        internal ConfigEntry<float> Cooldown = null!;
        public static IEnumerable<EquipmentBase> Equipments => _equipments ??= Instances.OfType<EquipmentBase>();

        public enum EquipmentActivationState
        {
            ConsumeStock,
            DontConsume,
            DidNothing
        }

        //Can manually call CallRpcOnClientEquipmentActivationRecieved maybe?


        /*
        [HarmonyPrefix, HarmonyPatch(typeof(EquipmentSlot), nameof(EquipmentSlot.RpcOnClientEquipmentActivationRecieved))]
        public static void PerformEquipmentActionRpc(EquipmentSlot __instance) // third, all clients except host and authority, only on successful equipmentAction(second)
        {
            if (NetworkServer.active) return;
            //if (__instance.characterBody.hasEffectiveAuthority) return;
            var boo = false;
            PerformEquipmentAction(__instance, EquipmentCatalog.GetEquipmentDef(__instance.equipmentIndex), ref boo);
        }*/

        [HarmonyPrefix,
         HarmonyPatch(typeof(EquipmentSlot), nameof(EquipmentSlot.InvokeRpcRpcOnClientEquipmentActivationRecieved))]
        public static bool NetReceive(NetworkBehaviour obj, NetworkReader reader) // 2.5
        {
            EquipmentActivationState state = EquipmentActivationState.ConsumeStock;
            try
            {
                state = (EquipmentActivationState)reader.ReadByte();
            }
            catch (IndexOutOfRangeException)
            {
            }

            var __instance = (EquipmentSlot)obj;
            var equipmentDef = EquipmentCatalog.GetEquipmentDef(__instance.equipmentIndex);
            var equipment = Equipments.FirstOrDefault(x => x.EquipmentDef == equipmentDef);
            if (equipment != null)
            {
                equipment.PerformClientAction(__instance, state);
                return false;
            }

            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(EquipmentSlot), nameof(EquipmentSlot.CallCmdExecuteIfReady))]
        public static void PerformEquipmentActionClient(EquipmentSlot __instance) // first, activator client
        {
            if (!__instance.characterBody.hasEffectiveAuthority) return;
            if (NetworkServer.active) return; // dont call here because it'll call in second instead
            if (__instance.equipmentIndex == EquipmentIndex.None || __instance.stock <= 0) return;

            var equipmentDef = EquipmentCatalog.GetEquipmentDef(__instance.equipmentIndex);
            var equipment = Equipments.FirstOrDefault(x => x.EquipmentDef == equipmentDef);
            equipment?.AuthorityEquipmentPress(__instance);
        }
        //OnEquipmentExecuted runs EquipmentSlot.onServerEquipmentActivated and it runs on server right after calling the rpc so somewhere between second and third, maybe after third

        [HarmonyPrefix, HarmonyPatch(typeof(EquipmentSlot), nameof(EquipmentSlot.PerformEquipmentAction))]
        public static bool PerformEquipmentAction(EquipmentSlot __instance, EquipmentDef equipmentDef,
            ref bool __result) // second, server
        {
            var equipment = Equipments.FirstOrDefault(x => x.EquipmentDef == equipmentDef);
            if (equipment == null) return true;

            try
            {
                var state = equipment.PerformEquipment(__instance);
                __result = state == EquipmentActivationState.ConsumeStock;
                if (state != EquipmentActivationState.ConsumeStock && NetworkServer.active)
                {
                    NetworkWriter networkWriter = new();
                    networkWriter.StartMessage(2); // 2 being rpc
                    networkWriter.WritePackedUInt32((uint)EquipmentSlot.kRpcRpcOnClientEquipmentActivationRecieved);
                    networkWriter.Write(__instance.GetComponent<NetworkIdentity>().netId);
                    networkWriter.Write((byte)state); // this may end up breaking other mods, maybe.
                    // if that happens use my own packeduint32 and hook static constructor of equipmentslot and call NetworkBehaviour.RegisterRpcDelegate with my id and point it to a static method here
                    // NetworkBehaviour.RegisterRpcDelegate(typeof(EquipmentSlot), myID, new NetworkBehaviour.CmdDelegate(EquipmentBase.InvokeRpcEquipment));
                    // rpcName is unused, not sure why it even exists
                    __instance.SendRPCInternal(networkWriter, 0, "RpcOnClientEquipmentActivationRecieved");
                }
            }
            catch (Exception e)
            {
                equipment.sharedInfo.Logger?.LogError(e);
            }

            return false;
        }

        [HarmonyILManipulator, HarmonyPatch(typeof(EquipmentSlot), nameof(EquipmentSlot.UpdateTargets))]
        public static void UpdateTargetsIL(ILContext il)
        {
            var c = new ILCursor(il);
            var activeFlag = -1;
            c.GotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchLdfld<EquipmentSlot>("targetIndicator"),
                x => x.MatchLdloc(out activeFlag)
            );
            c.Index--;
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<EquipmentSlot, bool>>(UpdateTargetsHook);
            c.Emit(OpCodes.Ldloc, activeFlag);
            c.Emit(OpCodes.Or);
            c.Emit(OpCodes.Stloc, activeFlag);
        }

        public static bool
            UpdateTargetsHook(
                EquipmentSlot __instance) // this is probably the most expensive function in my mod, its mostly because of the linq inside a update function which is pretty ew but im not smart enough to change it
        {
            var equipment = Equipments.FirstOrDefault(x => x.EquipmentDef.equipmentIndex == __instance.equipmentIndex);
            if (equipment == null) return false;

            try
            {
                return equipment.UpdateTargets(__instance);
            }
            catch (Exception e)
            {
                equipment.sharedInfo.Logger?.LogError(e);
            }

            return false;
        }

        public override string GetFormattedDescription(Inventory? inventory = null, string? token = null,
            bool forceHideExtended = false)
        {
            return Language.GetString(token ?? EquipmentDef.descriptionToken);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Inventory), nameof(Inventory.SetEquipmentInternal), typeof(EquipmentState), typeof(uint), typeof(uint))]
        public static void OnEquipmentSwap(Inventory __instance, EquipmentState equipmentState, uint slot, uint set)
        {
            EquipmentState? oldState = null;
            try
            {
                oldState = __instance._equipmentStateSlots[slot][set];
            }
            catch (IndexOutOfRangeException)
            {
            }

            if (oldState.Equals(equipmentState)) return;
            if (oldState?.equipmentIndex == equipmentState.equipmentIndex) return;

            var oldDef = oldState?.equipmentDef;
            var newDef = equipmentState.equipmentDef;

            var oldEquip = Equipments.FirstOrDefault(x => x.EquipmentDef == oldDef);
            var newEquip = Equipments.FirstOrDefault(x => x.EquipmentDef == newDef);

            try
            {
                oldEquip?.OnUnEquip(__instance, equipmentState);
            }
            catch (Exception e)
            {
                oldEquip.sharedInfo.Logger.LogError(e);
            }

            try
            {
                newEquip?.OnEquip(__instance, oldState);
            }
            catch (Exception e)
            {
                newEquip?.sharedInfo.Logger.LogError(e);
            }
        }

        public override void AddDisplayRules(VanillaIDRS which, ItemDisplayRule[] displayRules)
        {
            var set = IDRHelper.GetRuleSet(which);
            if (set is null) return;
            set.keyAssetRuleGroups = set.keyAssetRuleGroups.AddItem(new ItemDisplayRuleSet.KeyAssetRuleGroup
            {
                displayRuleGroup = new DisplayRuleGroup { rules = displayRules },
                keyAsset = EquipmentDef
            }).ToArray();
        }

        public override void AddDisplayRules(ModdedIDRS which, ItemDisplayRule[] displayRules)
        {
            var set = IDRHelper.GetRuleSet(which);
            if (set is null) return;
            set.keyAssetRuleGroups = set.keyAssetRuleGroups.AddItem(new ItemDisplayRuleSet.KeyAssetRuleGroup
            {
                displayRuleGroup = new DisplayRuleGroup { rules = displayRules },
                keyAsset = EquipmentDef
            }).ToArray();
        }

        /*
        public void RenderPickup()
        {
            PickupRenderer.PickupRenderer.RenderPickupIcon(new ConCommandArgs {userArgs = new List<string> {EquipmentDef.name}});
        }*/

        protected override void FillDefsFromSerializableCP(SerializableContentPack serializableContentPack)
        {
            base.FillDefsFromSerializableCP(serializableContentPack);
            var name = GetType().Name;
            foreach (var equipmentDef in serializableContentPack.equipmentDefs)
            {
                if (equipmentDef is null) continue;
                if (MatchName(equipmentDef.name, name)) EquipmentDef = equipmentDef;
            }

            if (EquipmentDef == null)
            {
                sharedInfo.Logger?.LogWarning(
                    $"Could not find EquipmentDef for item {this} in serializableContentPack, class/equipmentdef name are probably mismatched. This will throw an exception later.");
            }
        }

        protected override void FillDefsFromContentPack()
        {
            foreach (var pack in ContentPacks)
            {
                if (EquipmentDef != null) continue;
                var name = GetType().Name;
                foreach (var equipmentDef in pack.equipmentDefs)
                    if (MatchName(equipmentDef.name, name))
                        EquipmentDef = equipmentDef;
            }

            if (EquipmentDef == null)
                sharedInfo.Logger?.LogWarning(
                    $"Could not find EquipmentDef for item {this}, class/equipmentdef name are probably mismatched. This will throw an exception later.");
        }

        protected override void FillPickupIndex()
        {
            try
            {
                var pickup = PickupCatalog.FindPickupIndex(EquipmentDef.equipmentIndex);
                PickupIndex = pickup;
                PickupIndexes.Add(pickup, this);
            }
            catch (NullReferenceException e)
            {
                sharedInfo.Logger?.LogError("Equipment " + GetType().Name +
                                            " threw a NRE when filling pickup indexes, this could mean its not defined in your content pack:\n" +
                                            e);
            }
        }

        protected override void FillRequiredExpansions()
        {
            if (RequiresSotv)
                EquipmentDef.requiredExpansion = sharedInfo.SotVExpansion ? sharedInfo.SotVExpansion : SotvExpansion;
            else
                EquipmentDef.requiredExpansion = sharedInfo.Expansion;
        }

        public static EquipmentSlot SetEquipmentSlotAndSet(EquipmentSlot equipSlot, short slot,
            short set)
        {
            if (equipSlot.inventory != null)
            {
                var inv = equipSlot.inventory;

                var clampedSlot = slot % inv.activeEquipmentSet.Length;
                inv.activeEquipmentSlot = (byte)clampedSlot;
                inv.activeEquipmentSet[clampedSlot] = (byte)(set % inv._equipmentStateSlots[clampedSlot].Length);
                inv._activeEquipmentDirty = true;
                inv.wasRecentlyExtraEquipmentSwapped = true;
                inv.SetDirtyBit(16U);
                inv.HandleInventoryChanged();
            }
            return equipSlot;
        }
        public static EquipmentSlot SetEquipmentSlotAndSet(NetworkMessage netMsg)
        {
            return SetEquipmentSlotAndSet(netMsg.reader.ReadNetworkIdentity().GetComponent<EquipmentSlot>(), netMsg.reader.ReadByte(),
                netMsg.reader.ReadByte());
        }

        public static void SendEquipmentSlotAndSet(short msgType, EquipmentSlot equipSlot, short slot, short set)
        {
            _messageWriter.StartMessage(msgType);
            _messageWriter.Write(equipSlot.netIdentity);
            _messageWriter.Write(slot);
            _messageWriter.Write(set);
            _messageWriter.FinishMessage();
            ClientScene.readyConnection.SendWriter(_messageWriter, QosChannelIndex.defaultReliable.intVal);
        }
        
        [ExtraNetworkMessageHandler(server = true)]
        public static void HandleSetEquipmentSlotAndSet(NetworkMessage netMsg)
        {
            SetEquipmentSlotAndSet(netMsg);
        }

        public static void CmdSetEquipmentSlotAndSet(EquipmentSlot equipSlot, short slot, short set)
        {
            if (NetworkServer.active)
            {
                SetEquipmentSlotAndSet(equipSlot, slot, set);
                return;
            }
            SendEquipmentSlotAndSet(ExtraNetworkMessageHandlerAttribute.GetMsgType<EquipmentBase>(nameof(HandleSetEquipmentSlotAndSet)) ?? throw new InvalidOperationException(), equipSlot, slot, set);
        }
        
        [ExtraNetworkMessageHandler(server = true)]
        public static void HandleExecuteEquipmentSlotAndSet(NetworkMessage netMsg)
        {
            var slot = SetEquipmentSlotAndSet(netMsg);
            slot.Invoke(nameof(EquipmentSlot.ExecuteIfReady), 0.1f);
        }
        
        public static void CmdExecuteEquipmentSlotAndSet(EquipmentSlot equipSlot, short slot, short set)
        {
            if (NetworkServer.active)
            {
                SetEquipmentSlotAndSet(equipSlot, slot, set);
                equipSlot.ExecuteIfReady();
                return;
            }
            SendEquipmentSlotAndSet(ExtraNetworkMessageHandlerAttribute.GetMsgType<EquipmentBase>(nameof(HandleExecuteEquipmentSlotAndSet)) ?? throw new InvalidOperationException(), equipSlot, slot, set);            
        }
        
        private static NetworkWriter _messageWriter = new NetworkWriter();
    }
}