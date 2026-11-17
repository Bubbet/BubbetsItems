using BubbetsItems.Items;
using HarmonyLib;
using RoR2;
using RoR2.ContentManagement;
using UnityEngine;

namespace BubbetsItems.Artifacts
{
	public class VoidScrapAtStart : ArtifactBase
	{
		private VoidJunkToScrapTier1? Scrap;
		public override bool RequiresSotv => true;

		protected override void MakeTokens()
		{
			base.MakeTokens();
			AddToken("VOIDSCRAP_AT_START_NAME", "Artifact of the Broken");
			AddToken("VOIDSCRAP_AT_START_DESC", "Start the Run with Void Scrap");
		}

		protected override void MakeBehaviours()
		{
			base.MakeBehaviours();
			Scrap = GetInstance<VoidJunkToScrapTier1>();
			if (Scrap is not null)
			{
				CharacterMaster.onStartGlobal += Start;
				RunArtifactManager.onArtifactEnabledGlobal += OnArtifactEnabled;
			}
		}

		protected override void DestroyBehaviours()
		{
			base.DestroyBehaviours();
			CharacterMaster.onStartGlobal -= Start;
			RunArtifactManager.onArtifactEnabledGlobal -= OnArtifactEnabled;
		}


		private void OnArtifactEnabled(RunArtifactManager runArtifactManager, ArtifactDef artifactDef)
		{
			if (Run.instance && GetInstance<VoidScrapAtStart>()?.ArtifactDef == artifactDef)
			{
				foreach (var playerCharacterMasterController in PlayerCharacterMasterController.instances)
				{
					playerCharacterMasterController.master.inventory.GiveItemPermanent(Scrap!.ItemDef);
				}
			}
		}

		private void Start(CharacterMaster characterMaster)
		{
			if (RunArtifactManager.instance.IsArtifactEnabled(GetInstance<VoidScrapAtStart>()!.ArtifactDef))
			{
				characterMaster.inventory.GiveItemPermanent(Scrap!.ItemDef);
			}
		}

		protected override void FillDefsFromSerializableCP(SerializableContentPack serializableContentPack)
		{
			ArtifactDef = ScriptableObject.CreateInstance<ArtifactDef>();
			ArtifactDef.cachedName = "VoidScrapAtStart";
			ArtifactDef.nameToken = "BUB_VOIDSCRAP_AT_START_NAME";
			ArtifactDef.descriptionToken = "VOIDSCRAP_AT_START_DESC";
			//ArtifactDef.smallIconSelectedReference = ;
			//ArtifactDef.smallIconDeselectedReference = ;
			serializableContentPack.artifactDefs = serializableContentPack.artifactDefs.AddToArray(ArtifactDef);
			base.FillDefsFromSerializableCP(serializableContentPack);
		}
	}
}