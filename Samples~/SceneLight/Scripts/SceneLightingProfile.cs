using UnityEngine;

namespace Plugins.VFX.Volumes
{
	[Icon("Packages/com.unity.render-pipelines.core/Editor/Icons/Processed/VolumeProfile Icon.asset")]
	[CreateAssetMenu]
	public class SceneLightingProfile : ScriptableVolumeProfileT<SceneLightingComponent>
	{
		public override void Apply(VolumeStack stack)
		{
			foreach (var kvp in stack.components)
			{
				if (kvp.Value is SceneLightingComponent component)
				{
					if (component && component.active)
					{
						component.Apply(stack);
					}
				}
			}
		}
	}
}