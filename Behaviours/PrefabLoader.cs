﻿using System;
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
				Addressables.LoadAssetAsync<GameObject>(prefabAddress).Completed += PrefabLoaded;
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
					Debug.LogError("Prefab load failed.");
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
			foreach(Transform child in transform)
			{
				SetRecursiveFlags(child);
			}
		}
	}
}