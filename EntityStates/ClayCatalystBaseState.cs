using System;
using BubbetsItems.Items.BarrierItems;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbetsItems.EntityStates
{
    public class ClayCatalystBaseState : EntityState
    {
        private HoldoutZoneController _holdoutZone = null!;
        private float _radius;
        private TeamIndex _teamIndex;
        private BuffWard _indicator = null!;
        private float _frequency;
        private float _barrierAdd;
        private int _pulseCount;

        public override void OnEnter()
        {
            base.OnEnter();
            if (!SharedBase.TryGetInstance<ClayCatalyst>(out var inst)) return;
            Transform parent = transform.parent;
            if (parent)
            {
                _holdoutZone = parent.GetComponentInParent<HoldoutZoneController>();
            }

            TeamFilter teamFilter = GetComponent<TeamFilter>();
            _teamIndex = teamFilter ? teamFilter.teamIndex : TeamIndex.None;

            if (NetworkServer.active)
            {
                var amount = Util.GetItemCountForTeam(_teamIndex, inst.ItemDef.itemIndex, false);
                _frequency = inst.ScalingInfos[2].ScalingFunction(amount);
                _barrierAdd = inst.ScalingInfos[3].ScalingFunction(amount);
                _radius = inst.ScalingInfos[0].ScalingFunction(amount);
            }

            _indicator = GetComponent<BuffWard>();
            _indicator.radius = _radius;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (_pulseCount < Mathf.FloorToInt(_holdoutZone.charge / _frequency))
            {
                foreach (var component1 in TeamComponent.GetTeamMembers(_indicator.teamFilter.teamIndex))
                {
                    var vector = component1.transform.position - _indicator.transform.position;
                    if (_indicator.shape == BuffWard.BuffWardShape.VerticalTube)
                    {
                        vector.y = 0f;
                    }

                    if (vector.sqrMagnitude <= _indicator.calculatedRadius * _indicator.calculatedRadius)
                    {
                        var component = component1.GetComponent<CharacterBody>();
                        if (component && (!_indicator.requireGrounded || !component.characterMotor ||
                                          component.characterMotor.isGrounded))
                        {
                            component.healthComponent.AddBarrier(_barrierAdd);
                        }
                    }
                }

                _pulseCount++;
                // TODO add vfx and sfx
            }

            if (!NetworkServer.active) return;
            if (Math.Abs(_holdoutZone.charge - 1f) < 0.01f)
            {
                Destroy(gameObject);
            }
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(_radius);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            _radius = reader.ReadSingle();
        }
    }
}