using System;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BubbetsItems.Behaviours
{
	public class SpawnVoidOrb : MonoBehaviour
	{
		private bool _loading = false;
		private GameObject? _instance;
		private bool _inEditor;

		private void Start()
		{
			LoadPrefab();
		}

		private void OnValidate()
		{
			_inEditor = true;
			LoadPrefab();
		}

		private void LoadPrefab()
		{
			if (!_loading)
			{
				_loading = true;
				Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidChest/VoidChest.prefab").Completed += PrefabLoaded;
			}
		}

		private void PrefabLoaded(AsyncOperationHandle<GameObject> obj)
		{
			switch (obj.Status)
			{
				case AsyncOperationStatus.Succeeded:
					DestroyInst();
					var prefab = obj.Result;
					var parent = Instantiate(prefab);
					_instance = parent.transform.GetChild(0).GetChild(0).gameObject;

					var transformChild = _instance.transform;
					DestroyImmediate(transformChild.GetChild(2).gameObject);
					var root = transformChild.GetChild(1);
					root.Find("Base").localScale = Vector3.zero;
					root.Find("ROOT").Find("CenterBall.1").localScale = Vector3.zero;
					//root.Find("Center").localScale = Vector3.zero;

					DestroyImmediate(transformChild.GetComponent<Animator>());
					DestroyImmediate(transformChild.GetComponent<ChildLocator>());
					DestroyImmediate(transformChild.GetComponent<EntityLocator>());
					DestroyImmediate(transformChild.GetComponent<AnimationEvents>());
					DestroyImmediate(transformChild.GetComponentInChildren<Light>());
					
					if (_inEditor)
						SetRecursiveFlags(transformChild);
					else
					{
						DontDestroyOnLoad(_instance);
						_instance.hideFlags |= HideFlags.HideInInspector | HideFlags.HideInHierarchy;
					}

					transformChild.eulerAngles = Vector3.zero;
					transformChild.position = Vector3.zero;
					transformChild.localScale = Vector3.one;
					transformChild.SetParent(gameObject.transform, false);
					if (_inEditor)
						DestroyImmediate(parent);
					else
						parent.SetActive(false); // I have no fucking idea why but deleting the parent also deletes the now disjointed child
						//Destroy(parent);
					
					transformChild.eulerAngles = Vector3.zero;
					transformChild.localPosition = Vector3.zero; 
					
					
					_loading = false;
					break;
				case AsyncOperationStatus.Failed:
					DestroyInst();
					Debug.LogError("Prefab load failed.");
					_loading = false;
					break;
				default:
					// case AsyncOperationStatus.None:
					break;
			}
		}

		public void DestroyInst()
		{
			if (_instance == null) return;
			if (_inEditor) 
				DestroyImmediate(_instance);
			else
				Destroy(_instance);
		}

		private void OnDestroy()
		{
			DestroyInst();
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