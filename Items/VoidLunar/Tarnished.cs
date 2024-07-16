using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BubbetsItems.Helpers;
using BubbetsItems.ItemBehaviors;
using HarmonyLib;
using R2API;
using RiskOfOptions;
using RiskOfOptions.Options;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbetsItems.Items.VoidLunar
{
    public class Tarnished : ItemBase
    {
        public static SkillDef? SkillDef =>
            _skillDef ??= BubbetsItemsPlugin.ContentPack.skillDefs.Find("SkillDefTarnished");

        protected override void MakeTokens()
        {
            base.MakeTokens();
            var name = GetType().Name.ToUpper();
            SimpleDescriptionToken = name + "_DESC_SIMPLE";
            AddToken(name + "_NAME", "Tarnished");
            var convert = "Converts all Purity".Style(StyleEnum.Void) + ".";
            AddToken(name + "_CONVERT", convert);

            AddToken(name + "_DESC",
                "Gain " + "{3} Luck".Style(StyleEnum.Utility) + " for " +
                "{0} favorable rolls.".Style(StyleEnum.Utility) +
                " Once out of favorable rolls, lock a random skill for {2} seconds. ".Style(StyleEnum.Health) +
                "Then get more rolls. ");
            AddToken(name + "_DESC_SIMPLE",
                "Gain " + "+1 Luck ".Style(StyleEnum.Utility) + "(+1 per stack) ".Style(StyleEnum.Stack) + " for " +
                "10 favorable rolls. ".Style(StyleEnum.Utility) + "(+10 per stack) ".Style(StyleEnum.Stack) +
                "Once out of favorable rolls, lock a random skill for 5 seconds. ".Style(StyleEnum.Health) +
                "(+2 per stack) ".Style(StyleEnum.Stack) + "Then get more rolls. ");

            AddToken(name + "_DESC_OLD",
                "Gain " + "{3} Luck".Style(StyleEnum.Utility) + " for " +
                "{0} favorable rolls per stage.".Style(StyleEnum.Utility) +
                " Once out of favorable rolls, gain {1} luck. ".Style(StyleEnum.Health));
            AddToken(name + "_DESC_SIMPLE_OLD",
                "While active, all random effects are rolled " +
                "+1 time for a favorable outcome".Style(StyleEnum.Utility) + ". " +
                "(+1 per stack) ".Style(StyleEnum.Stack) + "Only stays active for 10 ".Style(StyleEnum.Health) +
                "(+10 per stack) ".Style(StyleEnum.Stack) + "rolls per stage".Style(StyleEnum.Health) + ". " +
                "When inactive, all random effects are rolled " + "+1 ".Style(StyleEnum.Health) +
                "(+1 per stack) ".Style(StyleEnum.Stack) + "times for an unfavorable outcome".Style(StyleEnum.Health) +
                ". ");
            AddToken(name + "_PICKUP",
                "Gain temporary luck, " + "then become unlucky.".Style(StyleEnum.Health) + convert);
            AddToken(name + "_LORE", "");
        }

        public override string GetFormattedDescription(Inventory? inventory, string? token = null,
            bool forceHideExtended = false)
        {
            var name = GetType().Name.ToUpper();
            SimpleDescriptionToken = name + "_DESC_SIMPLE" + (OldTarnished.Value ? "_OLD" : "");
            return base.GetFormattedDescription(inventory,
                !OldTarnished.Value ? token : ItemDef.descriptionToken + "_OLD", forceHideExtended);
        }

        protected override void MakeConfigs()
        {
            base.MakeConfigs();
            AddScalingFunction("[a] * 10", "Rolls Per Stage", oldDefault: "[a] * 50");
            AddScalingFunction("[a] * -1", "Unfavorable Rolls");
            AddScalingFunction("[a] * 2 + 3", "Skill Lock Duration");
            AddScalingFunction("[a]", "Luck Add");
            OldTarnished = sharedInfo.ConfigFile.Bind(ConfigCategoriesEnum.General, "Tarnished Old", false,
                "Make tarnished function how it used to.");
        }

        public override void MakeRiskOfOptions()
        {
            base.MakeRiskOfOptions();
            ModSettingsManager.AddOption(new CheckBoxOption(OldTarnished));
        }

        protected override void FillVoidConversions(List<ItemDef.Pair> pairs)
        {
            base.FillVoidConversions(pairs);
            AddVoidPairing(nameof(RoR2Content.Items.LunarBadLuck));
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

        private void RecalcStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            var skills = sender.skillLocator.allSkills;
            var contains = skills.Where(x => x.skillOverrides.Any(y => y.skillDef == SkillDef)).ToArray();
            if (sender.HasBuff(BuffDef2) && !contains.Any())
                skills[Random.RandomRangeInt(0, skills.Length)]
                    .SetSkillOverride(this, SkillDef, GenericSkill.SkillOverridePriority.Contextual);
            else if (!sender.HasBuff(BuffDef2) && contains.Any())
            {
                foreach (var genericSkill in contains)
                    genericSkill.UnsetSkillOverride(this, SkillDef, GenericSkill.SkillOverridePriority.Contextual);
                var instance = GetInstance<Tarnished>();
                var stack = sender.inventory.GetItemCount(instance!.ItemDef);
                var toAdd = Mathf.FloorToInt(ScalingInfos[0].ScalingFunction(stack));
                if (NetworkServer.active)
                    sender.SetBuffCount(Tarnished.BuffDef!.buffIndex, toAdd);
                sender.master.OnInventoryChanged();
            }
        }

        private static BuffDef? _buffDef2;
        private static BuffDef? _buffDef;
        private static SkillDef? _skillDef;
        public static ConfigEntry<bool> OldTarnished = null!;
        public static BuffDef? BuffDef => _buffDef ??= BubbetsItemsPlugin.ContentPack.buffDefs.Find("BuffDefTarnished");

        public static BuffDef? BuffDef2 =>
            _buffDef2 ??= BubbetsItemsPlugin.ContentPack.buffDefs.Find("BuffDefTarnishedLock");

        protected override void FillDefsFromSerializableCP(SerializableContentPack serializableContentPack)
        {
            base.FillDefsFromSerializableCP(serializableContentPack);
            // yeahh code based content because TK keeps fucking freezing
            var buff = ScriptableObject.CreateInstance<BuffDef>();
            buff.canStack = true;
            buff.name = "BuffDefTarnished";
            buff.buffColor = new Color(r: 0.5254902f, g: 0, b: 0.79607844f, a: 1);
            buff.iconSprite = BubbetsItemsPlugin.AssetBundle.LoadAsset<Sprite>("texBuffTemporaryLuckIcon");

            var buff2 = ScriptableObject.CreateInstance<BuffDef>();
            buff2.isDebuff = true;
            buff2.name = "BuffDefTarnishedLock";
            buff2.buffColor = new Color(r: 0.5254902f, g: 0, b: 0.79607844f, a: .5f);
            buff2.iconSprite = BubbetsItemsPlugin.AssetBundle.LoadAsset<Sprite>("texBuffTemporaryLuckIcon");
            serializableContentPack.buffDefs = serializableContentPack.buffDefs.AddItem(buff).AddItem(buff2).ToArray();
        }

        [HarmonyPrefix,
         HarmonyPatch(typeof(Util), nameof(Util.CheckRoll), typeof(float), typeof(float), typeof(CharacterMaster))]
        public static void UpdateRollsPre(float percentChance, float luck = 0f,
            CharacterMaster? effectOriginMaster = null)
        {
            if (!NetworkServer.active) return;
            if (!effectOriginMaster) return;
            var body = effectOriginMaster != null ? effectOriginMaster.GetBody() : null;
            if (body == null || !body) return;
            if (!body.wasLucky) return;
            body.wasLucky = false;
        }

        [HarmonyPostfix,
         HarmonyPatch(typeof(Util), nameof(Util.CheckRoll), typeof(float), typeof(float), typeof(CharacterMaster))]
        // ReSharper disable once InconsistentNaming
        public static void UpdateRolls(bool __result, float percentChance, float luck = 0f,
            CharacterMaster? effectOriginMaster = null)
        {
            if (!NetworkServer.active) return;
            if (!__result) return;
            if (!effectOriginMaster) return;
            var body = effectOriginMaster != null ? effectOriginMaster.GetBody() : null;
            if (body == null || !body) return;
            if (!body.wasLucky) return;
            var inv = effectOriginMaster!.inventory;
            if (!inv) return;
            var inst = GetInstance<Tarnished>();
            var amount = inv.GetItemCount(inst!.ItemDef);
            if (amount <= 0) return;
            var count = body.GetBuffCount(BuffDef!.buffIndex);
            body.SetBuffCount(BuffDef.buffIndex, count - 1);
            if (count - 1 == 0)
            {
                if (!body.HasBuff(Tarnished.BuffDef2) && !Tarnished.OldTarnished.Value)
                {
                    body.AddTimedBuff(Tarnished.BuffDef2, inst.ScalingInfos[2].ScalingFunction(amount));
                }

                effectOriginMaster.OnInventoryChanged();
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CharacterMaster), nameof(CharacterMaster.OnInventoryChanged))]
        public static void ApplyLuck(CharacterMaster __instance)
        {
            var instance = GetInstance<Tarnished>();
            if (instance == null) return;
            var stack = __instance.inventory.GetItemCount(instance.ItemDef);
            if (stack > 0)
            {
                var luckDifference = 0;
                var body = __instance.GetBody();
                if (body.GetBuffCount(BuffDef) <= 0)
                {
                    if (Tarnished.OldTarnished.Value)
                        luckDifference = Mathf.FloorToInt(instance.ScalingInfos[1].ScalingFunction(stack));
                    //if(NetworkServer.active && !body.HasBuff(Tarnished.BuffDef))
                    //body.AddBuff(Tarnished.BuffDef);
                }
                else
                {
                    luckDifference = Mathf.RoundToInt(instance.ScalingInfos[3].ScalingFunction(stack));
                    body.statsDirty = true;
                    //if(NetworkServer.active && body.HasBuff(Tarnished.BuffDef))
                    //body.RemoveBuff(Tarnished.BuffDef);
                }

                __instance.luck += luckDifference;
            }
        }
    }
}