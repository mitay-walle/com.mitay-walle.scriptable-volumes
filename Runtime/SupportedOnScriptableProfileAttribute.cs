using System;
using System.Linq;
using Sirenix.Utilities;
using UnityEngine;

namespace Plugins.VFX.Volumes
{
	/// <summary>
	///   <para>Set which render pipelines make a class active.</para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class SupportedOnScriptableProfileAttribute : Attribute
	{
		private static readonly Lazy<Type[]> k_DefaultRenderPipelineAsset = new Lazy<Type[]>((Func<Type[]>)(() => new Type[1]
		{
			typeof(ScriptableVolumeProfile)
		}));

		/// <summary>
		///   <para>The Render Pipeline Assets that support the attribute.</para>
		/// </summary>
		public Type[] renderPipelineTypes { get; }

		public SupportedOnScriptableProfileAttribute(Type renderPipeline)
			: this(new Type[1] { renderPipeline })
		{
		}

		public SupportedOnScriptableProfileAttribute(params Type[] profiles)
		{
			if (profiles == null)
			{
				Debug.LogError((object)"The SupportedOnCustomProfileAttribute parameters cannot be null.");
			}
			else
			{
				for (int index = 0; index < profiles.Length; ++index)
				{
					Type c = profiles[index];
					if (!(c != (Type)null) || !typeof(ScriptableVolumeProfile).IsAssignableFrom(c))
					{
						Debug.LogError((object)("The SupportedOnCustomProfileAttribute Attribute targets an invalid CustomProfile. One of the types cannot be assigned from RenderPipelineAsset: [" + string.Join(", ", profiles.Select(t => t.Name).ToArray())) + "].");
						return;
					}
				}
				renderPipelineTypes = profiles.Length == 0 ? k_DefaultRenderPipelineAsset.Value : profiles;
			}
		}

		/// <summary>
		///   <para>Use SupportedOnCustomProfileAttribute.GetSupportedMode to find out whether a RenderPipelineAsset supports the attribute.</para>
		/// </summary>
		/// <param name="renderPipelineAssetType">The RenderPipelineAsset you want to check.</param>
		/// <returns>
		///   <para>Whether the RenderPipelineAsset or its base RenderPipelineAsset supports the attribute.</para>
		/// </returns>
		public SupportedMode GetSupportedMode(
			Type renderPipelineAssetType)
		{
			return GetSupportedMode(renderPipelineTypes, renderPipelineAssetType);
		}

		internal static SupportedMode GetSupportedMode(
			Type[] renderPipelineTypes,
			Type renderPipelineAssetType)
		{
			if (renderPipelineTypes == null)
				throw new ArgumentNullException("Parameter renderPipelineTypes cannot be null.");
			if (renderPipelineAssetType == (Type)null)
				return SupportedMode.Unsupported;
			for (int index = 0; index < renderPipelineTypes.Length; ++index)
			{
				if (renderPipelineTypes[index] == renderPipelineAssetType)
					return SupportedMode.Supported;
			}
			for (int index = 0; index < renderPipelineTypes.Length; ++index)
			{
				if (renderPipelineTypes[index].IsAssignableFrom(renderPipelineAssetType))
					return SupportedMode.SupportedByBaseClass;
			}
			return SupportedMode.Unsupported;
		}

		/// <summary>
		///   <para>Use this method to determine whether a type has the SupportedOnCustomProfileAttribute attribute and determine whether a RenderPipelineAsset type supports that attribute.</para>
		/// </summary>
		/// <param name="type">The type you want to check.</param>
		/// <param name="renderPipelineAssetType">RenderPipelineAsset type.</param>
		/// <returns>
		///   <para>Returns true if the provided type is supported on the provided RenderPipelineAsset type.</para>
		/// </returns>
		public static bool IsTypeSupportedOnRenderPipeline(Type type, Type renderPipelineAssetType)
		{
			SupportedOnScriptableProfileAttribute scriptableAttribute = type.GetCustomAttribute<SupportedOnScriptableProfileAttribute>();
			return scriptableAttribute == null || (uint)scriptableAttribute.GetSupportedMode(renderPipelineAssetType) > 0U;
		}

		/// <summary>
		///   <para>Whether the RenderPipelineAsset or its base RenderPipelineAsset supports the attribute.</para>
		/// </summary>
		public enum SupportedMode
		{
			/// <summary>
			///   <para>The RenderPipelineAsset doesn't support the attribute.</para>
			/// </summary>
			Unsupported,
			/// <summary>
			///   <para>The RenderPipelineAsset supports the attribute.</para>
			/// </summary>
			Supported,
			/// <summary>
			///   <para>The base class of the RenderPipelineAsset supports the attribute.</para>
			/// </summary>
			SupportedByBaseClass,
		}
	}
}