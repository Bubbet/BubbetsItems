using System;
using System.Collections;
using System.Reflection;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;

namespace MaterialHud
{
	[ExecuteAlways]
	public class FieldLoader : MonoBehaviour
	{
		public string addressablePath = "";
		public string targetFieldName = "";
		public Component target = null!;

		private static readonly MethodInfo? LoadAssetAsyncInfo = typeof(Addressables).GetMethod(nameof(Addressables.LoadAssetAsync), new[] { typeof(string) });

		private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.NonPublic;

		private void LoadAsset(bool dontSave = false)
		{
			var typ = target.GetType();
			var field = typ.GetField(targetFieldName, Flags);
			PropertyInfo? property = null;
			if (field == null)
			{
				property = typ.GetProperty(targetFieldName, Flags);
				if (property == null) return;
			}
			var meth = LoadAssetAsyncInfo?.MakeGenericMethod(field?.FieldType ?? property!.PropertyType);
			var awaiter = meth?.Invoke(null, new object[] { addressablePath });
			var wait = awaiter?.GetType().GetMethod("WaitForCompletion", BindingFlags.Instance | BindingFlags.Public);
			var asset = wait?.Invoke(awaiter, null);
			var assetObject = asset as UnityEngine.Object;
			if (assetObject == null) return;
			if (dontSave)
			{
				assetObject.hideFlags |= HideFlags.DontSave;
			}
			field?.SetValue(target, asset);
			property?.SetValue(target, asset);
		}

		private IEnumerator WaitAndLoadAsset()
		{
			yield return new WaitUntil(() => Addressables.InternalIdTransformFunc != null);
			LoadAsset(true);
		}

		private void Start()
		{
			LoadAsset();
		}

		private void OnValidate()
		{
			if(gameObject.activeInHierarchy) StartCoroutine(WaitAndLoadAsset());
		}
	}
	
	[ExecuteAlways]
	public class ParticleSystemMaterialLoader : MonoBehaviour
	{
		public string addressablePath = null!;
		public ParticleSystem target = null!;
		[FormerlySerializedAs("Tint")] public Color tint;

		private void LoadAsset(bool dontSave = false)
		{
			var renderer = target.GetComponent<ParticleSystemRenderer>();
			renderer.material = Addressables.LoadAssetAsync<Material>(addressablePath).WaitForCompletion();
			if (tint != default)
				renderer.material.SetColor("_TintColor", tint);
		}

		private IEnumerator WaitAndLoadAsset()
		{
			yield return new WaitUntil(() => Addressables.InternalIdTransformFunc != null);
			LoadAsset(true);
		}

		private void Start()
		{
			LoadAsset();
		}

		private void OnValidate()
		{
			if(gameObject.activeInHierarchy) StartCoroutine(WaitAndLoadAsset());
		}
	}

	[ExecuteAlways]
	public class PrefabChildLoader : MonoBehaviour
	{
		public string prefabAddress = "";
		public int childIndex;
		
		private bool _loading = false;
		private GameObject? _instance;
		private int _loadedIndex = -1;

		public UnityEvent finished = new();

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
			if (_loadedIndex != childIndex)
			{
				_loadedIndex = -1;
				if (_instance != null) DestroyImmediate(_instance);
			}

			if (string.IsNullOrEmpty(prefabAddress) || _loading) return;
			_loading = true;
			Addressables.LoadAssetAsync<GameObject>(prefabAddress).Completed += PrefabLoaded;
		}

		private void PrefabLoaded(AsyncOperationHandle<GameObject> obj)
		{
			switch (obj.Status)
			{
				case AsyncOperationStatus.Succeeded:
					if (_loadedIndex == childIndex) break;
					if (_instance != null) DestroyImmediate(_instance);
					var prefab = obj.Result;
					var parent = Instantiate(prefab);
					_instance = parent.transform.childCount > 0 ? parent.transform.GetChild(Math.Min(childIndex, parent.transform.childCount - 1)).gameObject : parent;

					var transformChild = _instance.transform;
					SetRecursiveFlags(transformChild);
					transformChild.eulerAngles = Vector3.zero;
					transformChild.position = Vector3.zero;
					transformChild.localScale = Vector3.one;
					transformChild.SetParent(gameObject.transform, false);
					if (parent.transform.childCount > 0)
						DestroyImmediate(parent);
					_loadedIndex = childIndex;
					_loading = false;
					finished.Invoke();
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
	
	[ExecuteAlways]
	public class ShaderLoader : MonoBehaviour
	{
		public string addressablePath = "";
		public Renderer target = null!;

		[ContextMenu("Fill In Editor")]
		[ExecuteAlways]
		public void Start()
		{
			var shader = Addressables.LoadAssetAsync<Shader>(addressablePath).WaitForCompletion();
			target.material.shader = shader;
			target.sharedMaterial.shader = shader;
			for (var i = 0; i < target.sharedMaterial.shader.GetPropertyCount(); i++)
			{
				Debug.Log(target.sharedMaterial.shader.GetPropertyFlags(i));
			}
		}
	}
}