using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Plugins.VFX.Volumes
{
	public static class CoreUtils
	{
		public static int GetTextureHash(Texture texture)
		{
			int hash = texture.GetHashCode();

			unchecked
			{
#if UNITY_EDITOR
				hash = 23 * hash + texture.imageContentsHash.GetHashCode();
#endif
				hash = 23 * hash + texture.GetInstanceID().GetHashCode();
				hash = 23 * hash + texture.graphicsFormat.GetHashCode();
				hash = 23 * hash + texture.wrapMode.GetHashCode();
				hash = 23 * hash + texture.width.GetHashCode();
				hash = 23 * hash + texture.height.GetHashCode();
				hash = 23 * hash + texture.filterMode.GetHashCode();
				hash = 23 * hash + texture.anisoLevel.GetHashCode();
				hash = 23 * hash + texture.mipmapCount.GetHashCode();
				hash = 23 * hash + texture.updateCount.GetHashCode();
			}

			return hash;
		}

		/// <summary>
		/// Destroys a UnityObject safely.
		/// </summary>
		/// <param name="obj">Object to be destroyed.</param>
		public static void Destroy(Object obj)
		{
			if (obj != null)
			{
#if UNITY_EDITOR
				if (Application.isPlaying && !UnityEditor.EditorApplication.isPaused)
					Object.Destroy(obj);
				else
					Object.DestroyImmediate(obj);
#else
                Object.Destroy(obj);
#endif
			}
		}

		/// <summary>
		/// Returns a list of types that inherit from the provided type.
		/// </summary>
		/// <typeparam name="T">Parent Type</typeparam>
		/// <returns>A list of types that inherit from the provided type.</returns>
		public static IEnumerable<Type> GetAllTypesDerivedFrom<T>()
		{
#if UNITY_EDITOR && UNITY_2019_2_OR_NEWER
			return UnityEditor.TypeCache.GetTypesDerivedFrom<T>();
#else
            return GetAllAssemblyTypes().Where(t => t.IsSubclassOf(typeof(T)));
#endif
		}
	}
}