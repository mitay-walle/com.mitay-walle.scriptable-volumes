using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Plugins.VFX.Volumes
{
	public class ScriptableVolumeManager
	{
		static readonly ProfilerMarker k_ProfilerMarkerUpdate = new("CustomVolumeManager.Update");
		static readonly ProfilerMarker k_ProfilerMarkerReplaceData = new("CustomVolumeManager.ReplaceData");
		static readonly ProfilerMarker k_ProfilerMarkerEvaluateVolumeDefaultState = new("CustomVolumeManager.EvaluateVolumeDefaultState");

		static readonly Lazy<ScriptableVolumeManager> s_Instance = new Lazy<ScriptableVolumeManager>(() => new ScriptableVolumeManager());

		/// <summary>
		/// The current singleton instance of <see cref="ScriptableVolumeManager"/>.
		/// </summary>
		public static ScriptableVolumeManager instance => s_Instance.Value;

		/// <summary>
		/// A reference to the main <see cref="VolumeStack"/>.
		/// </summary>
		/// <seealso cref="VolumeStack"/>
		public VolumeStack stack { get; set; }

		/// <summary>
		/// The current list of all available types that derive from <see cref="ScriptableVolumeComponent"/>.
		/// </summary>
		[Obsolete("Please use baseComponentTypeArray instead.")]
		public IEnumerable<Type> baseComponentTypes => baseComponentTypeArray;

		static readonly Dictionary<Type, List<(string, Type)>> s_SupportedVolumeComponentsForRenderPipeline = new();

		/// <summary>
		/// The current list of all available types that derive from <see cref="ScriptableVolumeComponent"/>.
		/// </summary>
		public Type[] baseComponentTypeArray { get; internal set; }// internal only for tests

		/// <summary>
		/// Global default profile that provides default values for volume components. CustomVolumeManager applies
		/// this profile to its internal component default state first, before <see cref="qualityDefaultProfile"/>
		/// and <see cref="customDefaultProfiles"/>.
		/// </summary>
		public ScriptableVolumeProfile globalDefaultProfile { get; private set; }

		/// <summary>
		/// Quality level specific volume profile that is applied to the default state after
		/// <see cref="globalDefaultProfile"/> and before <see cref="customDefaultProfiles"/>.
		/// </summary>
		public ScriptableVolumeProfile qualityDefaultProfile { get; private set; }

		/// <summary>
		/// Collection of additional default profiles that can be used to override default values for volume components
		/// in a way that doesn't cause any overhead at runtime. Unity applies these CustomVolume Profiles to its internal
		/// component default state after <see cref="globalDefaultProfile"/> and <see cref="qualityDefaultProfile"/>.
		/// The custom profiles are applied in the order that they appear in the collection.
		/// </summary>
		public ReadOnlyCollection<ScriptableVolumeProfile> customDefaultProfiles { get; private set; }

		private readonly ScriptableVolumeCollection m_VolumeCollection = new ScriptableVolumeCollection();

		// Internal list of default state for each component type - this is used to reset component
		// states on update instead of having to implement a Reset method on all components (which
		// would be error-prone)
		// The "Default State" is evaluated as follows:
		//   Default-constructed VolumeComponents (VolumeParameter values coming from code)
		// + Values from globalDefaultProfile
		// + Values from qualityDefaultProfile
		// + Values from customDefaultProfiles
		// = Default State.
		ScriptableVolumeComponent[] m_ComponentsDefaultState;

		// Flat list of every volume parameter in default state for faster per-frame stack reset.
		internal ScriptableVolumeParameter[] m_ParametersDefaultState;

		/// <summary>
		/// Retrieve the default state for a given CustomVolumeComponent type. Default state is defined as
		/// "default-constructed CustomVolumeComponent + Default Profiles evaluated in order".
		/// </summary>
		/// <remarks>
		/// If you want just the CustomVolumeComponent with default-constructed values without overrides from
		/// Default Profiles, use <see cref="ScriptableObject.CreateInstance(Type)"/>.
		/// </remarks>
		/// <param name="volumeComponentType">Type of CustomVolumeComponent</param>
		/// <returns>CustomVolumeComponent in default state, or null if the type is not found</returns>
		public ScriptableVolumeComponent GetVolumeComponentDefaultState(Type volumeComponentType)
		{
			if (!typeof(ScriptableVolumeComponent).IsAssignableFrom(volumeComponentType))
				return null;

			foreach (ScriptableVolumeComponent component in m_ComponentsDefaultState)
			{
				if (component.GetType() == volumeComponentType)
					return component;
			}

			return null;
		}

		// Recycled list used for volume traversal
		readonly List<Collider> m_TempColliders = new(8);

		// The default stack the volume manager uses.
		// We cache this as users able to change the stack through code and
		// we want to be able to switch to the default one through the ResetMainStack() function.
		VolumeStack m_DefaultStack;

		// List of stacks created through CustomVolumeManager.
		readonly List<VolumeStack> m_CreatedVolumeStacks = new();

		// Internal for tests
		internal ScriptableVolumeManager()
		{
			Initialize();
		}

		// Note: The "isInitialized" state and explicit Initialize/Deinitialize are only required because VolumeManger
		// is a singleton whose lifetime exceeds that of RenderPipelines. Thus it must be initialized & deinitialized
		// explicitly by the RP to handle pipeline switch gracefully. It would be better to get rid of singletons and
		// have the RP own the class instance instead.
		/// <summary>
		/// Returns whether <see cref="ScriptableVolumeManager.Initialize(ScriptableVolumeProfile,ScriptableVolumeProfile)"/> has been called, and the
		/// class is in valid state. It is not valid to use CustomVolumeManager before this returns true.
		/// </summary>
		public bool isInitialized { get; private set; }

		/// <summary>
		/// Initialize CustomVolumeManager with specified global and quality default volume profiles that are used to evaluate
		/// the default state of all VolumeComponents. Should be called from <see cref="RenderPipeline"/> constructor.
		/// </summary>
		/// <param name="globalDefaultVolumeProfile">Global default volume profile.</param>
		/// <param name="qualityDefaultVolumeProfile">Quality default volume profile.</param>
		public void Initialize(ScriptableVolumeProfile globalDefaultVolumeProfile = null, ScriptableVolumeProfile qualityDefaultVolumeProfile = null)
		{
			Debug.Assert(!isInitialized);
			Debug.Assert(m_CreatedVolumeStacks.Count == 0);

			LoadBaseTypes(typeof(ScriptableVolumeProfile));
			InitializeVolumeComponents();

			globalDefaultProfile = globalDefaultVolumeProfile;
			qualityDefaultProfile = qualityDefaultVolumeProfile;
			EvaluateVolumeDefaultState();

			m_DefaultStack = CreateStack();
			stack = m_DefaultStack;

			isInitialized = true;
		}

		/// <summary>
		/// Deinitialize CustomVolumeManager. Should be called from <see cref="RenderPipeline.Dispose()"/>.
		/// </summary>
		public void Deinitialize()
		{
			Debug.Assert(isInitialized);
			DestroyStack(m_DefaultStack);
			m_DefaultStack = null;
			foreach (var s in m_CreatedVolumeStacks)
				s.Dispose();
			m_CreatedVolumeStacks.Clear();
			baseComponentTypeArray = null;
			globalDefaultProfile = null;
			qualityDefaultProfile = null;
			customDefaultProfiles = null;
			isInitialized = false;
		}

		/// <summary>
		/// Assign the given CustomVolumeProfile as the global default profile and update the default component state.
		/// </summary>
		/// <param name="profile">The CustomVolumeProfile to use as the global default profile.</param>
		public void SetGlobalDefaultProfile(ScriptableVolumeProfile profile)
		{
			globalDefaultProfile = profile;
			EvaluateVolumeDefaultState();
		}

		/// <summary>
		/// Assign the given CustomVolumeProfile as the quality default profile and update the default component state.
		/// </summary>
		/// <param name="profile">The CustomVolumeProfile to use as the quality level default profile.</param>
		public void SetQualityDefaultProfile(ScriptableVolumeProfile profile)
		{
			qualityDefaultProfile = profile;
			EvaluateVolumeDefaultState();
		}

		/// <summary>
		/// Assign the given VolumeProfiles as custom default profiles and update the default component state.
		/// </summary>
		/// <param name="profiles">List of VolumeProfiles to set as default profiles, or null to clear them.</param>
		public void SetCustomDefaultProfiles(List<ScriptableVolumeProfile> profiles)
		{
			List<ScriptableVolumeProfile> validProfiles = profiles ?? new List<ScriptableVolumeProfile>();
			validProfiles.RemoveAll(x => x == null);
			customDefaultProfiles = new ReadOnlyCollection<ScriptableVolumeProfile>(validProfiles.ToArray());
			EvaluateVolumeDefaultState();
		}

		/// <summary>
		/// Call when a CustomVolumeProfile is modified to trigger default state update if necessary.
		/// </summary>
		/// <param name="profile">CustomVolumeProfile that has changed.</param>
		public void OnVolumeProfileChanged(ScriptableVolumeProfile profile)
		{
			if (!isInitialized)
				return;

			if (globalDefaultProfile == profile ||
				qualityDefaultProfile == profile ||
				(customDefaultProfiles != null && customDefaultProfiles.Contains(profile)))
				EvaluateVolumeDefaultState();
		}

		/// <summary>
		/// Call when a CustomVolumeComponent is modified to trigger default state update if necessary.
		/// </summary>
		/// <param name="component">CustomVolumeComponent that has changed.</param>
		public void OnVolumeComponentChanged(ScriptableVolumeComponent component)
		{
			var defaultProfiles = new List<ScriptableVolumeProfile> { globalDefaultProfile, globalDefaultProfile };
			if (customDefaultProfiles != null)
				defaultProfiles.AddRange(customDefaultProfiles);

			foreach (var defaultProfile in defaultProfiles)
			{
				if (defaultProfile.components.Contains(component))
				{
					EvaluateVolumeDefaultState();
					return;
				}
			}
		}

		/// <summary>
		/// Creates and returns a new <see cref="VolumeStack"/> to use when you need to store
		/// the result of the CustomVolume blending pass in a separate stack.
		/// </summary>
		/// <returns>A new <see cref="VolumeStack"/> instance with freshly loaded components.</returns>
		/// <seealso cref="VolumeStack"/>
		/// <seealso cref="Update(VolumeStack,Transform,LayerMask)"/>
		public VolumeStack CreateStack()
		{
			var stack = new VolumeStack();
			stack.Reload(baseComponentTypeArray);
			m_CreatedVolumeStacks.Add(stack);
			return stack;
		}

		/// <summary>
		/// Resets the main stack to be the default one.
		/// Call this function if you've assigned the main stack to something other than the default one.
		/// </summary>
		public void ResetMainStack()
		{
			stack = m_DefaultStack;
		}

		/// <summary>
		/// Destroy a CustomVolume Stack
		/// </summary>
		/// <param name="stack">CustomVolume Stack that needs to be destroyed.</param>
		public void DestroyStack(VolumeStack stack)
		{
			m_CreatedVolumeStacks.Remove(stack);
			stack.Dispose();
		}

		// This will be called only once at runtime and on domain reload / pipeline switch in the editor
		// as we need to keep track of any compatible component in the project
		internal void LoadBaseTypes(Type customProfile)
		{
			// Grab all the component types we can find that are compatible with current pipeline
			using (ListPool<Type>.Get(out var list))
			{
				foreach (var t in CoreUtils.GetAllTypesDerivedFrom<ScriptableVolumeComponent>())
				{
					if (t.IsAbstract)
						continue;

					var isSupported = SupportedOnScriptableProfileAttribute.IsTypeSupportedOnRenderPipeline(t, customProfile);

					if (isSupported)
						list.Add(t);
				}

				baseComponentTypeArray = list.ToArray();
			}
		}

		internal void InitializeVolumeComponents()
		{
			// Call custom static Init method if present
			var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			foreach (var type in baseComponentTypeArray)
			{
				var initMethod = type.GetMethod("Init", flags);
				if (initMethod != null)
				{
					initMethod.Invoke(null, null);
				}
			}
		}

		// Evaluate static default values for VolumeComponents, which is the baseline to reset the values to at the start of Update.
		internal void EvaluateVolumeDefaultState()
		{
			if (baseComponentTypeArray == null || baseComponentTypeArray.Length == 0)
				return;

			using var profilerScope = k_ProfilerMarkerEvaluateVolumeDefaultState.Auto();

			// TODO consider if the "component default values" array should be kept in memory separately. Creating the
			// instances is likely the slowest operation here, so doing that would mean it can only be done once in
			// Initialize() and the default state can be updated a lot quicker.

			// First, default-construct all VolumeComponents
			List<ScriptableVolumeComponent> componentsDefaultStateList = new();
			foreach (var type in baseComponentTypeArray)
			{
				componentsDefaultStateList.Add((ScriptableVolumeComponent)ScriptableObject.CreateInstance(type));
			}

			void ApplyDefaultProfile(ScriptableVolumeProfile profile)
			{
				if (profile == null)
					return;

				for (int i = 0; i < profile.components.Count; i++)
				{
					var profileComponent = profile.components[i];
					var defaultStateComponent = componentsDefaultStateList.FirstOrDefault(
						x => x.GetType() == profileComponent.GetType());

					if (defaultStateComponent != null && profileComponent.active)
					{
						// Ideally we would just call SetValue here. However, there are custom non-trivial
						// implementations of VolumeParameter.Interp() (such as DiffusionProfileList) that make it
						// necessary for us to call the it. This ensures the new DefaultProfile behavior works
						// consistently with the old HDRP implementation where the Default Profile was implemented as
						// a regular global volume inside the scene.
						profileComponent.Override(defaultStateComponent, 1.0f);
					}
				}
			}

			ApplyDefaultProfile(globalDefaultProfile);// Apply global default profile first
			ApplyDefaultProfile(qualityDefaultProfile);// Apply quality default profile second
			if (customDefaultProfiles != null)// Finally, apply custom default profiles in order
				foreach (var profile in customDefaultProfiles)
					ApplyDefaultProfile(profile);

			// Build the flat parametersDefaultState list for fast per-frame resets
			var parametersDefaultStateList = new List<ScriptableVolumeParameter>();
			foreach (var component in componentsDefaultStateList)
			{
				parametersDefaultStateList.AddRange(component.parameters);
			}

			m_ComponentsDefaultState = componentsDefaultStateList.ToArray();
			m_ParametersDefaultState = parametersDefaultStateList.ToArray();

			// All properties in stacks must be reset because the default state has changed
			foreach (var s in m_CreatedVolumeStacks)
			{
				s.requiresReset = true;
				s.requiresResetForAllProperties = true;
			}
		}

		/// <summary>
		/// Registers a new CustomVolume in the manager. Unity does this automatically when a new CustomVolume is
		/// enabled, or its layer changes, but you can use this function to force-register a CustomVolume
		/// that is currently disabled.
		/// </summary>
		/// <param name="volume">The volume to register.</param>
		/// <seealso cref="Unregister"/>
		public void Register(ScriptableVolume volume)
		{
			m_VolumeCollection.Register(volume, volume.gameObject.layer);
		}

		/// <summary>
		/// Unregisters a CustomVolume from the manager. Unity does this automatically when a CustomVolume is
		/// disabled or goes out of scope, but you can use this function to force-unregister a CustomVolume
		/// that you added manually while it was disabled.
		/// </summary>
		/// <param name="volume">The CustomVolume to unregister.</param>
		/// <seealso cref="Register"/>
		public void Unregister(ScriptableVolume volume)
		{
			m_VolumeCollection.Unregister(volume, volume.gameObject.layer);
		}

		/// <summary>
		/// Checks if a <see cref="ScriptableVolumeComponent"/> is active in a given LayerMask.
		/// </summary>
		/// <typeparam name="T">A type derived from <see cref="ScriptableVolumeComponent"/></typeparam>
		/// <param name="layerMask">The LayerMask to check against</param>
		/// <returns><c>true</c> if the component is active in the LayerMask, <c>false</c>
		/// otherwise.</returns>
		public bool IsComponentActiveInMask<T>(LayerMask layerMask)
			where T : ScriptableVolumeComponent
		{
			return m_VolumeCollection.IsComponentActiveInMask<T>(layerMask);
		}

		internal void SetLayerDirty(int layer)
		{
			m_VolumeCollection.SetLayerIndexDirty(layer);
		}

		internal void UpdateVolumeLayer(ScriptableVolume volume, int prevLayer, int newLayer)
		{
			m_VolumeCollection.ChangeLayer(volume, prevLayer, newLayer);
		}

		// Go through all listed components and lerp overridden values in the global state
		void OverrideData(VolumeStack stack, List<ScriptableVolumeComponent> components, float interpFactor)
		{
			var numComponents = components.Count;
			for (int i = 0; i < numComponents; i++)
			{
				var component = components[i];
				if (!component.active)
					continue;

				var state = stack.GetComponent(component.GetType());
				if (state != null)
				{
					component.Override(state, interpFactor);
				}
			}
		}

		// Faster version of OverrideData to force replace values in the global state.
		// NOTE: As an optimization, only the VolumeParameters with overrideState=true are reset. All other parameters
		// are assumed to be in their correct default state so no reset is necessary.
		internal void ReplaceData(VolumeStack stack)
		{
			using var profilerScope = k_ProfilerMarkerReplaceData.Auto();

			var stackParams = stack.parameters;
			bool resetAllParameters = stack.requiresResetForAllProperties;
			int count = stackParams.Length;
			Debug.Assert(count == m_ParametersDefaultState.Length);

			for (int i = 0; i < count; i++)
			{
				var stackParam = stackParams[i];
				if (stackParam.overrideState || resetAllParameters)// Only reset the parameters that have been overriden by a scene volume
				{
					stackParam.overrideState = false;
					stackParam.SetValue(m_ParametersDefaultState[i]);
				}
			}

			stack.requiresResetForAllProperties = false;
		}

		/// <summary>
		/// Checks component default state. This is only used in the editor to handle entering and exiting play mode
		/// because the instances created during playmode are automatically destroyed.
		/// </summary>
		[Conditional("UNITY_EDITOR")]
		public void CheckDefaultVolumeState()
		{
			if (m_ComponentsDefaultState == null || (m_ComponentsDefaultState.Length > 0 && m_ComponentsDefaultState[0] == null))
			{
				EvaluateVolumeDefaultState();
			}
		}

		/// <summary>
		/// Checks the state of a given stack. This is only used in the editor to handle entering and exiting play mode
		/// because the instances created during playmode are automatically destroyed.
		/// </summary>
		/// <param name="stack">The stack to check.</param>
		[Conditional("UNITY_EDITOR")]
		public void CheckStack(VolumeStack stack)
		{
			if (stack.components == null)
			{
				stack.Reload(baseComponentTypeArray);
				return;
			}

			foreach (var kvp in stack.components)
			{
				if (kvp.Key == null || kvp.Value == null)
				{
					stack.Reload(baseComponentTypeArray);
					return;
				}
			}
		}

		// Returns true if must execute Update() in full, and false if we can early exit.
		bool CheckUpdateRequired(VolumeStack stack)
		{
			if (m_VolumeCollection.count == 0)
			{
				if (stack.requiresReset)
				{
					// Update the stack one more time in case there was a volume that just ceased to exist. This ensures
					// the stack will return to default values correctly.
					stack.requiresReset = false;
					return true;
				}

				// There were no volumes last frame either, and stack has been returned to defaults, so no update is
				// needed and we can early exit from Update().
				return false;
			}
			stack.requiresReset = true;// Stack must be reset every frame whenever there are volumes present
			return true;
		}

		/// <summary>
		/// Updates the global state of the CustomVolume manager. Unity usually calls this once per Camera
		/// in the Update loop before rendering happens.
		/// </summary>
		/// <param name="trigger">A reference Transform to consider for positional CustomVolume blending
		/// </param>
		/// <param name="layerMask">The LayerMask that the CustomVolume manager uses to filter Volumes that it should consider
		/// for blending.</param>
		public void Update(Transform trigger, LayerMask layerMask)
		{
			Update(stack, trigger, layerMask);
		}

		/// <summary>
		/// Updates the CustomVolume manager and stores the result in a custom <see cref="VolumeStack"/>.
		/// </summary>
		/// <param name="stack">The stack to store the blending result into.</param>
		/// <param name="trigger">A reference Transform to consider for positional CustomVolume blending.
		/// </param>
		/// <param name="layerMask">The LayerMask that Unity uses to filter Volumes that it should consider
		/// for blending.</param>
		/// <seealso cref="VolumeStack"/>
		public void Update(VolumeStack stack, Transform trigger, LayerMask layerMask)
		{
			using var profilerScope = k_ProfilerMarkerUpdate.Auto();

			if (!isInitialized)
				return;

			Assert.IsNotNull(stack);

			CheckDefaultVolumeState();
			CheckStack(stack);

			if (!CheckUpdateRequired(stack))
				return;

			// Start by resetting the global state to default values.
			ReplaceData(stack);

			bool onlyGlobal = trigger == null;
			var triggerPos = onlyGlobal ? Vector3.zero : trigger.position;

			// Sort the cached volume list(s) for the given layer mask if needed and return it
			var volumes = GrabVolumes(layerMask);

			Camera camera = null;
			// Behavior should be fine even if camera is null
			if (!onlyGlobal)
				trigger.TryGetComponent<Camera>(out camera);

			// Traverse all volumes
			int numVolumes = volumes.Count;
			for (int i = 0; i < numVolumes; i++)
			{
				ScriptableVolume volume = volumes[i];
				if (volume == null)
					continue;

#if UNITY_EDITOR
				// Skip volumes that aren't in the scene currently displayed in the scene view
				if (!IsVolumeRenderedByCamera(volume, camera))
					continue;
#endif

				// Skip disabled volumes and volumes without any data or weight
				if (!volume.enabled || volume.profileRef == null || volume.weight <= 0f)
					continue;

				// Global volumes always have influence
				if (volume.isGlobal)
				{
					OverrideData(stack, volume.profileRef.components, Mathf.Clamp01(volume.weight));
					continue;
				}

				if (onlyGlobal)
					continue;

				// If volume isn't global and has no collider, skip it as it's useless
				var colliders = m_TempColliders;
				volume.GetComponents(colliders);
				if (colliders.Count == 0)
					continue;

				// Find closest distance to volume, 0 means it's inside it
				float closestDistanceSqr = float.PositiveInfinity;

				int numColliders = colliders.Count;
				for (int c = 0; c < numColliders; c++)
				{
					var collider = colliders[c];
					if (!collider.enabled)
						continue;

					var closestPoint = collider.ClosestPoint(triggerPos);
					var d = (closestPoint - triggerPos).sqrMagnitude;

					if (d < closestDistanceSqr)
						closestDistanceSqr = d;
				}

				colliders.Clear();
				float blendDistSqr = volume.blendDistance * volume.blendDistance;

				// CustomVolume has no influence, ignore it
				// Note: CustomVolume doesn't do anything when `closestDistanceSqr = blendDistSqr` but we
				//       can't use a >= comparison as blendDistSqr could be set to 0 in which case
				//       volume would have total influence
				if (closestDistanceSqr > blendDistSqr)
					continue;

				// CustomVolume has influence
				float interpFactor = 1f;

				if (blendDistSqr > 0f)
					interpFactor = 1f - (closestDistanceSqr / blendDistSqr);

				// No need to clamp01 the interpolation factor as it'll always be in [0;1[ range
				OverrideData(stack, volume.profileRef.components, interpFactor * Mathf.Clamp01(volume.weight));
			}
		}

		/// <summary>
		/// Get all volumes on a given layer mask sorted by influence.
		/// </summary>
		/// <param name="layerMask">The LayerMask that Unity uses to filter Volumes that it should consider.</param>
		/// <returns>An array of volume.</returns>
		public ScriptableVolume[] GetVolumes(LayerMask layerMask)
		{
			var volumes = GrabVolumes(layerMask);
			volumes.RemoveAll(v => v == null);
			return volumes.ToArray();
		}

		List<ScriptableVolume> GrabVolumes(LayerMask mask)
		{
			return m_VolumeCollection.GrabVolumes(mask);
		}

		static bool IsVolumeRenderedByCamera(ScriptableVolume volume, Camera camera)
		{
#if UNITY_2018_3_OR_NEWER && UNITY_EDITOR
			// GameObject for default global volume may not belong to any scene, following check prevents it from being culled
			if (!volume.gameObject.scene.IsValid())
				return true;
			// IsGameObjectRenderedByCamera does not behave correctly when camera is null so we have to catch it here.
			return camera == null ? true : UnityEditor.SceneManagement.StageUtility.IsGameObjectRenderedByCamera(volume.gameObject, camera);
#else
            return true;
#endif
		}
	}

	/// <summary>
	/// A scope in which a Camera filters a CustomVolume.
	/// </summary>
	[Obsolete("VolumeIsolationScope is deprecated, it does not have any effect anymore.")]
	public struct VolumeIsolationScope : IDisposable
	{
		/// <summary>
		/// Constructs a scope in which a Camera filters a CustomVolume.
		/// </summary>
		/// <param name="unused">Unused parameter.</param>
		public VolumeIsolationScope(bool unused) { }

		/// <summary>
		/// Stops the Camera from filtering a CustomVolume.
		/// </summary>
		void IDisposable.Dispose() { }
	}
}