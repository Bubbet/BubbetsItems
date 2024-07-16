using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RoR2;
using RoR2.UI;
using RoR2.UI.LogBook;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace BubbetsItems
{
    [HarmonyPatch]
    public class LogBookPageScalingGraph : MonoBehaviour //Graphic
    {
        [FormerlySerializedAs("RectTransform")] public RectTransform rectTransform = null!;
        [FormerlySerializedAs("LineRenderer")] public ManagerGraphic lineRenderer = null!;

        public ItemBase Item { get; set; } = null!;

        private void FillWidthAndHeight()
        {
            //width = RectTransform.rect.width;
            //height = RectTransform.rect.height;
            //width = RectTransform.sizeDelta.x * 5;
            //height = RectTransform.sizeDelta.y * 5;
            //var rect = transform.parent.GetComponent<RectTransform>().rect;
            var rect = rectTransform.rect;
            _width = rect.width;
            _height = rect.height;
            //Debug.Log(width);
            lineRenderer.size = new Vector2(_width, _height);
        }

        public bool built;
        /* TODO remake this
        public void FixedUpdate()
        {
            if (!built && Math.Abs(RectTransform.rect.width - 100) > 0.01f)
                BuildGraph();
        }*/

        //private Func<int, float> test = i => Mathf.Log(i)*3f + 1.15f;
        public void BuildGraph()
        {
            built = true;
            //if (Item.scalingFunction == null) return;
            FillWidthAndHeight();
            // ReSharper disable once CollectionNeverUpdated.Local
            var points = new List<float>();
            /*
            for (var i = 0; i < 50; i++)
            {
                points.Add(Item.GraphScalingFunction(i+1));
                //points.Add(test(i+1));
            }*/
            var max = Mathf.Ceil(points.Max());
            for (var i = 0; i < 50; i++)
            {
                var pos = new Vector2(Mathf.Clamp01((float) (i) / 49f) * _width - 0.5f * _width,
                    Mathf.Clamp01(points[i] / max) * _height - 0.5f * _height);
                
                GameObject go = new GameObject();
                go.transform.SetParent(lineRenderer.transform, false);

                var img = go.AddComponent<Image>();
                img.rectTransform.sizeDelta = new Vector2(8.0f, 8.0f);
                img.rectTransform.localPosition = pos;
                
                var tooltip = go.AddComponent<TooltipProvider>();
                tooltip.SetContent(new TooltipContent {overrideTitleText = "Scaling Value", overrideBodyText = $"Amount: {i+1}, Value: {points[i]}", titleColor = Color.grey});
                
                lineRenderer.lineStrip.Add(pos);
            }

            lineRenderer.gridSize.y = (int) max;
            
            lineRenderer.SetVerticesDirty();
        }
        
        [FormerlySerializedAs("Granularity")] public int granularity = 50;

        public Vector2Int gridSize = new(1, 1);
        public float thickness = 10f;

        private float _width;
        private float _height;




        /*
        private void BuildGraph()
        {
            var granularity = 50;
            var gover2 = granularity * 0.5;
            var sizeDelta = RectTransform.sizeDelta;
            var xwidth = sizeDelta.x / granularity;
            var amounts = new List<float>();
            for (var i = 0; i < granularity; i++) amounts.Add(Item.scalingFunction(new ItemBase.ExpressionContext(i)));
            var max = amounts.Max();
            var i2 = 0;
            //LineRenderer.SetPositions(Positions(new Vector3[] {});
            //LineRenderer.positionCount = granularity;
            var positions = new List<Vector3>();
            foreach (var yAmount in amounts)
            {
                var y = (float) (yAmount / max * sizeDelta.y - sizeDelta.y * 0.5);
                var x = (float) (xwidth * i2 - gover2);
                var pos = new Vector3(x, y, 1);
                positions.Add(pos);
                i2++;
            }
            LineRenderer.SetPositions(positions.ToArray());
        }*/

        //[HarmonyPostfix, HarmonyPatch(typeof(PageBuilder), nameof(PageBuilder.AddSimplePickup))] //TODO
        // ReSharper disable once InconsistentNaming
        public static void AddGraph(PageBuilder __instance, PickupIndex pickupIndex)
        {
            if (!SharedBase.PickupIndexes.ContainsKey(pickupIndex)) return;
            var item = SharedBase.PickupIndexes[pickupIndex] as ItemBase;
            //if (item?.scalingFunction == null) return;
            if (item == null) return;
            __instance.AddSimpleTextPanel("Scaling Function:");
            var obj = __instance.managedObjects[__instance.managedObjects.Count - 1];
            var graph = Instantiate(BubbetsItemsPlugin.AssetBundle.LoadAsset<GameObject>("LogBookGraph"), obj.transform);
            graph.GetComponent<LogBookPageScalingGraph>().Item = item;
        }
    }
}