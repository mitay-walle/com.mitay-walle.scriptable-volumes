using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plugins.VFX.Volumes
{
	[Serializable]
	[SupportedOnScriptableProfile(typeof(ScriptableVolumeProfile))]
	public sealed class Ambient : SceneLightingComponent
	{
		[InlineProperty] public EnumParameter<AmbientMode> mode = new(AmbientMode.Custom);
		[InlineProperty] public ClampedFloatParameter ambientIntensity = new(1, 0, 1, true);
		[InlineProperty, ShowIf("@mode==AmbientMode.Flat")] public ColorParameter ambientLight = new(Color.gray, false, false, true);
		[InlineProperty, ShowIf("@mode==AmbientMode.Trilight")] public ColorParameter ambientSkyColor = new(Color.gray, false, false, true);
		[InlineProperty, ShowIf("@mode==AmbientMode.Trilight")] public ColorParameter ambientEquatorColor = new(Color.gray, false, false, true);
		[InlineProperty, ShowIf("@mode==AmbientMode.Trilight")] public ColorParameter ambientGroundColor = new(Color.gray, false, false, true);
		[InlineProperty, ShowIf("@mode==AmbientMode.Skybox")] public MaterialParameter skybox = new(null);
		[InlineProperty, ShowIf("@mode==AmbientMode.Custom")] public CubemapParameter customReflectionTexture = new(null);

		public override void Apply(VolumeStack stack)
		{
			Ambient other = stack.GetComponent<Ambient>();

			if (other && other.active)
			{
				RenderSettings.ambientMode = other.mode.value;
				RenderSettings.ambientIntensity = other.ambientIntensity.value;
				RenderSettings.ambientLight = other.ambientLight.value;
				RenderSettings.ambientSkyColor = other.ambientSkyColor.value;
				RenderSettings.ambientEquatorColor = other.ambientEquatorColor.value;
				RenderSettings.ambientGroundColor = other.ambientGroundColor.value;
				RenderSettings.customReflectionTexture = other.customReflectionTexture.value;
				RenderSettings.skybox = other.skybox.value;
			}
		}
	}
}