using System;
using System.Reflection;

namespace BetterExperience.Utils;

public static class ReflectionExtensions
{
	public static T GetAttribute<T>(this Type t) where T : Attribute
	{
		return (T)Attribute.GetCustomAttribute(t, typeof(T));
	}

	public static T GetAttribute<T>(this PropertyInfo t) where T : Attribute
	{
		return (T)Attribute.GetCustomAttribute(t, typeof(T));
	}

	public static T GetAttribute<T>(this MethodInfo t) where T : Attribute
	{
		return (T)Attribute.GetCustomAttribute(t, typeof(T));
	}
}
