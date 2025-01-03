﻿using System.Linq;
using System.Reflection;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace BubbetsItems
{
	[HarmonyPatch]
	public static class ColorCatalogPatches
	{
		public static FieldInfo? indexToColor32 = typeof(ColorCatalog).GetField("indexToColor32", BindingFlags.Static | BindingFlags.NonPublic);
		public static FieldInfo? indexToHexString = typeof(ColorCatalog).GetField("indexToHexString", BindingFlags.Static | BindingFlags.NonPublic);
		
		public static Color32 VoidLunarColor = new(134, 0, 203, 255);

		
		public static void AddNewColors()
		{
			var len = ColorCatalog.indexToColor32.Length;
			BubbetsItemsPlugin.VoidLunarTier.colorIndex = (ColorCatalog.ColorIndex) len;
			BubbetsItemsPlugin.VoidLunarTier.darkColorIndex = (ColorCatalog.ColorIndex) len + 1;
			
			var voidLunarDark = new Color32(83, 0, 126, 255);
            
			indexToColor32?.SetValue(null, ColorCatalog.indexToColor32.AddItem(VoidLunarColor).AddItem(voidLunarDark).ToArray());
			indexToHexString?.SetValue(null, ColorCatalog.indexToHexString.AddItem(Util.RGBToHex(VoidLunarColor)).AddItem(Util.RGBToHex(voidLunarDark)).ToArray());
		}

		[HarmonyPrefix, HarmonyPatch(typeof(ColorCatalog), nameof(ColorCatalog.GetColor))]
		public static bool PatchGetColor(ColorCatalog.ColorIndex colorIndex, ref Color32 __result)
		{
			var ind = (int) colorIndex;
			if (ind >= ColorCatalog.indexToColor32.Length) return true;
			__result = ColorCatalog.indexToColor32[ind];
			return false;
		}
		
		[HarmonyPrefix, HarmonyPatch(typeof(ColorCatalog), nameof(ColorCatalog.GetColorHexString))]
		public static bool GetColorHexString(ColorCatalog.ColorIndex colorIndex, ref string __result)
		{
			var ind = (int) colorIndex;
			if (ind >= ColorCatalog.indexToHexString.Length) return true;
			__result = ColorCatalog.indexToHexString[ind];
			return false;
		}
	}
}