using System;
using System.Collections.Generic;
using System.Reflection;

namespace BrightIdeasSoftware
{
	/// <summary>
	/// An instance of Munger gets a value from or puts a value into a target object. The property
	/// to be peeked (or poked) is determined from a string. The peeking or poking is done using reflection.
	/// </summary>
	/// <remarks>
	/// Name of the aspect to be peeked can be a field, property or parameterless method. The name of an
	/// aspect to poke can be a field, writable property or single parameter method.
	/// <para>
	/// Aspect names can be dotted to chain a series of references. 
	/// </para>
	/// <example>Order.Customer.HomeAddress.State</example>
	/// </remarks>
	public class Munger
	{
		#region Life and death

		/// <summary>
		/// Create a Munger that works on the given aspect name
		/// </summary>
		/// <param name="aspectName">The name of the </param>
		public Munger(string aspectName)
		{
			AspectName = aspectName;
		}

		#endregion

		#region Static utility methods

		/// <summary>
		/// Gets or sets whether Mungers will silently ignore missing aspect errors.
		/// </summary>
		/// <remarks>
		/// <para>
		/// By default, if a Munger is asked to fetch a field/property/method
		/// that does not exist from a model, it returns an error message, since that 
		/// condition is normally a programming error. There are some use cases where
		/// this is not an error, and the munger should simply keep quiet.
		/// </para>
		/// <para>By default this is true during release builds.</para>
		/// </remarks>
		public static bool IgnoreMissingAspects
		{
			get { return ignoreMissingAspects; }
			set { ignoreMissingAspects = value; }
		}
		private static bool ignoreMissingAspects
#if !DEBUG
            = true
#endif
			;

		#endregion

		#region Public properties

		/// <summary>
		/// The name of the aspect that is to be peeked or poked.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This name can be a field, property or parameter-less method.
		/// </para>
		/// <para>
		/// The name can be dotted, which chains references. If any link in the chain returns
		/// null, the entire chain is considered to return null.
		/// </para>
		/// </remarks>
		/// <example>"DateOfBirth"</example>
		/// <example>"Owner.HomeAddress.Postcode"</example>
		public string AspectName
		{
			get { return aspectName; }
			set
			{
				aspectName = value;

				// Clear any cache
				aspectParts = null;
			}
		}
		private string aspectName;

		#endregion

		#region Public interface

		/// <summary>
		/// Extract the value indicated by our AspectName from the given target.
		/// </summary>
		/// <remarks>If the aspect name is null or empty, this will return null.</remarks>
		/// <param name="target">The object that will be peeked</param>
		/// <returns>The value read from the target</returns>
		public object GetValue(object target)
		{
			if (Parts.Count == 0)
				return null;

			try
			{
				return EvaluateParts(target, Parts);
			}
			catch (MungerException ex)
			{
				if (Munger.IgnoreMissingAspects)
					return null;

				return string.Format("'{0}' is not a parameter-less method, property or field of type '{1}'",
										 ex.Munger.AspectName, ex.Target.GetType());
			}
		}

		/// <summary>
		/// Poke the given value into the given target indicated by our AspectName.
		/// </summary>
		/// <remarks>
		/// <para>
		/// If the AspectName is a dotted path, all the selectors bar the last
		/// are used to find the object that should be updated, and the last
		/// selector is used as the property to update on that object.
		/// </para>
		/// <para>
		/// So, if 'target' is a Person and the AspectName is "HomeAddress.Postcode",
		/// this method will first fetch "HomeAddress" property, and then try to set the
		/// "Postcode" property on the home address object.
		/// </para>
		/// </remarks>
		/// <param name="target">The object that will be poked</param>
		/// <param name="value">The value that will be poked into the target</param>
		/// <returns>bool indicating whether the put worked</returns>
		public bool PutValue(object target, object value)
		{
			if (Parts.Count == 0)
				return false;

			SimpleMunger lastPart = Parts[Parts.Count - 1];

			if (Parts.Count > 1)
			{
				List<SimpleMunger> parts = new List<SimpleMunger>(Parts);
				parts.RemoveAt(parts.Count - 1);
				try
				{
					target = EvaluateParts(target, parts);
				}
				catch (MungerException ex)
				{
					ReportPutValueException(ex);
					return false;
				}
			}

			if (target != null)
			{
				try
				{
					return lastPart.PutValue(target, value);
				}
				catch (MungerException ex)
				{
					ReportPutValueException(ex);
				}
			}

			return false;
		}

		#endregion

		#region Implementation

		/// <summary>
		/// Gets the list of SimpleMungers that match our AspectName
		/// </summary>
		private IList<SimpleMunger> Parts
		{
			get
			{
				if (aspectParts == null)
					aspectParts = BuildParts(AspectName);
				return aspectParts;
			}
		}
		private IList<SimpleMunger> aspectParts;

		/// <summary>
		/// Convert a possibly dotted AspectName into a list of SimpleMungers
		/// </summary>
		/// <param name="aspect"></param>
		/// <returns></returns>
		private IList<SimpleMunger> BuildParts(string aspect)
		{
			List<SimpleMunger> parts = new List<SimpleMunger>();
			if (!string.IsNullOrEmpty(aspect))
			{
				foreach (string part in aspect.Split('.'))
				{
					parts.Add(new SimpleMunger(part.Trim()));
				}
			}
			return parts;
		}

		/// <summary>
		/// Evaluate the given chain of SimpleMungers against an initial target.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="parts"></param>
		/// <returns></returns>
		private object EvaluateParts(object target, IList<SimpleMunger> parts)
		{
			foreach (SimpleMunger part in parts)
			{
				if (target == null)
					break;
				target = part.GetValue(target);
			}
			return target;
		}

		private void ReportPutValueException(MungerException ex)
		{
			//TODO: How should we report this error?
			System.Diagnostics.Debug.WriteLine("PutValue failed");
			System.Diagnostics.Debug.WriteLine(string.Format("- Culprit aspect: {0}", ex.Munger.AspectName));
			System.Diagnostics.Debug.WriteLine(string.Format("- Target: {0} of type {1}", ex.Target, ex.Target.GetType()));
			System.Diagnostics.Debug.WriteLine(string.Format("- Inner exception: {0}", ex.InnerException));
		}

		#endregion
	}

	/// <summary>
	/// A SimpleMunger deals with a single property/field/method on its target.
	/// </summary>
	/// <remarks>
	/// Munger uses a chain of these resolve a dotted aspect name.
	/// </remarks>
	public class SimpleMunger
	{
		#region Life and death

		/// <summary>
		/// Create a SimpleMunger
		/// </summary>
		/// <param name="aspectName"></param>
		public SimpleMunger(string aspectName)
		{
			this.aspectName = aspectName;
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The name of the aspect that is to be peeked or poked.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This name can be a field, property or method. 
		/// When using a method to get a value, the method must be parameter-less.
		/// When using a method to set a value, the method must accept 1 parameter.
		/// </para>
		/// <para>
		/// It cannot be a dotted name.
		/// </para>
		/// </remarks>
		public string AspectName
		{
			get { return aspectName; }
		}
		private readonly string aspectName;

		#endregion

		#region Public interface

		/// <summary>
		/// Get a value from the given target
		/// </summary>
		/// <param name="target"></param>
		/// <returns></returns>
		public object GetValue(object target)
		{
			if (target == null)
				return null;

			ResolveName(target, AspectName, 0);

			try
			{
				if (resolvedPropertyInfo != null)
					return resolvedPropertyInfo.GetValue(target, null);

				if (resolvedMethodInfo != null)
					return resolvedMethodInfo.Invoke(target, null);

				if (resolvedFieldInfo != null)
					return resolvedFieldInfo.GetValue(target);

				// If that didn't work, try to use the indexer property. 
				// This covers things like dictionaries and DataRows.
				if (indexerPropertyInfo != null)
					return indexerPropertyInfo.GetValue(target, new object[] { AspectName });
			}
			catch (Exception ex)
			{
				// Lots of things can do wrong in these invocations
				throw new MungerException(this, target, ex);
			}

			// If we get to here, we couldn't find a match for the aspect
			throw new MungerException(this, target, new MissingMethodException());
		}

		/// <summary>
		/// Poke the given value into the given target indicated by our AspectName.
		/// </summary>
		/// <param name="target">The object that will be poked</param>
		/// <param name="value">The value that will be poked into the target</param>
		/// <returns>bool indicating if the put worked</returns>
		public bool PutValue(object target, object value)
		{
			if (target == null)
				return false;

			ResolveName(target, AspectName, 1);

			try
			{
				if (resolvedPropertyInfo != null)
				{
					resolvedPropertyInfo.SetValue(target, value, null);
					return true;
				}

				if (resolvedMethodInfo != null)
				{
					resolvedMethodInfo.Invoke(target, new object[] { value });
					return true;
				}

				if (resolvedFieldInfo != null)
				{
					resolvedFieldInfo.SetValue(target, value);
					return true;
				}

				// If that didn't work, try to use the indexer property. 
				// This covers things like dictionaries and DataRows.
				if (indexerPropertyInfo != null)
				{
					indexerPropertyInfo.SetValue(target, value, new object[] { AspectName });
					return true;
				}
			}
			catch (Exception ex)
			{
				// Lots of things can do wrong in these invocations
				throw new MungerException(this, target, ex);
			}

			return false;
		}

		#endregion

		#region Implementation

		private void ResolveName(object target, string name, int numberMethodParameters)
		{

			if (cachedTargetType == target.GetType() && cachedName == name && cachedNumberParameters == numberMethodParameters)
				return;

			cachedTargetType = target.GetType();
			cachedName = name;
			cachedNumberParameters = numberMethodParameters;

			resolvedFieldInfo = null;
			resolvedPropertyInfo = null;
			resolvedMethodInfo = null;
			indexerPropertyInfo = null;

			const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance /*| BindingFlags.NonPublic*/;

			foreach (PropertyInfo pinfo in target.GetType().GetProperties(flags))
			{
				if (pinfo.Name == name)
				{
					resolvedPropertyInfo = pinfo;
					return;
				}

				// See if we can find an string indexer property while we are here.
				// We also need to allow for old style <object> keyed collections.
				if (indexerPropertyInfo == null && pinfo.Name == "Item")
				{
					ParameterInfo[] par = pinfo.GetGetMethod().GetParameters();
					if (par.Length > 0)
					{
						Type parameterType = par[0].ParameterType;
						if (parameterType == typeof(string) || parameterType == typeof(object))
							indexerPropertyInfo = pinfo;
					}
				}
			}

			foreach (FieldInfo info in target.GetType().GetFields(flags))
			{
				if (info.Name == name)
				{
					resolvedFieldInfo = info;
					return;
				}
			}

			foreach (MethodInfo info in target.GetType().GetMethods(flags))
			{
				if (info.Name == name && info.GetParameters().Length == numberMethodParameters)
				{
					resolvedMethodInfo = info;
					return;
				}
			}
		}

		private Type cachedTargetType;
		private string cachedName;
		private int cachedNumberParameters;

		private FieldInfo resolvedFieldInfo;
		private PropertyInfo resolvedPropertyInfo;
		private MethodInfo resolvedMethodInfo;
		private PropertyInfo indexerPropertyInfo;

		#endregion
	}

	/// <summary>
	/// These exceptions are raised when a munger finds something it cannot process
	/// </summary>
	public class MungerException : ApplicationException
	{
		/// <summary>
		/// Create a MungerException
		/// </summary>
		/// <param name="munger"></param>
		/// <param name="target"></param>
		/// <param name="ex"></param>
		public MungerException(SimpleMunger munger, object target, Exception ex)
			: base("Munger failed", ex)
		{
			this.munger = munger;
			this.target = target;
		}

		/// <summary>
		/// Get the munger that raised the exception
		/// </summary>
		public SimpleMunger Munger
		{
			get { return munger; }
		}
		private readonly SimpleMunger munger;

		/// <summary>
		/// Gets the target that threw the exception
		/// </summary>
		public object Target
		{
			get { return target; }
		}
		private readonly object target;
	}
}