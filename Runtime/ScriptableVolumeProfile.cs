using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Plugins.VFX.Volumes
{
	[Icon("Packages/com.unity.render-pipelines.core/Editor/Icons/Processed/VolumeProfile Icon.asset")]
	public abstract class ScriptableVolumeProfileT<T> : ScriptableVolumeProfile where T : ScriptableVolumeComponent
	{
		[Button] private void Add()
		{
#if UNITY_EDITOR
			var types = TypeCache.GetTypesDerivedFrom<T>().ToList();
			if (!typeof(T).IsAbstract)
			{
				types.Add(typeof(T));
			}
			var menu = new GenericMenu();
			foreach (Type type in types)
			{
				if (!type.IsAbstract && !components.Exists(c => c.GetType() == type))
				{
					menu.AddItem(new GUIContent(type.Name), false, () => { Add(type); });
				}
			}
			menu.ShowAsContext();
#endif
		}
	}

	[Icon("Packages/com.unity.render-pipelines.core/Editor/Icons/Processed/VolumeProfile Icon.asset")]
	public abstract class ScriptableVolumeProfile : ScriptableObject
	{
		/// <summary>
		/// A list of every setting that this Volume Profile stores.
		/// </summary>
		[InlineEditor(InlineEditorObjectFieldModes.CompletelyHidden), ListDrawerSettings(IsReadOnly = true, ShowFoldout = false, ShowIndexLabels = true)]
		public List<ScriptableVolumeComponent> components = new List<ScriptableVolumeComponent>();

		/// <summary>
		/// **Note**: For Internal Use Only<br/>
		/// A dirty check used to redraw the profile inspector when something has changed. This is
		/// currently only used in the editor.
		/// </summary>
		[NonSerialized]
		public bool isDirty = true;// Editor only, doesn't have any use outside of it

		void OnEnable()
		{
			// Make sure every setting is valid. If a profile holds a script that doesn't exist
			// anymore, nuke it to keep the volume clean. Note that if you delete a script that is
			// currently in use in a volume you'll still get a one-time error in the console, it's
			// harmless and happens because Unity does a redraw of the editor (and thus the current
			// frame) before the recompilation step.
			components.RemoveAll(x => x == null);
		}

		// The lifetime of ScriptableObjects is different from MonoBehaviours. When the last reference to a
		// VolumeProfile goes out of scope (e.g. when a scene containing Volume components is unloaded), Unity will call
		// OnDisable() on the VolumeProfile. We need to release the internal resources in this case to avoid leaks.
		internal void OnDisable()
		{
			if (components == null)
				return;

			for (int i = 0; i < components.Count; i++)
			{
				if (components[i] != null)
					components[i].Release();
			}
		}

		/// <summary>
		/// Resets the dirty state of the Volume Profile. Unity uses this to force-refresh and redraw the
		/// Volume Profile editor when you modify the Asset via script instead of the Inspector.
		/// </summary>
		public void Reset()
		{
			isDirty = true;
		}

		/// <summary>
		/// Adds a <see cref="ScriptableVolumeComponent"/> to this Volume Profile.
		/// </summary>
		/// <remarks>
		/// You can only have a single component of the same type per Volume Profile.
		/// </remarks>
		/// <typeparam name="T">A type of <see cref="ScriptableVolumeComponent"/>.</typeparam>
		/// <param name="overrides">Specifies whether Unity should automatically override all the settings when
		/// you add a <see cref="ScriptableVolumeComponent"/> to the Volume Profile.</param>
		/// <returns>The instance for the given type that you added to the Volume Profile</returns>
		/// <seealso cref="Add"/>
		public T Add<T>(bool overrides = false)
			where T : ScriptableVolumeComponent
		{
			return (T)Add(typeof(T), overrides);
		}

		public abstract void Apply(VolumeStack stack);

		/// <summary>
		/// Adds a <see cref="ScriptableVolumeComponent"/> to this Volume Profile.
		/// </summary>
		/// <remarks>
		/// You can only have a single component of the same type per Volume Profile.
		/// </remarks>
		/// <param name="type">A type that inherits from <see cref="ScriptableVolumeComponent"/>.</param>
		/// <param name="overrides">Specifies whether Unity should automatically override all the settings when
		/// you add a <see cref="ScriptableVolumeComponent"/> to the Volume Profile.</param>
		/// <returns>The instance created for the given type that has been added to the profile</returns>
		/// <seealso cref="Add{T}"/>
		public ScriptableVolumeComponent Add(Type type, bool overrides = false)
		{
			if (Has(type))
				throw new InvalidOperationException("Component already exists in the volume");

			var component = (ScriptableVolumeComponent)CreateInstance(type);
#if UNITY_EDITOR
			//component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
			component.name = type.Name;
#endif
			component.SetAllOverridesTo(overrides);
			components.Add(component);
			isDirty = true;

			#if UNITY_EDITOR
			AssetDatabase.AddObjectToAsset(component, this);
			AssetDatabase.SaveAssets();
			AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this));
			#endif
			return component;
		}

		/// <summary>
		/// Removes a <see cref="ScriptableVolumeComponent"/> from this Volume Profile.
		/// </summary>
		/// <remarks>
		/// This method does nothing if the type does not exist in the Volume Profile.
		/// </remarks>
		/// <typeparam name="T">A type of <see cref="ScriptableVolumeComponent"/>.</typeparam>
		/// <seealso cref="Remove"/>
		public void Remove<T>()
			where T : ScriptableVolumeComponent
		{
			Remove(typeof(T));
		}

		/// <summary>
		/// Removes a <see cref="ScriptableVolumeComponent"/> from this Volume Profile.
		/// </summary>
		/// <remarks>
		/// This method does nothing if the type does not exist in the Volume Profile.
		/// </remarks>
		/// <param name="type">A type that inherits from <see cref="ScriptableVolumeComponent"/>.</param>
		/// <seealso cref="Remove{T}"/>
		public void Remove(Type type)
		{
			int toRemove = -1;

			for (int i = 0; i < components.Count; i++)
			{
				if (components[i].GetType() == type)
				{
					toRemove = i;
					break;
				}
			}

			if (toRemove >= 0)
			{
				ScriptableVolumeComponent comp = components[toRemove];
				components.RemoveAt(toRemove);
#if UNITY_EDITOR
				AssetDatabase.RemoveObjectFromAsset(comp);
				AssetDatabase.SaveAssets();
				AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this));
				AssetDatabase.Refresh();
#endif
				isDirty = true;
			}
		}

		/// <summary>
		/// Checks if this Volume Profile contains the <see cref="ScriptableVolumeComponent"/> you pass in.
		/// </summary>
		/// <typeparam name="T">A type of <see cref="ScriptableVolumeComponent"/>.</typeparam>
		/// <returns><c>true</c> if the <see cref="ScriptableVolumeComponent"/> exists in the Volume Profile,
		/// <c>false</c> otherwise.</returns>
		/// <seealso cref="Has"/>
		/// <seealso cref="HasSubclassOf"/>
		public bool Has<T>()
			where T : ScriptableVolumeComponent
		{
			return Has(typeof(T));
		}

		/// <summary>
		/// Checks if this Volume Profile contains the <see cref="ScriptableVolumeComponent"/> you pass in.
		/// </summary>
		/// <param name="type">A type that inherits from <see cref="ScriptableVolumeComponent"/>.</param>
		/// <returns><c>true</c> if the <see cref="ScriptableVolumeComponent"/> exists in the Volume Profile,
		/// <c>false</c> otherwise.</returns>
		/// <seealso cref="Has{T}"/>
		/// <seealso cref="HasSubclassOf"/>
		public bool Has(Type type)
		{
			foreach (var component in components)
			{
				if (component.GetType() == type)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Checks if this Volume Profile contains the <see cref="ScriptableVolumeComponent"/>, which is a subclass of <paramref name="type"/>,
		/// that you pass in.
		/// </summary>
		/// <param name="type">A type that inherits from <see cref="ScriptableVolumeComponent"/>.</param>
		/// <returns><c>true</c> if the <see cref="ScriptableVolumeComponent"/> exists in the Volume Profile,
		/// <c>false</c> otherwise.</returns>
		/// <seealso cref="Has"/>
		/// <seealso cref="Has{T}"/>
		public bool HasSubclassOf(Type type)
		{
			foreach (var component in components)
			{
				if (component.GetType().IsSubclassOf(type))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Gets the <see cref="ScriptableVolumeComponent"/> of the specified type, if it exists.
		/// </summary>
		/// <typeparam name="T">A type of <see cref="ScriptableVolumeComponent"/>.</typeparam>
		/// <param name="component">The output argument that contains the <see cref="ScriptableVolumeComponent"/>
		/// or <c>null</c>.</param>
		/// <returns><c>true</c> if the <see cref="ScriptableVolumeComponent"/> is in the Volume Profile,
		/// <c>false</c> otherwise.</returns>
		/// <seealso cref="TryGet{T}(Type, out T)"/>
		/// <seealso cref="TryGetSubclassOf{T}"/>
		/// <seealso cref="TryGetAllSubclassOf{T}"/>
		public bool TryGet<T>(out T component)
			where T : ScriptableVolumeComponent
		{
			return TryGet(typeof(T), out component);
		}

		/// <summary>
		/// Gets the <see cref="ScriptableVolumeComponent"/> of the specified type, if it exists.
		/// </summary>
		/// <typeparam name="T">A type of <see cref="ScriptableVolumeComponent"/></typeparam>
		/// <param name="type">A type that inherits from <see cref="ScriptableVolumeComponent"/>.</param>
		/// <param name="component">The output argument that contains the <see cref="ScriptableVolumeComponent"/>
		/// or <c>null</c>.</param>
		/// <returns><c>true</c> if the <see cref="ScriptableVolumeComponent"/> is in the Volume Profile,
		/// <c>false</c> otherwise.</returns>
		/// <seealso cref="TryGet{T}(out T)"/>
		/// <seealso cref="TryGetSubclassOf{T}"/>
		/// <seealso cref="TryGetAllSubclassOf{T}"/>
		public bool TryGet<T>(Type type, out T component)
			where T : ScriptableVolumeComponent
		{
			component = null;

			foreach (var comp in components)
			{
				if (comp.GetType() == type)
				{
					component = (T)comp;
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Gets the <see cref="ScriptableVolumeComponent"/>, which is a subclass of <paramref name="type"/>, if
		/// it exists.
		/// </summary>
		/// <typeparam name="T">A type of <see cref="ScriptableVolumeComponent"/>.</typeparam>
		/// <param name="type">A type that inherits from <see cref="ScriptableVolumeComponent"/>.</param>
		/// <param name="component">The output argument that contains the <see cref="ScriptableVolumeComponent"/>
		/// or <c>null</c>.</param>
		/// <returns><c>true</c> if the <see cref="ScriptableVolumeComponent"/> is in the Volume Profile,
		/// <c>false</c> otherwise.</returns>
		/// <seealso cref="TryGet{T}(Type, out T)"/>
		/// <seealso cref="TryGet{T}(out T)"/>
		/// <seealso cref="TryGetAllSubclassOf{T}"/>
		public bool TryGetSubclassOf<T>(Type type, out T component)
			where T : ScriptableVolumeComponent
		{
			component = null;

			foreach (var comp in components)
			{
				if (comp.GetType().IsSubclassOf(type))
				{
					component = (T)comp;
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Gets all the <see cref="ScriptableVolumeComponent"/> that are subclasses of the specified type,
		/// if there are any.
		/// </summary>
		/// <typeparam name="T">A type of <see cref="ScriptableVolumeComponent"/>.</typeparam>
		/// <param name="type">A type that inherits from <see cref="ScriptableVolumeComponent"/>.</param>
		/// <param name="result">The output list that contains all the <see cref="ScriptableVolumeComponent"/>
		/// if any. Note that Unity does not clear this list.</param>
		/// <returns><c>true</c> if any <see cref="ScriptableVolumeComponent"/> have been found in the profile,
		/// <c>false</c> otherwise.</returns>
		/// <seealso cref="TryGet{T}(Type, out T)"/>
		/// <seealso cref="TryGet{T}(out T)"/>
		/// <seealso cref="TryGetSubclassOf{T}"/>
		public bool TryGetAllSubclassOf<T>(Type type, List<T> result)
			where T : ScriptableVolumeComponent
		{
			Assert.IsNotNull(components);
			int count = result.Count;

			foreach (var comp in components)
			{
				if (comp.GetType().IsSubclassOf(type))
					result.Add((T)comp);
			}

			return count != result.Count;
		}

		/// <summary>
		/// A custom hashing function that Unity uses to compare the state of parameters.
		/// </summary>
		/// <returns>A computed hash code for the current instance.</returns>
		public override int GetHashCode()
		{
			unchecked
			{
				int hash = 17;

				for (int i = 0; i < components.Count; i++)
					hash = hash * 23 + components[i].GetHashCode();

				return hash;
			}
		}

		internal int GetComponentListHashCode()
		{
			unchecked
			{
				int hash = 17;

				for (int i = 0; i < components.Count; i++)
					hash = hash * 23 + components[i].GetType().GetHashCode();

				return hash;
			}
		}

		/// <summary>
		/// Removes any components that were destroyed externally from the iternal list of components
		/// </summary>
		internal void Sanitize()
		{
			for (int i = components.Count - 1; i >= 0; i--)
				if (components[i] == null)
					components.RemoveAt(i);
		}

#if UNITY_EDITOR
		void OnValidate()
		{
			// Delay the callback because when undoing the deletion of a CustomVolumeComponent from a profile,
			// it's possible CustomVolumeComponent.OnEnable() has not yet been called, resulting in a crash when trying to
			// update the default state.
			EditorApplication.delayCall += () =>
			{
				if (ScriptableVolumeManager.instance.isInitialized)
					ScriptableVolumeManager.instance.OnVolumeProfileChanged(this);
			};
		}
#endif
	}
}