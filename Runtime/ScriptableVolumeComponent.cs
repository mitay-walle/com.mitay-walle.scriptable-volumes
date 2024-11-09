using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Plugins.VFX.Volumes
{
	/// <summary>
	/// The base class for all the components that can be part of a <see cref="VolumeProfile"/>.
	/// The Volume framework automatically handles and interpolates any <see cref="ScriptableVolumeParameter"/> members found in this class.
	/// </summary>
	/// <example>
	/// <code>
	/// using UnityEngine.Rendering;
	///
	/// [Serializable, CustomVolumeComponentMenuForRenderPipeline("Custom/Example Component")]
	/// public class ExampleComponent : CustomVolumeComponent
	/// {
	///     public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
	/// }
	/// </code>
	/// </example>
	[Serializable, EnableIf("@$value.active")]
	public abstract class ScriptableVolumeComponent : ScriptableObject
	{
		public abstract void Apply(VolumeStack stack);

		[Button, EnableGUI]
		protected void Remove()
		{
			#if UNITY_EDITOR
			Object main = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(this));
			if (main is ScriptableVolumeProfile profile)
			{
				profile.Remove(GetType());
			}

  #endif
		}

		/// <summary>
		/// Local attribute for CustomVolumeComponent fields only.
		/// It handles relative indentation of a property for inspector.
		/// </summary>
		public sealed class Indent : PropertyAttribute
		{
			/// <summary> Relative indent amount registered in this attribute </summary>
			public readonly int relativeAmount;

			/// <summary> Constructor </summary>
			/// <param name="relativeAmount">Relative indent change to use</param>
			public Indent(int relativeAmount = 1)
				=> this.relativeAmount = relativeAmount;
		}

		/// <summary>
		/// The active state of the set of parameters defined in this class. You can use this to
		/// quickly turn on or off all the overrides at once.
		/// </summary>
		[EnableGUI, Title("@this.GetType().Name")]
		public bool active = true;

		/// <summary>
		/// The name displayed in the component header. If you do not set a name, Unity generates one from
		/// the class name automatically.
		/// </summary>
		public string displayName { get; protected set; } = "";

		/// <summary>
		/// The backing storage of <see cref="parameters"/>. Use this for performance-critical work.
		/// </summary>
		internal readonly List<ScriptableVolumeParameter> parameterList = new();

		ReadOnlyCollection<ScriptableVolumeParameter> m_ParameterReadOnlyCollection;
		/// <summary>
		/// A read-only collection of all the <see cref="ScriptableVolumeParameter"/>s defined in this class.
		/// </summary>
		public ReadOnlyCollection<ScriptableVolumeParameter> parameters
		{
			get
			{
				if (m_ParameterReadOnlyCollection == null)
					m_ParameterReadOnlyCollection = parameterList.AsReadOnly();
				return m_ParameterReadOnlyCollection;
			}
		}

		/// <summary>
		/// Extracts all the <see cref="ScriptableVolumeParameter"/>s defined in this class and nested classes.
		/// </summary>
		/// <param name="o">The object to find the parameters</param>
		/// <param name="parameters">The list filled with the parameters.</param>
		/// <param name="filter">If you want to filter the parameters</param>
		internal static void FindParameters(object o, List<ScriptableVolumeParameter> parameters, Func<FieldInfo, bool> filter = null)
		{
			if (o == null)
				return;

			var fields = o.GetType()
				.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.OrderBy(t => t.MetadataToken);// Guaranteed order

			foreach (var field in fields)
			{
				if (field.FieldType.IsSubclassOf(typeof(ScriptableVolumeParameter)))
				{
					if (filter?.Invoke(field) ?? true)
					{
						ScriptableVolumeParameter scriptableVolumeParameter = (ScriptableVolumeParameter)field.GetValue(o);
						parameters.Add(scriptableVolumeParameter);
					}
				}
				else if (!field.FieldType.IsArray && field.FieldType.IsClass)
					FindParameters(field.GetValue(o), parameters, filter);
			}
		}

		/// <summary>
		/// Unity calls this method when it loads the class.
		/// </summary>
		/// <remarks>
		/// If you want to override this method, you must call <c>base.OnEnable()</c>.
		/// </remarks>
		protected virtual void OnEnable()
		{
			// Automatically grab all fields of type VolumeParameter for this instance
			parameterList.Clear();
			FindParameters(this, parameterList);

			foreach (var parameter in parameterList)
			{
				if (parameter != null)
					parameter.OnEnable();
				else
					Debug.LogWarning("Volume Component " + GetType().Name + " contains a null parameter; please make sure all parameters are initialized to a default value. Until this is fixed the null parameters will not be considered by the system.");
			}
		}

		/// <summary>
		/// Unity calls this method when the object goes out of scope.
		/// </summary>
		protected virtual void OnDisable()
		{
			foreach (var parameter in parameterList)
			{
				if (parameter != null)
					parameter.OnDisable();
			}
		}

		/// <summary>
		/// Interpolates a <see cref="ScriptableVolumeComponent"/> with this component by an interpolation
		/// factor and puts the result back into the given <see cref="ScriptableVolumeComponent"/>.
		/// </summary>
		/// <remarks>
		/// You can override this method to do your own blending. Either loop through the
		/// <see cref="parameters"/> list or reference direct fields. You should only use
		/// <see cref="ScriptableVolumeParameter.SetValue"/> to set parameter values and not assign
		/// directly to the state object. you should also manually check
		/// <see cref="ScriptableVolumeParameter.overrideState"/> before you set any values.
		/// </remarks>
		/// <param name="state">The internal component to interpolate from. You must store
		/// the result of the interpolation in this same component.</param>
		/// <param name="interpFactor">The interpolation factor in range [0,1].</param>
		/// <example>
		/// <para> Below is the default implementation for blending:</para>
		/// <code>
		/// public virtual void Override(CustomVolumeComponent state, float interpFactor)
		/// {
		///     int count = parameters.Count;
		///
		///     for (int i = 0; i &lt; count; i++)
		///     {
		///         var stateParam = state.parameters[i];
		///         var toParam = parameters[i];
		///
		///         if (toParam.overrideState)
		///         {
		///             // Keep track of the override state to ensure that state will be reset on next frame (and for debugging purpose)
		///             stateParam.overrideState = toParam.overrideState;
		///             stateParam.Interp(stateParam, toParam, interpFactor);
		///         }
		///     }
		/// }
		/// </code>
		/// </example>
		public virtual void Override(ScriptableVolumeComponent state, float interpFactor)
		{
			int count = parameterList.Count;

			for (int i = 0; i < count; i++)
			{
				var stateParam = state.parameterList[i];
				var toParam = parameterList[i];

				if (toParam.overrideState)
				{
					// Keep track of the override state to ensure that state will be reset on next frame (and for debugging purpose)
					stateParam.overrideState = toParam.overrideState;
					stateParam.Interp(stateParam, toParam, interpFactor);
				}
			}
		}

		/// <summary>
		/// Sets the state of all the overrides on this component to a given value.
		/// </summary>
		/// <param name="state">The value to set the state of the overrides to.</param>
		public void SetAllOverridesTo(bool state)
		{
			SetOverridesTo(parameterList, state);
		}

		/// <summary>
		/// Sets the override state of the given parameters on this component to a given value.
		/// </summary>
		/// <param name="state">The value to set the state of the overrides to.</param>
		internal void SetOverridesTo(IEnumerable<ScriptableVolumeParameter> enumerable, bool state)
		{
			foreach (var prop in enumerable)
			{
				prop.overrideState = state;
				var t = prop.GetType();

				if (ScriptableVolumeParameter.IsObjectParameter(t))
				{
					// This method won't be called a lot but this is sub-optimal, fix me
					var innerParams = (ReadOnlyCollection<ScriptableVolumeParameter>)
						t.GetProperty("parameters", BindingFlags.NonPublic | BindingFlags.Instance)
							.GetValue(prop, null);

					if (innerParams != null)
						SetOverridesTo(innerParams, state);
				}
			}
		}

		/// <summary>
		/// A custom hashing function that Unity uses to compare the state of parameters.
		/// </summary>
		/// <returns>A computed hash code for the current instance.</returns>
		public override int GetHashCode()
		{
			unchecked
			{
				//return parameters.Aggregate(17, (i, p) => i * 23 + p.GetHash());

				int hash = 17;

				for (int i = 0; i < parameterList.Count; i++)
					hash = hash * 23 + parameterList[i].GetHashCode();

				return hash;
			}
		}

		/// <summary>
		/// Returns true if any of the volume properites has been overridden.
		/// </summary>
		/// <returns>True if any of the volume properites has been overridden.</returns>
		public bool AnyPropertiesIsOverridden()
		{
			for (int i = 0; i < parameterList.Count; ++i)
			{
				if (parameterList[i].overrideState) return true;
			}
			return false;
		}

		/// <summary>
		/// Unity calls this method before the object is destroyed.
		/// </summary>
		protected virtual void OnDestroy() => Release();

		/// <summary>
		/// Releases all the allocated resources.
		/// </summary>
		public void Release()
		{
			if (parameterList == null)
				return;

			for (int i = 0; i < parameterList.Count; i++)
			{
				if (parameterList[i] != null)
					parameterList[i].Release();
			}
		}
	}
}