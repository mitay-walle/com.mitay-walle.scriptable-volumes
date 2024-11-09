using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plugins.VFX.Volumes
{
	[Serializable]
	[SupportedOnScriptableProfile(typeof(ScriptableVolumeProfile))]
	public sealed class Reflections : SceneLightingComponent
	{
		[InlineProperty] public IntParameter reflectionBounces = new(4);
		[InlineProperty] public IntParameter defaultReflectionResolution = new(4);
		[InlineProperty] public EnumParameter<DefaultReflectionMode> defaultReflectionMode = new(DefaultReflectionMode.Skybox, true);
		[InlineProperty] public FloatParameter reflectionIntensity = new(1, true);
		[InlineProperty] public TextureParameter customReflectionTexture = new(null, true);

		public override void Apply(VolumeStack stack)
		{
			Reflections other = stack.GetComponent<Reflections>();

			if (other && other.active)
			{
				RenderSettings.reflectionBounces = other.reflectionBounces.value;
				RenderSettings.reflectionIntensity = other.reflectionIntensity.value;
				RenderSettings.customReflectionTexture = other.customReflectionTexture.value;
				RenderSettings.defaultReflectionMode = other.defaultReflectionMode.value;
				RenderSettings.defaultReflectionResolution = other.defaultReflectionResolution.value;
			}
		}
	}
}