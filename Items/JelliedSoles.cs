using System;
using BepInEx.Configuration;
using BubbetsItems.Helpers;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RiskOfOptions.Options;
using RoR2;
using RoR2.Audio;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace BubbetsItems.Items
{
    public class JelliedSolesBehavior : MonoBehaviour
    {
        public float _storedDamage;

        public float storedDamage
        {
            get => _storedDamage;
            set
            {
                var old = _storedDamage;
                _storedDamage = Mathf.Max(0, value);
                if (!NetworkServer.active) return;
                if (Mathf.Approximately(old, _storedDamage)) return;
                NetworkServer.SendToAll(
                    ExtraNetworkMessageHandlerAttribute.GetMsgType<JelliedSolesBehavior>(nameof(Handle)) ??
                    throw new InvalidOperationException(), new JelliedMessage
                    {
                        identity = Master.networkIdentity,
                        damage = _storedDamage
                    });
            }
        }

        public void Awake()
        {
            Master = GetComponent<CharacterMaster>();
        }

        public CharacterMaster Master;


        [ExtraNetworkMessageHandler(client = true)]
        public static void Handle(NetworkMessage netMsg)
        {
            if (NetworkServer.active) return; // (client only)
            var message = netMsg.ReadMessage<JelliedMessage>();
            message.identity.GetComponent<JelliedSolesBehavior>().storedDamage = message.damage;
        }

        public class JelliedMessage : MessageBase
        {
            public NetworkIdentity identity;
            public float damage;

            public override void Deserialize(NetworkReader reader)
            {
                base.Deserialize(reader);
                identity = reader.ReadNetworkIdentity();
                damage = reader.ReadSingle();
            }

            public override void Serialize(NetworkWriter writer)
            {
                base.Serialize(writer);
                writer.Write(identity);
                writer.Write(damage);
            }
        }
    }

    public class JelliedSoles : ItemBase
    {
        private static ConfigEntry<bool> _transferWhenStolen = null!;
        private static ConfigEntry<bool> _clampFallDamageToHealth = null!;

        protected override void MakeTokens()
        {
            base.MakeTokens();
            AddToken("JELLIEDSOLES_NAME", "Jellied Soles");
            AddToken("JELLIEDSOLES_DESC",
                "Reduces " + "fall damage ".Style(StyleEnum.Utility) + "by " + "{0:0%}".Style(StyleEnum.Utility) +
                ". Converts {1:0%} of that reduction into your next attack.");
            AddToken("JELLIEDSOLES_DESC_SIMPLE",
                "Reduces " + "fall damage ".Style(StyleEnum.Utility) + "by " + "15% ".Style(StyleEnum.Utility) +
                "(+15% per stack) ".Style(StyleEnum.Stack) + "and converts " + "100% ".Style(StyleEnum.Utility) +
                "(+100% per stack) ".Style(StyleEnum.Stack) + "removed damage to your next attack. Scales by level.");
            SimpleDescriptionToken = "JELLIEDSOLES_DESC_SIMPLE";
            AddToken("JELLIEDSOLES_PICKUP",
                "Reduces " + "fall damage.".Style(StyleEnum.Utility) +
                " Converts that reduction into your next attack.");
            AddToken("JELLIEDSOLES_LORE", "");
        }

        protected override void MakeConfigs()
        {
            base.MakeConfigs();
            _transferWhenStolen = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General,
                "Transfer Jellied Soles Damage When Stealing", true, "The damage you've got stored, transferred to and from stealers like mithrix or the dlc3 extractor.");
            _clampFallDamageToHealth = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General,
                "Jellied Soles Clamp Fall Damage To Health", false,
                "Should fall damage be clamped to the character's health.");
            AddScalingFunction("Min([a] * 0.15, 1)", "Reduction",
                desc: "[a] = amount; [f] = incoming fall damage that was reduced;");
            AddScalingFunction("[s] + [a] * [d] * [f]", "Damage Add",
                desc:
                "[a] = amount; [s] = stored damage; [d] = level scaled damage over base damage; [f] = incoming fall damage that was reduced;",
                oldDefault: "[s] * [d] * [a]");
            AddScalingFunction("Min([h] - [i], [s])", "Damage Spent",
                desc:
                "[a] = amount; [s] = stored damage; [d] = level scaled damage over base damage; [i] = initial hit damage; [h] = health of the character;");
        }

        public override void MakeRiskOfOptions()
        {
            base.MakeRiskOfOptions();
            RiskOfOptions.ModSettingsManager.AddOption(new CheckBoxOption(_transferWhenStolen));
            RiskOfOptions.ModSettingsManager.AddOption(new CheckBoxOption(_clampFallDamageToHealth));
        }

        public override string GetFormattedDescription(Inventory? inventory, string? token = null,
            bool forceHideExtended = false)
        {
            if (inventory == null || !inventory)
                return base.GetFormattedDescription(inventory, token, forceHideExtended);
            var body = inventory.GetComponent<CharacterMaster>().GetBody();
            if (!body) return base.GetFormattedDescription(inventory, token, forceHideExtended);
            var info = ScalingInfos[1].WorkingContext;
            info.s = 0;
            info.f = 1;
            info.d = body.damage / body.baseDamage;
            var desc = base.GetFormattedDescription(inventory, token, forceHideExtended);
            desc += "\n" + "Stored Damage: " + inventory.GetComponent<JelliedSolesBehavior>().storedDamage;
            return desc;
        }

        protected override void MakeBehaviours()
        {
            base.MakeBehaviours();
            Inventory.onInventoryChangedGlobal += OnInvChanged;
            ModdedDamageColors.ReserveColor(new Color(1, 0.4f, 0), out index);
        }

        protected override void DestroyBehaviours()
        {
            base.DestroyBehaviours();
            Inventory.onInventoryChangedGlobal -= OnInvChanged;
        }

        private void OnInvChanged(Inventory obj)
        {
            var comp = obj.GetComponent<JelliedSolesBehavior>();
            if (!comp && obj.GetItemCount(ItemDef) > 0)
            {
                obj.gameObject.AddComponent<JelliedSolesBehavior>();
            }
        }

        [HarmonyILManipulator,
         HarmonyPatch(typeof(GlobalEventManager), nameof(GlobalEventManager.OnCharacterHitGroundServer))]
        public static void NullifyFallDamage(ILContext il)
        {
            var c = new ILCursor(il);
            //var h = -1;
            //var d = -1;

            c.GotoNext(MoveType.After,
                x => x.MatchLdcI4(0),
                x => x.MatchStloc(out _)
            );

            c.Emit(OpCodes.Ldarg_1);
            c.Emit(OpCodes.Ldarg_2);
            c.Emit(OpCodes.Ldloc_0); // weak ass knees
            c.EmitDelegate<Action<CharacterBody, Vector3, bool>>(CollectDamage);

            c.GotoNext(x => x.MatchCallvirt<CharacterBody>("get_footPosition"));
            c.GotoNext(MoveType.Before, x => x.MatchLdloc(out _), x => x.MatchLdloc(out _),
                x => x.MatchCallvirt<HealthComponent>(nameof(HealthComponent.TakeDamage)));
            c.Index++;
            c.Emit(OpCodes.Dup);
            c.Index++;
            //c.Emit(OpCodes.Ldloc, h);
            //c.Emit(OpCodes.Ldloc, d); // Maybe move this up and before the check for ignores fall damage
            c.EmitDelegate<Func<HealthComponent, DamageInfo, DamageInfo>>(UpdateDamage);
        }


        // body.damage/body.basedamage * storedDamage
        [HarmonyILManipulator, HarmonyPatch(typeof(HealthComponent), nameof(HealthComponent.TakeDamageProcess))]
        public static void IlTakeDamage(ILContext il)
        {
            var c = new ILCursor(il);
            if (!c.MatchNearbyDamage(out var masterNum, out var num2))
            {
                BubbetsItemsPlugin.Log.LogError("Failed to match nearby damage.");
                return;
            }

            ILLabel target = null!;
            if (!c.TryGotoNext(x =>
                    x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.adaptiveArmor))) ||
                !c.TryGotoNext(x => x.MatchBle(out target)))
            {
                BubbetsItemsPlugin.Log.LogError("Failed to match after adaptiveArmor.");
                return;
            }

            c.GotoNext(MoveType.After, x => x == target.Target);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldloc, masterNum);
            c.Emit(OpCodes.Ldarg_1);
            //c.Emit(OpCodes.Ldloc, num2);
            c.EmitDelegate<Func<float, HealthComponent, CharacterMaster, DamageInfo, float>>((amount, hc, master,
                damageInfo) =>
            {
                if (!master) return amount;
                var inv = master.inventory;
                if (!inv) return amount;
                if (!TryGetInstance(out JelliedSoles instance)) return amount;
                var count = inv.GetItemCountEffective(instance.ItemDef);
                if (count <= 0) return amount;
                var behavior = inv.GetComponent<JelliedSolesBehavior>(); // potential future incompat with holydll
                if (behavior.storedDamage <= 0) return amount;
                var body = master.GetBody();

                var context = instance.ScalingInfos[2].WorkingContext;
                context.s = behavior.storedDamage;
                context.d = body.damage / body.baseDamage;
                context.i = amount;
                context.h = hc.combinedHealth;
                var a = instance.ScalingInfos[2].ScalingFunction(count);
                behavior.storedDamage -= a;
                amount += a;
                damageInfo.damageColorIndex = index;
                var updatedDamage = damageInfo.damageType;
                updatedDamage.damageType |= DamageType.BypassOneShotProtection;
                damageInfo.damageType = updatedDamage;
                EntitySoundManager.EmitSoundServer(hitSound.index, body.gameObject);

                return amount;
            });
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Stloc, num2);
        }

        private static NetworkSoundEventDef? _hitSound;

        public static NetworkSoundEventDef hitSound => (_hitSound ??=
            BubbetsItemsPlugin.ContentPack.networkSoundEventDefs.Find("JelliedSolesHitSound"))!;

        private static NetworkSoundEventDef? _hitGroundSound;
        public static DamageColorIndex index;

        public static NetworkSoundEventDef hitGroundSound => (_hitGroundSound ??=
            BubbetsItemsPlugin.ContentPack.networkSoundEventDefs.Find("JelliedSolesHitGround"))!;

        public static void CollectDamage(CharacterBody body, Vector3 impactVelocity, bool weakAssKnees)
        {
            var damage = Mathf.Max(Mathf.Abs(impactVelocity.y) - (body.jumpPower + 20f), 0f);
            if (damage <= 0f) return;
            var inv = body.inventory;
            if (!inv) return;
            if (!TryGetInstance(out JelliedSoles instance)) return;
            var count = inv.GetItemCountEffective(instance.ItemDef);
            if (count <= 0) return;
            var behavior = inv.GetComponent<JelliedSolesBehavior>();

            damage /= 60f;
            damage *= body.maxHealth;
            if (weakAssKnees || body.teamComponent.teamIndex == TeamIndex.Player &&
                Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse3)
                damage *= 2f;

            if (_clampFallDamageToHealth.Value)
            {
                damage = Mathf.Min(damage, body.healthComponent.fullCombinedHealth);
            }

            instance.ScalingInfos[0].WorkingContext.f = damage;
            var frac = instance.ScalingInfos[0].ScalingFunction(count);
            var context = instance.ScalingInfos[1].WorkingContext;
            context.s = behavior.storedDamage;
            context.d = body.damage / body.baseDamage;
            context.f = damage * frac;
            behavior.storedDamage = instance.ScalingInfos[1].ScalingFunction(count);
            EntitySoundManager.EmitSoundServer(hitGroundSound.index, body.gameObject);
        }

        public static DamageInfo UpdateDamage(HealthComponent component, DamageInfo info)
        {
            var inv = component.body.inventory;
            if (!inv) return info;
            if (!TryGetInstance(out JelliedSoles instance)) return info;
            var count = inv.GetItemCountEffective(instance.ItemDef);
            if (count <= 0) return info;
            var damage = info.damage;
            if (_clampFallDamageToHealth.Value)
            {
                damage = Mathf.Min(damage, component.fullCombinedHealth);
            }

            instance.ScalingInfos[0].WorkingContext.f = damage;
            var frac = instance.ScalingInfos[0].ScalingFunction(count);
            info.damage *= 1f - frac;
            return info;
        }

        [HarmonyPrefix,
         HarmonyPatch(typeof(ItemStealController.StolenInventoryInfo),
             nameof(ItemStealController.StolenInventoryInfo.StealItem))]
        public static void StolenItem(ItemStealController.StolenInventoryInfo __instance, ItemIndex itemIndex, int maxStackToSteal,
            bool? useOrbOverride)
        {
            if (!_transferWhenStolen.Value) return;
            if (!TryGetInstance(out JelliedSoles instance)) return;
            if (itemIndex != instance.ItemDef.itemIndex) return;
            var target = __instance.owner.GetComponent<NetworkedBodyAttachment>().attachedBody;
            var comp = target.inventory.GetComponent<JelliedSolesBehavior>();
            if (!comp)
            {
                comp = target.inventory.gameObject.AddComponent<JelliedSolesBehavior>();
            }
            comp.storedDamage = __instance.victimInventory.GetComponent<JelliedSolesBehavior>().storedDamage;
        }

        [HarmonyPrefix,
         HarmonyPatch(typeof(ItemStealController.StolenInventoryInfo),
             nameof(ItemStealController.StolenInventoryInfo.ReclaimStolenItem))]
        public static void ReturnedItem(ItemStealController.StolenInventoryInfo __instance, ItemIndex itemToReclaim, int maxStackToReclaim,
            bool? useOrbOverride)
        {
            if (!_transferWhenStolen.Value) return;
            if (!TryGetInstance(out JelliedSoles instance)) return;
            if (itemToReclaim != instance.ItemDef.itemIndex) return;
            var target = __instance.owner.GetComponent<NetworkedBodyAttachment>().attachedBody;
            var comp = __instance.victimInventory.GetComponent<JelliedSolesBehavior>();
            if (!comp)
            {
                comp = __instance.victimInventory.gameObject.AddComponent<JelliedSolesBehavior>();
            }
            comp.storedDamage = target.inventory.GetComponent<JelliedSolesBehavior>().storedDamage;
        }
    }
}