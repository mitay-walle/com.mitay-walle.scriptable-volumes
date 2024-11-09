using System;
using UnityEngine;

namespace Plugins.VFX.Volumes
{
	[Icon("Packages/com.unity.render-pipelines.core/Editor/Icons/Processed/VolumeProfile Icon.asset")]
	[Serializable]
	[SupportedOnCustomProfile(typeof(CustomVolumeProfile))]
	public abstract class SceneLightingComponent : CustomVolumeComponent { }
}