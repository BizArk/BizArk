﻿using System;

namespace BizArk.Core
{
	/// <summary>
	/// A simple but extensible class factory. Used by BizArk to create some internal objects.
	/// </summary>
	public static class ClassFactory
	{

		/// <summary>
		/// Creates an object of the specified type.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static T CreateObject<T>(params object[] args)
		{
			return (T)CreateObject(typeof(T), args);
		}

		/// <summary>
		/// Creates an object of the specified type.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public static object CreateObject(Type type, params object[] args)
		{
			if (Factory != null)
			{
				var obj = Factory(type, args);
				if (obj != null) return obj;
			}

			// If the Factory method is null or returns null, create the instance using the default method.
			return Activator.CreateInstance(type, args);
		}

		/// <summary>
		/// Get or set the factory to use. Only a single factory can be used at a time. Leave null for default bahavior.
		/// </summary>
		public static Func<Type, object[], object> Factory { get; set; }

	}
}
