using System;
using System.Linq;
using BubbetsItems.Helpers;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Audio;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbetsItems
{
    [HarmonyPatch]
    public static class HarmonyPatches
    {

        [HarmonyILManipulator, HarmonyPatch(typeof(RuleCatalog), nameof(RuleCatalog.Init))]
        public static void GenerateRulesForAllItemTiers(ILContext il)
        {
            var c = new ILCursor(il);
            c.GotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt(typeof(Enum), nameof(Enum.GetValues)));
            c.EmitDelegate<Func<Array, Array>>(_ => ItemTierCatalog.allItemTierDefs.Select(x => x.tier).ToArray());
            var i = -1;
            c.GotoNext(MoveType.After, x => x.MatchLdloc(out i), x => x.MatchCallOrCallvirt(typeof(Array), "get_Length"));
            c.Emit(OpCodes.Ldloc, i);
            c.EmitDelegate<Func<int, ItemTier[], int>>((_, tiers) => (int)tiers.Max() + 1);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(LocalUserManager), nameof(LocalUserManager.Init))]
        public static void InitSystemInitializers()
        {
            // SystemInitializer busting my balls again so it can just never be used again.
            BubbetsItemsPlugin.ExtraTokens();

            BubPickupDisplayCustom.ModifyGenericPickup();
            SharedBase.MakeAllTokens();
            SharedBase.FillIDRS();
            SharedBase.FillAllExpansionDefs();
            SharedBase.InitializePickups();
            EquipmentBase.ApplyPostEquipments();
        }

        [HarmonyPrefix, HarmonyPatch(typeof(NetworkSoundEventCatalog), nameof(NetworkSoundEventCatalog.Init))]
        public static void LoadSoundbank()
        {
            BubbetsItemsPlugin.LoadSoundBank();
        }

        [HarmonyILManipulator, HarmonyPatch(typeof(GlobalEventManager), nameof(GlobalEventManager.OnCharacterDeath))]
        public static void AmmoPickupPatch(ILContext il)
        {
            if (!BubbetsItemsPlugin.Conf.AmmoPickupAsOrbEnabled.Value) return;
            var c = new ILCursor(il);
            c.GotoNext(
                x => x.MatchLdstr("Prefabs/NetworkedObjects/AmmoPack"),
                x => x.OpCode == OpCodes.Call // && (x.Operand as MethodInfo)?.Name == "Load",
                //x => x.MatchLdloc(out _)
            );
            var start = c.Index;
            c.GotoNext(MoveType.After,
                x => x.MatchCall<NetworkServer>("Spawn")
            );
            var end = c.Index;
            c.Index = start;
            c.RemoveRange(end - start);
            c.Emit(OpCodes.Ldarg_1);
            c.EmitDelegate<AmmoPickupDele>(DoAmmoPickupAsOrb);
        }

        public static void DoAmmoPickupAsOrb(DamageReport report)
        {
            OrbManager.instance.AddOrb(new AmmoPickupOrb
            {
                origin = report.victim.transform.position,
                target = report.attackerBody.mainHurtBox,
            });
        }

        private delegate void AmmoPickupDele(DamageReport report);
    }
}