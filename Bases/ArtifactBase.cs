using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using RoR2.ContentManagement;

namespace BubbetsItems
{
    public abstract class ArtifactBase : SharedBase
    {
        public ArtifactDef ArtifactDef = null!;
        private static IEnumerable<ArtifactBase>? _artifacts;
        public static IEnumerable<ArtifactBase> Artifacts => _artifacts ??= Instances.OfType<ArtifactBase>();

        protected override void MakeConfigs()
        {
            var name = GetType().Name;
            Enabled = sharedInfo.ConfigFile.Bind("Disable Artifacts", name, false, "Should this artifact be enabled."); // TODO enable the artifacts by default
        }
        
        protected override void FillDefsFromSerializableCP(SerializableContentPack serializableContentPack)
        {
            base.FillDefsFromSerializableCP(serializableContentPack);
            var name = GetType().Name;
            foreach (var artifactDef in serializableContentPack.artifactDefs)
            {
                if (artifactDef is null) continue;
                if (MatchName(artifactDef.cachedName, name)) ArtifactDef = artifactDef;
            }

            if (ArtifactDef == null)
            {
                sharedInfo.Logger?.LogWarning(
                    $"Could not find ArtifactDef for item {this} in serializableContentPack, class/artfactdef name are probably mismatched. This will throw an exception later.");
            }
        }

        protected override void FillDefsFromContentPack()
        {
            var name = GetType().Name;
            foreach (var pack in ContentPacks)
            {
                if (pack is null) continue;
                foreach (var artifactDef in pack.artifactDefs)
                    if (MatchName(artifactDef.cachedName, name))
                        ArtifactDef = artifactDef;
            }

            if (ArtifactDef == null)
            {
                sharedInfo.Logger?.LogWarning(
                    $"Could not find ArtifactDef for item {this} in serializableContentPack, class/artfactdef name are probably mismatched. This will throw an exception later.");
            }
        }

        protected override void FillPickupIndex()
        {
            try
            {
                var pickup = PickupCatalog.FindPickupIndex(ArtifactDef.artifactIndex);
                PickupIndex = pickup;
                PickupIndexes.Add(pickup, this);
            }
            catch (NullReferenceException e)
            {
                sharedInfo.Logger?.LogError("Artifact " + GetType().Name +
                                            " threw a NRE when filling pickup indexes, this could mean its not defined in your content pack:\n" +
                                            e);
            }
        }

        protected override void FillRequiredExpansions()
        {
            if (RequiresSotv)
                ArtifactDef.requiredExpansion = sharedInfo.SotVExpansion ? sharedInfo.SotVExpansion : SotvExpansion;
            else
                ArtifactDef.requiredExpansion = sharedInfo.Expansion;
        }

        public override void AddDisplayRules(ModdedIDRS which, ItemDisplayRule[] displayRules)
        {
        }

        public override void AddDisplayRules(VanillaIDRS which, ItemDisplayRule[] displayRules)
        {
        }
    }
}