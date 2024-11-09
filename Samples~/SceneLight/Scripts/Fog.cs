using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Plugins.VFX.Volumes
{
	[Serializable]
	[SupportedOnScriptableProfile(typeof(ScriptableVolumeProfile))]
	public sealed class Fog : SceneLightingComponent
	{
		[InlineProperty] public EnumParameter<FogMode> mode = new(FogMode.Linear);
		[InlineProperty] public ColorParameter color = new(Color.gray, false, false, true);
		[InlineProperty] public FloatParameter density = new(.001f, true);
		[InlineProperty] public FloatParameter start = new(0, true);
		[InlineProperty] public FloatParameter end = new(150, true);

		public override void Apply(VolumeStack stack)
		{
			Fog other = stack.GetComponent<Fog>();

			if (other && other.active)
			{
				RenderSettings.fogMode = other.mode.value;
				RenderSettings.fogColor = other.color.value;
				RenderSettings.fogDensity = other.density.value;
				RenderSettings.fogStartDistance = other.start.value;
				RenderSettings.fogEndDistance = other.end.value;
			}
		}
	}
}