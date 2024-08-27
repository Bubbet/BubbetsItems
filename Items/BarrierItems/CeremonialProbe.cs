using BepInEx.Configuration;
using BubbetsItems.Helpers;
using HarmonyLib;
using R2API;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;

namespace BubbetsItems.Items.BarrierItems
{
    public class CeremonialProbe : ItemBase
    {
        public static ConfigEntry<bool> RegenOnStage = null!;

        protected override void MakeTokens()
        {
            base.MakeTokens();
            AddToken("CEREMONIALPROBE_NAME", "Ceremonial Probe");
            AddToken("CEREMONIALPROBE_DESC",
                "Falling bellow " + "{0:0%} health ".Style(StyleEnum.Health) + " consumes this item and gives you " +
                "{1:0%} temporary barrier. ".Style(StyleEnum.Heal));
            AddToken("CEREMONIALPROBE_DESC_SIMPLE",
                "Falling bellow " + "35% health ".Style(StyleEnum.Health) + " consumes this item and gives you " +
                "75% temporary barrier. ".Style(StyleEnum.Heal));
            SimpleDescriptionToken = "CEREMONIALPROBE_DESC_SIMPLE";
            AddToken("CEREMONIALPROBE_PICKUP", "Get barrier at low health.");
            AddToken("CEREMONIALPROBE_LORE", "");
        }

        protected override void MakeConfigs()
        {
            base.MakeConfigs();
            AddScalingFunction("0.35", "Health Threshold");
            AddScalingFunction("0.75", "Barrier Add Percent");
            RegenOnStage = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General, "Ceremonial Probe Regen On Stage",
                true, "Should ceremonial probe regenerate on stage change.");
        }

        public override void MakeRiskOfOptions()
        {
            base.MakeRiskOfOptions();
            ModSettingsManager.AddOption(new CheckBoxOption(RegenOnStage));
        }

        protected override void MakeBehaviours()
        {
            base.MakeBehaviours();
            GlobalEventManager.onServerDamageDealt += OnHit;
        }

        protected override void DestroyBehaviours()
        {
            base.DestroyBehaviours();
            GlobalEventManager.onServerDamageDealt -= OnHit;
        }

        private void OnHit(DamageReport obj)
        {
            if (!obj.victim) return;
            var body = obj.victim.body;
            if (!body) return;
            DoEffect(body);
        }

        public static void DoEffect(CharacterBody body)
        {
            if (!TryGetInstance<CeremonialProbe>(out var inst)) return;
            var inv = body.inventory;
            if (!inv) return;
            var amount = inv.GetItemCount(inst.ItemDef);
            if (amount <= 0) return;
            if (body.healthComponent.combinedHealth / body.healthComponent.fullCombinedHealth <
                inst.ScalingInfos[0].ScalingFunction(amount))
            {
                body.healthComponent.AddBarrier(body.healthComponent.fullCombinedHealth *
                                                inst.ScalingInfos[1].ScalingFunction(amount));

                if (TryGetInstance<BrokenCeremonialProbe>(out var broke))
                {
                    body.inventory.GiveItem(broke.ItemDef);
                    CharacterMasterNotificationQueue.SendTransformNotification(body.master, inst.ItemDef.itemIndex,
                        broke.ItemDef.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                }

                body.inventory.RemoveItem(inst.ItemDef);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.OnServerStageBegin))]
        public static void RegenItem(CharacterMaster __instance)
        {
            if (!RegenOnStage.Value) return;
            if (TryGetInstance<BrokenCeremonialProbe>(out var broke) && TryGetInstance<CeremonialProbe>(out var inst))
            {
                var itemCount = __instance.inventory.GetItemCount(broke.ItemDef);
                if (itemCount <= 0) return;
                __instance.inventory.RemoveItem(broke.ItemDef, itemCount);
                __instance.inventory.GiveItem(inst.ItemDef, itemCount);
                CharacterMasterNotificationQueue.SendTransformNotification(__instance, broke.ItemDef.itemIndex,
                    inst.ItemDef.itemIndex,
                    CharacterMasterNotificationQueue.TransformationType.RegeneratingScrapRegen);
            }
        }
    }
}