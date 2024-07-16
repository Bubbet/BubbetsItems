using System;
using UnityEngine;

namespace BubbetsItems.Behaviours
{
	[ExecuteAlways]
	public class InfectionRampSwap : MonoBehaviour
	{
#pragma warning disable CS8618
		public Texture2D ramp;
#pragma warning restore CS8618
		public Color color;
		private static readonly int Color1 = Shader.PropertyToID("_Color");

		public void ReplaceRamp()
		{
			//child.GetComponent<Renderer>().material.SetTexture("_RemapTex", ramp);
			GetComponentInChildren<Renderer>().material.SetColor(Color1, new Color(3f, 0f, 1f, 1f));
		}
	}
}