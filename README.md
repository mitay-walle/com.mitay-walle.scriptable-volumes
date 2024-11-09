# Scriptable Volumes
Unity Volume-system extracted from ScriptableRenderPipeline, allowing to blend any parameters, using collider-shapes + distances


![ScriptableVolumes_Demo](https://github.com/user-attachments/assets/39e5d2be-cbd7-4719-9f5b-9b3419c1588d)
![image](https://github.com/user-attachments/assets/059f9fb2-34c9-4027-8de4-aaff98678e0b)
![image](https://github.com/user-attachments/assets/3d28dce4-3210-4edd-abb1-e5a0cdcb833d)
![image](https://github.com/user-attachments/assets/08a02141-4aa7-4119-8480-e8f702bac13b)

- Important! Odin Inspector as dependency

# Example

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
