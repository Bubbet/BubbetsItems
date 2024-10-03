using System.Collections;
using RoR2;
using UnityEngine;

namespace BubbetsItems.Behaviours
{
	public class AwakeHighlightRenderAssign : MonoBehaviour
	{
		public IEnumerator Start()
		{
			yield return new WaitForSeconds(2.5f);
			GetComponent<Highlight>().targetRenderer = GetComponentInChildren<Renderer>();
		}
	}
}