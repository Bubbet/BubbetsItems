using BubbetsItems.Helpers;
using RoR2;

namespace BubbetsItems.Items.BarrierItems
{
	public class BrokenCeremonialProbe : ItemBase
	{
		protected override void MakeTokens()
		{
			base.MakeTokens();
			AddToken("CEREMONIALPROBEBROKEN_NAME", "Ceremonial Probe (Consumed)");
			AddToken("CEREMONIALPROBEBROKEN_DESC", "Slows " + "barrier decay ".Style(StyleEnum.Heal) + "by " + "{0:0%}".Style(StyleEnum.Heal) + ".");
			SimpleDescriptionToken = "CEREMONIALPROBEBROKEN_DESC_SIMPLE";
			AddToken("CEREMONIALPROBEBROKEN_DESC_SIMPLE", "Slows " + "barrier decay ".Style(StyleEnum.Heal) + "by " + "15% ".Style(StyleEnum.Heal) + "(stacks logarithmically)".Style(StyleEnum.Stack) + ".");
			AddToken("CEREMONIALPROBEBROKEN_PICKUP", "Slow down barrier decay.");
			AddToken("CEREMONIALPROBEBROKEN_LORE", "");
		}

		protected override void MakeConfigs()
		{
			base.MakeConfigs();
			AddScalingFunction("Log((2.12961 * [a]), 2.718) * 0.201992", "Barrier Decay Slow Percent");
		}

		
		protected override void MakeBehaviours()
		{
			base.MakeBehaviours();
			RecalculateStatsAPI.GetStatCoefficients += GetBarrierDecay;
		}

		protected override void DestroyBehaviours()
		{
			base.DestroyBehaviours();
			RecalculateStatsAPI.GetStatCoefficients -= GetBarrierDecay;
		}

		private void GetBarrierDecay(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
		{
			var inv = sender.inventory;
			if (!inv) return;
			var count = inv.GetItemCountEffective(ItemDef);
			if (count <= 0) return;
			args.barrierDecayMult -= ScalingInfos[0].ScalingFunction(count);
		}
	}
}