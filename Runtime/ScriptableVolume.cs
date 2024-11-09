using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Plugins.VFX.Volumes
{
	[ExecuteAlways]
	public class ScriptableVolume : MonoBehaviour, IVolume
	{
		[FormerlySerializedAs("m_IsGlobal")]
		[SerializeField]
		private bool _isGlobal = true;

		/// <summary>
		/// Specifies whether to apply the Volume to the entire Scene or not.
		/// </summary>
		public bool isGlobal
		{
			get => _isGlobal;
			set => _isGlobal = value;
		}

		/// <summary>
		/// A value which determines which Volume is being used when Volumes have an equal amount of influence on the Scene. Volumes with a higher priority will override lower ones.
		/// </summary>
		[Delayed]
		public float priority = 0f;

		/// <summary>
		/// The outer distance to start blending from. A value of 0 means no blending and Unity applies
		/// the Volume overrides immediately upon entry.
		/// </summary>
		public float blendDistance = 0f;

		/// <summary>
		/// The total weight of this volume in the Scene. 0 means no effect and 1 means full effect.
		/// </summary>
		[Range(0f, 1f)]
		public float weight = 1f;

		/// <summary>
		/// The shared Profile that this Volume uses.
		/// Modifying <c>sharedProfile</c> changes every Volumes that uses this Profile and also changes
		/// the Profile settings stored in the Project.
		/// </summary>
		/// <remarks>
		/// You should not modify Profiles that <c>sharedProfile</c> returns. If you want
		/// to modify the Profile of a Volume, use <see cref="profile"/> instead.
		/// </remarks>
		/// <seealso cref="profile"/>
		[InlineEditor] public ScriptableVolumeProfile sharedProfile = null;

		/// <summary>
		/// Gets the first instantiated <see cref="ScriptableVolumeProfile"/> assigned to the Volume.
		/// Modifying <c>profile</c> changes the Profile for this Volume only. If another Volume
		/// uses the same Profile, this clones the shared Profile and starts using it from now on.
		/// </summary>
		/// <remarks>
		/// This property automatically instantiates the Profile and make it unique to this Volume
		/// so you can safely edit it via scripting at runtime without changing the original Asset
		/// in the Project.
		/// Note that if you pass your own Profile, you must destroy it when you finish using it.
		/// </remarks>
		/// <seealso cref="sharedProfile"/>
		public ScriptableVolumeProfile profile
		{
			get
			{
				if (m_InternalProfile == null)
				{
					m_InternalProfile = ScriptableObject.CreateInstance<ScriptableVolumeProfile>();

					if (sharedProfile != null)
					{
						m_InternalProfile.name = sharedProfile.name;

						foreach (var item in sharedProfile.components)
						{
							var itemCopy = Instantiate(item);
							m_InternalProfile.components.Add(itemCopy);
						}
					}
				}

				return m_InternalProfile;
			}
			set => m_InternalProfile = value;
		}

		internal List<Collider> m_Colliders = new List<Collider>();

		/// <summary>
		/// The colliders of the volume if <see cref="isGlobal"/> is false
		/// </summary>
		public List<Collider> colliders => m_Colliders;

		internal ScriptableVolumeProfile profileRef => m_InternalProfile == null ? sharedProfile : m_InternalProfile;

		/// <summary>
		/// Checks if the Volume has an instantiated Profile or if it uses a shared Profile.
		/// </summary>
		/// <returns><c>true</c> if the profile has been instantiated.</returns>
		/// <seealso cref="profile"/>
		/// <seealso cref="sharedProfile"/>
		public bool HasInstantiatedProfile() => m_InternalProfile != null;

		// Needed for state tracking (see the comments in Update)
		private int m_PreviousLayer;
		private float m_PreviousPriority;
		private ScriptableVolumeProfile m_InternalProfile;

		private void OnEnable()
		{
			m_PreviousLayer = gameObject.layer;
			ScriptableVolumeManager.instance.Register(this);
			GetComponents(m_Colliders);
		}

		private void OnDisable()
		{
			ScriptableVolumeManager.instance.Unregister(this);
		}

		private void Update()
		{
			UpdateLayer();
			UpdatePriority();

#if UNITY_EDITOR
			// In the editor, we refresh the list of colliders at every frame because it's frequent to add/remove them
			GetComponents(m_Colliders);
#endif
			if (_isGlobal && sharedProfile)
			{
				VolumeManager.instance.ResetMainStack();
				sharedProfile.Apply(ScriptableVolumeManager.instance.stack);
				ScriptableVolumeManager.instance.Update(Camera.main?.transform,-1);
			}
		}

		internal void UpdateLayer()
		{
			// Unfortunately we need to track the current layer to update the volume manager in
			// real-time as the user could change it at any time in the editor or at runtime.
			// Because no event is raised when the layer changes, we have to track it on every
			// frame :/

			int layer = gameObject.layer;
			if (layer == m_PreviousLayer)
				return;

			ScriptableVolumeManager.instance.UpdateVolumeLayer(this, m_PreviousLayer, layer);
			m_PreviousLayer = layer;
		}

		internal void UpdatePriority()
		{
			if (!(Math.Abs(priority - m_PreviousPriority) > Mathf.Epsilon))
				return;

			// Same for priority. We could use a property instead, but it doesn't play nice with the
			// serialization system. Using a custom Attribute/PropertyDrawer for a property is
			// possible but it doesn't work with Undo/Redo in the editor, which makes it useless for
			// our case.
			ScriptableVolumeManager.instance.SetLayerDirty(gameObject.layer);
			m_PreviousPriority = priority;
		}

		private void OnValidate()
		{
			blendDistance = Mathf.Max(blendDistance, 0f);
		}
	}
}