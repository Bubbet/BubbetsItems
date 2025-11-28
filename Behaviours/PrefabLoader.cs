using System.Collections.Generic;
using BubbetsItems;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ZedMod
{
    [ExecuteAlways]
    public class PrefabLoader : MonoBehaviour
    {
        public string prefabAddress = null!;
        private string _loadedPrefab = null!;
        private bool _loading = false;
        private GameObject _instance = null!;

        public static readonly Dictionary<string, string> AddressMap = new Dictionary<string, string>()
        {
            { "RoR2/Base/bazaar/NewtStatueProp.prefab", "RoR2/Base/bazaar/Bazaar_NewtStatue.prefab" },
            { "RoR2/Base/bazaar/LunarInfectionLargeMesh.prefab", "RoR2/Base/bazaar/Bazaar_LunarInfectionLarge.prefab" },
            { "RoR2/Base/arena/CrabFoam1Prop.prefab", "RoR2/Base/arena/Arena_CrabFoam.prefab" },
            { "RoR2/Base/bazaar/BazaarBoulder.prefab", "RoR2/Base/arena/BBBoulderMediumRound1.prefab" }, // bruh they moved my fucking rock
        };

        private void Start()
        {
            LoadPrefab();
        }

        private void OnValidate()
        {
            LoadPrefab();
        }

        private void LoadPrefab()
        {
            if (!string.IsNullOrEmpty(prefabAddress) && !_loading)
            {
                _loading = true;
                var address = AddressMap.GetValueOrDefault(prefabAddress, prefabAddress);
                //var address = prefabAddress;
                Addressables.LoadAssetAsync<GameObject>(address).Completed += PrefabLoaded;
            }
        }

        private void PrefabLoaded(AsyncOperationHandle<GameObject> obj)
        {
            switch (obj.Status)
            {
                case AsyncOperationStatus.Succeeded:
                    if (_loadedPrefab == prefabAddress) break;
                    if (_instance != null) DestroyImmediate(_instance);
                    var prefab = obj.Result;
                    _instance = Instantiate(prefab, gameObject.transform, false);
                    SetRecursiveFlags(_instance.transform);
                    _loadedPrefab = prefabAddress;
                    _loading = false;
                    break;
                case AsyncOperationStatus.Failed:
                    if (_instance != null) DestroyImmediate(_instance);
                    BubbetsItemsPlugin.Log.LogError($"Prefab load failed. {prefabAddress}");
                    _loading = false;
                    break;
                case AsyncOperationStatus.None:
                default:
                    // case AsyncOperationStatus.None:
                    break;
            }
        }

        private static void SetRecursiveFlags(Transform transform)
        {
            transform.gameObject.hideFlags |= HideFlags.DontSave;
            foreach (Transform child in transform)
            {
                SetRecursiveFlags(child);
            }
        }
    }
}