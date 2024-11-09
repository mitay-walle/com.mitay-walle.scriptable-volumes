using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Plugins.VFX.Volumes
{
	/// <summary>
	/// Holds the state of a CustomVolume blending update. A global stack is
	/// available by default in <see cref="VolumeManager"/> but you can also create your own using
	/// <see cref="VolumeManager.CreateStack"/> if you need to update the manager with specific
	/// settings and store the results for later use.
	/// </summary>
	public sealed class VolumeStack : IDisposable
	{
		// Holds the state of _all_ component types you can possibly add on volumes
		public readonly Dictionary<Type, ScriptableVolumeComponent> components = new();

		// Flat list of every volume parameter for faster per-frame stack reset.
		internal ScriptableVolumeParameter[] parameters;

		// Flag indicating that some properties have received overrides, therefore they must be reset in the next update.
		internal bool requiresReset = true;

		// Flag indicating that default state has changed, therefore all properties in the stack must be reset in the next update.
		internal bool requiresResetForAllProperties = true;

		internal VolumeStack()
		{
		}

		internal void Clear()
		{
			foreach (var component in components)
				CoreUtils.Destroy(component.Value);

			components.Clear();

			parameters = null;
		}

		internal void Reload(Type[] componentTypes)
		{
			Clear();

			requiresReset = true;
			requiresResetForAllProperties = true;

			List<ScriptableVolumeParameter> parametersList = new();
			foreach (var type in componentTypes)
			{
				var component = (ScriptableVolumeComponent)ScriptableObject.CreateInstance(type);
				components.Add(type, component);
				parametersList.AddRange(component.parameters);
			}

			parameters = parametersList.ToArray();

			isValid = true;
		}

		/// <summary>
		/// Gets the current state of the <see cref="ScriptableVolumeComponent"/> of type <typeparamref name="T"/>
		/// in the stack.
		/// </summary>
		/// <typeparam name="T">A type of <see cref="ScriptableVolumeComponent"/>.</typeparam>
		/// <returns>The current state of the <see cref="ScriptableVolumeComponent"/> of type <typeparamref name="T"/>
		/// in the stack.</returns>
		public T GetComponent<T>()
			where T : ScriptableVolumeComponent
		{
			var comp = GetComponent(typeof(T));
			return (T)comp;
		}

		/// <summary>
		/// Gets the current state of the <see cref="ScriptableVolumeComponent"/> of the specified type in the
		/// stack.
		/// </summary>
		/// <param name="type">The type of <see cref="ScriptableVolumeComponent"/> to look for.</param>
		/// <returns>The current state of the <see cref="ScriptableVolumeComponent"/> of the specified type,
		/// or <c>null</c> if the type is invalid.</returns>
		public ScriptableVolumeComponent GetComponent(Type type)
		{
			components.TryGetValue(type, out var comp);
			return comp;
		}

		/// <summary>
		/// Cleans up the content of this stack. Once a <c>VolumeStack</c> is disposed, it shouldn't
		/// be used anymore.
		/// </summary>
		public void Dispose()
		{
			Clear();

			isValid = false;
		}

		/// <summary>
		/// Check if the stack is in valid state and can be used.
		/// </summary>
		public bool isValid { get; private set; }
	}

	internal class ScriptableVolumeCollection
	{
		// Max amount of layers available in Unity
		internal const int k_MaxLayerCount = 32;

		// Cached lists of all volumes (sorted by priority) by layer mask
		readonly Dictionary<int, List<ScriptableVolume>> m_SortedVolumes = new();

		// Holds all the registered volumes
		readonly List<ScriptableVolume> m_Volumes = new();

		// Keep track of sorting states for layer masks
		readonly Dictionary<int, bool> m_SortNeeded = new();

		public int count => m_Volumes.Count;

		public bool Register(ScriptableVolume volume, int layer)
		{
			if (volume == null)
				throw new ArgumentNullException(nameof(volume), "The volume to register is null");

			if (m_Volumes.Contains(volume))
				return false;

			m_Volumes.Add(volume);

			// Look for existing cached layer masks and add it there if needed
			foreach (var kvp in m_SortedVolumes)
			{
				// We add the volume to sorted lists only if the layer match and if it doesn't contain the volume already.
				if ((kvp.Key & (1 << layer)) != 0 && !kvp.Value.Contains(volume))
					kvp.Value.Add(volume);
			}

			SetLayerIndexDirty(layer);
			return true;
		}

		public bool Unregister(ScriptableVolume volume, int layer)
		{
			if (volume == null)
				throw new ArgumentNullException(nameof(volume), "The volume to unregister is null");

			m_Volumes.Remove(volume);

			foreach (var kvp in m_SortedVolumes)
			{
				// Skip layer masks this volume doesn't belong to
				if ((kvp.Key & (1 << layer)) == 0)
					continue;

				kvp.Value.Remove(volume);
			}

			SetLayerIndexDirty(layer);

			return true;
		}

		public bool ChangeLayer(ScriptableVolume volume, int previousLayerIndex, int currentLayerIndex)
		{
			if (volume == null)
				throw new ArgumentNullException(nameof(volume), "The volume to change layer is null");

			Assert.IsTrue(previousLayerIndex >= 0 && previousLayerIndex <= k_MaxLayerCount, "Invalid layer bit");
			Unregister(volume, previousLayerIndex);

			return Register(volume, currentLayerIndex);
		}

		// Stable insertion sort. Faster than List<T>.Sort() for our needs.
		internal static void SortByPriority(List<ScriptableVolume> volumes)
		{
			for (int i = 1; i < volumes.Count; i++)
			{
				var temp = volumes[i];
				int j = i - 1;

				// Sort order is ascending
				while (j >= 0 && volumes[j].priority > temp.priority)
				{
					volumes[j + 1] = volumes[j];
					j--;
				}

				volumes[j + 1] = temp;
			}
		}

		public List<ScriptableVolume> GrabVolumes(LayerMask mask)
		{
			List<ScriptableVolume> list;

			if (!m_SortedVolumes.TryGetValue(mask, out list))
			{
				// New layer mask detected, create a new list and cache all the volumes that belong
				// to this mask in it
				list = new List<ScriptableVolume>();

				var numVolumes = m_Volumes.Count;
				for (int i = 0; i < numVolumes; i++)
				{
					var volume = m_Volumes[i];
					if ((mask & (1 << volume.gameObject.layer)) == 0)
						continue;

					list.Add(volume);
					m_SortNeeded[mask] = true;
				}

				m_SortedVolumes.Add(mask, list);
			}

			// Check sorting state
			if (m_SortNeeded.TryGetValue(mask, out var sortNeeded) && sortNeeded)
			{
				m_SortNeeded[mask] = false;
				SortByPriority(list);
			}

			return list;
		}

		public void SetLayerIndexDirty(int layerIndex)
		{
			Assert.IsTrue(layerIndex >= 0 && layerIndex <= k_MaxLayerCount, "Invalid layer bit");

			foreach (var kvp in m_SortedVolumes)
			{
				var mask = kvp.Key;

				if ((mask & (1 << layerIndex)) != 0)
					m_SortNeeded[mask] = true;
			}
		}

		public bool IsComponentActiveInMask<T>(LayerMask layerMask)
			where T : ScriptableVolumeComponent
		{
			int mask = layerMask.value;

			foreach (var kvp in m_SortedVolumes)
			{
				if (kvp.Key != mask)
					continue;

				foreach (var volume in kvp.Value)
				{
					if (!volume.enabled || volume.profileRef == null)
						continue;

					if (volume.profileRef.TryGet(out T component) && component.active)
						return true;
				}
			}

			return false;
		}
	}
}