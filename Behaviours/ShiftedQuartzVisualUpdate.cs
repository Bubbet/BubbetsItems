using System.Linq;
using BubbetsItems.Items;
using RoR2;
using UnityEngine;

namespace BubbetsItems.Behaviours
{
	public class ShiftedQuartzVisualUpdate : MonoBehaviour
	{
		
		private void Awake()
		{
			_renderer = GetComponentInChildren<Renderer>();
		}

		private void Startup()
		{
			_started = true;
			_body = transform.parent.GetComponent<CharacterBody>();
			
			var allButNeutral = TeamMask.allButNeutral;
			var objectTeam = _body.teamComponent.teamIndex;
			if (objectTeam != TeamIndex.None)
			{
				allButNeutral.RemoveTeam(objectTeam);
			}
			_search = new BullseyeSearch
			{
				teamMaskFilter = allButNeutral,
				viewer = _body
			};
			_renderer.material.SetFloat(Color2BaseAlpha, ShiftedQuartz.VisualTransparency.Value);
			if (ShiftedQuartz.VisualOnlyForAuthority.Value && !_body.hasEffectiveAuthority)
			{
				_renderer.material.SetColor(Color1, Color.clear);
				_renderer.material.SetColor(Color2, Color.clear);
			}
		}

		private bool Search()
		{
			_search.searchOrigin = gameObject.transform.position;
			_search.RefreshCandidates();
			return _search.GetResults()?.Any() ?? false;
		}

		private void FixedUpdate()
		{
			if(!transform.parent) return;
			if(!_started) Startup();
			_search.maxDistanceFilter = transform.localScale.z / 2f;
			inside = Search();
			var inRadius = inside ? 1f : 0f;
			_renderer.material.SetFloat(ColorMix, inRadius);
		}
		
		private Renderer _renderer = null!;
		private BullseyeSearch _search = null!;
		public bool inside;
		private CharacterBody _body = null!;
		private bool _started;
		private static readonly int Color2BaseAlpha = Shader.PropertyToID("_Color2BaseAlpha");
		private static readonly int Color1 = Shader.PropertyToID("_Color");
		private static readonly int Color2 = Shader.PropertyToID("_Color2");
		private static readonly int ColorMix = Shader.PropertyToID("_ColorMix");
	}
}