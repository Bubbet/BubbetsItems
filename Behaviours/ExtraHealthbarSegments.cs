using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BubbetsItems.Behaviours
{
	[HarmonyPatch]
	public static class ExtraHealthBarSegments
	{
		private static List<Type> _barDataTypes = new();

		public static void AddType<T>() where T : BarData, new()
		{
			_barDataTypes.Add(typeof(T));
		}
		
		/*
		public static event Func<BarData> collectExtraHealthBarStyles;
		public static IEnumerable<BarData> CollectBarInfosInvoke()
		{
			return collectExtraHealthBarStyles.GetInvocationList().Select(dele => ((Func<BarData>) dele).Invoke());
		}*/
		
		[HarmonyPostfix, HarmonyPatch(typeof(HealthBar), nameof(HealthBar.Awake))]
		// ReSharper disable once InconsistentNaming
		public static void AddTracker(HealthBar? __instance)
		{
			if (__instance != null)
				__instance.gameObject.AddComponent<BubsExtraHealthbarInfoTracker>().Init(__instance);
		}

		[HarmonyPostfix, HarmonyPatch(typeof(HealthBar), nameof(HealthBar.CheckInventory))]
		// ReSharper disable once InconsistentNaming
		public static void CheckInventory(HealthBar __instance)
		{
			var tracker = __instance.GetComponent<BubsExtraHealthbarInfoTracker>();
			if (!tracker) return;
			var source = __instance.source;
			if (!source) return;
			var body = source.body;
			if (!body) return;
			var inv = body.inventory;
			if (!inv) return;
			tracker.CheckInventory(inv, body, source);
		}

		[HarmonyPostfix, HarmonyPatch(typeof(HealthBar), nameof(HealthBar.UpdateBarInfos))]
		// ReSharper disable once InconsistentNaming
		public static void UpdateInfos(HealthBar __instance)
		{
			var tracker = __instance.GetComponent<BubsExtraHealthbarInfoTracker>();
			tracker.UpdateInfo();
		}

		[HarmonyILManipulator, HarmonyPatch(typeof(HealthBar), nameof(HealthBar.ApplyBars))]
		public static void ApplyBar(ILContext il)
		{
			var c = new ILCursor(il);

			var cls = -1;
			FieldReference? fld = null;
			c.GotoNext(
				x => x.MatchLdloca(out cls),
				x => x.MatchLdcI4(0),
				x => x.MatchStfld(out fld)
			);
			
			c.GotoNext(MoveType.After,
				x => x.MatchCallOrCallvirt<HealthBar.BarInfoCollection>(nameof(HealthBar.BarInfoCollection.GetActiveCount))
			);
			c.Emit(OpCodes.Ldarg_0);
			c.EmitDelegate<Func<int, HealthBar, int>>((i, bar) =>
			{
				var tracker = bar.GetComponent<BubsExtraHealthbarInfoTracker>();
				i += tracker.BarInfos.Count(x => x.Info.enabled);
				return i;
			});
			c.Index = il.Instrs.Count - 2;
			c.Emit(OpCodes.Ldloca, cls);
			c.Emit(OpCodes.Ldarg_0);
			c.Emit(OpCodes.Ldloca, cls);
			c.Emit(OpCodes.Ldfld, fld);
			c.EmitDelegate<Func<HealthBar, int, int>>((bar, i) =>
			{
				var tracker = bar.GetComponent<BubsExtraHealthbarInfoTracker>();
				tracker.ApplyBar(ref i);
				return i;
				//return tracker.ApplyBar();
			});
			c.Emit(OpCodes.Stfld, fld);;
			/*
			c.Index = il.Instrs.Count - 1;
			c.Emit(OpCodes.Ldarg_0);
			c.Emit(OpCodes.Ldloc_0);
			c.EmitDelegate<Func<HealthBar, object, object>>((HealthBar bar, object fuck) =>
			{
				var tracker = bar.GetComponent<BubsExtraHealthbarInfoTracker>();
				tracker.ApplyBar(ref fuck);
				return fuck;
			});
			c.Emit(OpCodes.Stloc_0);*/
		}
		
		public abstract class BarData
		{
			public BubsExtraHealthbarInfoTracker Tracker = null!;
			public HealthBar? Bar;
			public HealthBar.BarInfo Info;
			public HealthBarStyle.BarStyle? CachedStyle;
			private Image? _imageReference;
			public virtual Image? ImageReference
			{
				get => _imageReference;
				set
				{
					if (_imageReference != null && _imageReference && _imageReference != value && Bar != null)
					{
						_imageReference.material = Bar.barAllocator.elementPrefab.GetComponent<Image>().material;
					}
					_imageReference = value;
				}
			}

			public abstract HealthBarStyle.BarStyle GetStyle();

			public virtual void UpdateInfo(ref HealthBar.BarInfo inf, HealthComponent.HealthBarValues healthBarValues)
			{
				if (CachedStyle == null) CachedStyle = GetStyle();
				var style = CachedStyle.Value;
				
				inf.enabled &= style.enabled;
				inf.color = style.baseColor;
				inf.imageType = style.imageType;
				inf.sprite = style.sprite;
				inf.sizeDelta = style.sizeDelta;
			}

			public virtual void CheckInventory(ref HealthBar.BarInfo inf, Inventory inventory, CharacterBody characterBody, HealthComponent healthComponent) {}
			public virtual void ApplyBar(ref HealthBar.BarInfo inf, Image image, ref int i)
			{
				image.type = inf.imageType;
				image.sprite = inf.sprite;
				image.color = inf.color;

				var rectTransform = (RectTransform) image.transform;
				rectTransform.anchorMin = new Vector2(inf.normalizedXMin, 0f);
				rectTransform.anchorMax = new Vector2(inf.normalizedXMax, 1f);
				rectTransform.anchoredPosition = Vector2.zero;
				rectTransform.sizeDelta = new Vector2(inf.sizeDelta * 0.5f + 1f, inf.sizeDelta + 1f);

				i++;
			}
		}

		public class BubsExtraHealthbarInfoTracker : MonoBehaviour
		{
			public List<BarData> BarInfos = null!;
			public HealthBar? healthBar;
			
			public void CheckInventory(Inventory inv, CharacterBody characterBody, HealthComponent healthComponent)
			{
				foreach (var barInfo in BarInfos)
				{
					barInfo.CheckInventory(ref barInfo.Info, inv, characterBody, healthComponent);
				}
			}
			public void UpdateInfo()
			{
				if (healthBar == null || !healthBar || !healthBar.source) return;
				var healthBarValues = healthBar.source.GetHealthBarValues();
				foreach (var barInfo in BarInfos)
				{
					if(barInfo.Tracker == null)
						barInfo.Tracker = this;
					if(barInfo.Bar == null) // I cant do this in the init because it loses its reference somehow
						barInfo.Bar = healthBar;
					barInfo.UpdateInfo(ref barInfo.Info, healthBarValues);
				}
			}
			public void ApplyBar(ref int i)
			{
				foreach (var barInfo in BarInfos)
				{
					ref var info = ref barInfo.Info;
					if (!info.enabled)
					{
						barInfo.ImageReference = null; // Release the reference.
						continue;
					}

					if (healthBar == null) continue;
					Image image = healthBar.barAllocator.elements[i];
					barInfo.ImageReference = image;
					barInfo.ApplyBar(ref barInfo.Info, image, ref i);
				}
			}

			public void Init(HealthBar? hBar)
			{
				healthBar = hBar;
				BarInfos = _barDataTypes.Select(dataType => (BarData) Activator.CreateInstance(dataType)).ToList();
			}
		}
	}
}