using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BubbetsItems
{
	public class BubPickupDisplayCustom : MonoBehaviour
	{
		[SystemInitializer(typeof(GenericPickupController))]
		public static void ModifyGenericPickup()
		{
			_pickup = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Common/GenericPickup.prefab").WaitForCompletion();

			var pickupDisplay = _pickup.transform.Find("PickupDisplay").gameObject;
			pickupDisplay.AddComponent<BubPickupDisplayCustom>();
			
			var voidSystem = _pickup.transform.Find("VoidSystem");
			var voidSystemLoops = voidSystem.Find("Loops");
			var lunarSystem = _pickup.transform.Find("LunarSystem");
			var lunarSystemLoops = lunarSystem.Find("Loops");
			_voidLunarSystem = new GameObject("VoidLunarSystem");
			_voidLunarSystem.SetActive(false);
			DontDestroyOnLoad(_voidLunarSystem);
			

			//Setup Loops
			var voidLunarSystemLoops = new GameObject("Loops");
			voidLunarSystemLoops.transform.SetParent(_voidLunarSystem.transform);

			var swirls = Instantiate(lunarSystemLoops.Find("Swirls").gameObject, voidLunarSystemLoops.transform);
			var mainModule = swirls.GetComponent<ParticleSystem>().main;
			mainModule.startColor = new ParticleSystem.MinMaxGradient(ColorCatalogPatches.VoidLunarColor);
			
			Instantiate(voidSystemLoops.Find("DistantSoftGlow").gameObject, voidLunarSystemLoops.transform);
			Instantiate(voidSystemLoops.Find("Glowies").gameObject, voidLunarSystemLoops.transform);
			var pointLight = Instantiate(voidSystemLoops.Find("Point Light").gameObject, voidLunarSystemLoops.transform);
			pointLight.GetComponent<Light>().color = ColorCatalogPatches.VoidLunarColor;

			//Setup Bursts
			Instantiate(lunarSystem.Find("Burst").gameObject, _voidLunarSystem.transform).name = "LunarBurst";
			Instantiate(voidSystem.Find("Burst").gameObject, _voidLunarSystem.transform).name = "VoidBurst";
		}
		
		private static GameObject _voidLunarSystem = null!;
		private static GameObject _pickup = null!;
		private PickupDisplay _display = null!;
		private bool _set;

		private void Awake()
		{
			_display = GetComponent<PickupDisplay>();
		}

		public void Update()
		{
			var pickupDef = PickupCatalog.GetPickupDef(_display.pickupIndex);
			if (pickupDef == null || _set) return;
			_set = true;
			var itemIndex = pickupDef.itemIndex;
			var tier = ItemCatalog.GetItemDef(itemIndex)?.tier ?? ItemTier.NoTier;
			if (tier == BubbetsItemsPlugin.VoidLunarTier.tier)
				Instantiate(_voidLunarSystem, transform.parent).SetActive(true);
		}
	}
}