using System;
using System.Linq;
using BubbetsItems.Items;
using HarmonyLib;
using RoR2;
using RoR2.Items;
using UnityEngine;

namespace BubbetsItems.ItemBehaviors
{
    public class ShiftedQuartzBehavior : BaseItemBodyBehavior
    {
        [ItemDefAssociation(useOnServer = true, useOnClient = false)]
        private static ItemDef? GetItemDef()
        {
            return SharedBase.TryGetInstance(out ShiftedQuartz inst) ? inst.ItemDef : null;
        }

        private void OnEnable()
        {
            if (!SharedBase.TryGetInstance(out ShiftedQuartz instance)) return;
            var allButNeutral = TeamMask.allButNeutral;
            var objectTeam = TeamComponent.GetObjectTeam(gameObject);
            if (objectTeam != TeamIndex.None)
            {
                allButNeutral.RemoveTeam(objectTeam);
            }

            _search = new BullseyeSearch
            {
                maxDistanceFilter = instance.ScalingInfos[0].ScalingFunction(stack),
                teamMaskFilter = allButNeutral,
                viewer = body
            };
            IndicatorEnabled = true;
        }

        private bool Search()
        {
            if (_search == null) return false;
            _search.searchOrigin = gameObject.transform.position;
            _search.RefreshCandidates();
            return _search.GetResults()?.Any() ?? false;
        }

        private void OnDisable()
        {
            IndicatorEnabled = false;
            _search = null;
        }

        private void FixedUpdate()
        {
            if (!SharedBase.TryGetInstance(out ShiftedQuartz instance)) return;
            if (_search != null) _search.maxDistanceFilter = instance.ScalingInfos[0].ScalingFunction(stack);
            inside = Search();
        }

        private bool IndicatorEnabled
        {
            get => _nearbyDamageBonusIndicator;
            set
            {
                if (IndicatorEnabled == value)
                {
                    return;
                }

                if (value)
                {
                    var original = BubbetsItemsPlugin.AssetBundle.LoadAsset<GameObject>("FarDamageBonusIndicator");
                    _nearbyDamageBonusIndicator = Instantiate(original, body.corePosition, Quaternion.identity);
                    if (_search != null)
                    {
                        var radius = _search.maxDistanceFilter / 20f;
                        _nearbyDamageBonusIndicator.transform.localScale *= radius;
                    }

                    _nearbyDamageBonusIndicator.GetComponent<NetworkedBodyAttachment>()
                        .AttachToGameObjectAndSpawn(
                            gameObject); // TODO figure out what the fuck this is doing and replace it with my own client and server ran code
                    return;
                }

                Destroy(_nearbyDamageBonusIndicator);
                _nearbyDamageBonusIndicator = null;
            }
        }

        private GameObject? _nearbyDamageBonusIndicator;
        private BullseyeSearch? _search;
        public bool inside;
    }
}