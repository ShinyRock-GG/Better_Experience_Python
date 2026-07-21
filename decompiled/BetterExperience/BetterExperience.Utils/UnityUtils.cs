using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Utils;

public class UnityUtils
{
	public static Transform NewTransform(string name, Transform parent = null, ScopeSupport scope = null)
	{
		GameObject go = new GameObject();
		go.name = name;
		go.transform.parent = parent;
		if (scope != null)
		{
			scope.OnDispose += delegate
			{
				UnityEngine.Object.DestroyImmediate(go);
			};
		}
		if (parent == null)
		{
			go.hideFlags |= HideFlags.HideAndDontSave;
			UnityEngine.Object.DontDestroyOnLoad(go);
		}
		return go.transform;
	}

	public static T[] FindAndInitializeSingletonsOfType<T>()
	{
		Singleton[] singletons = UnityEngine.Object.FindObjectsOfType<Singleton>();
		List<T> result = new List<T>();
		Singleton[] array = singletons;
		foreach (Singleton s in array)
		{
			Type t = s.GetType();
			Type st = FindGenericSuperClass(t, typeof(Singleton<>));
			if (st == null)
			{
				continue;
			}
			Type singletonT = st.GenericTypeArguments[0];
			if (!typeof(T).IsAssignableFrom(singletonT))
			{
				continue;
			}
			PropertyInfo prop = st.GetProperty("instance");
			if (!(prop == null))
			{
				if (prop.GetValue(null) is T r)
				{
					result.Add(r);
					continue;
				}
				new Logger().Error("Unable to get instance of singleton {0}", t.Name);
			}
		}
		return result.ToArray();
	}

	internal static Vector3 Abs(Vector3 result)
	{
		return new Vector3(Mathf.Abs(result.x), Mathf.Abs(result.y), Mathf.Abs(result.z));
	}

	internal static Vector3 Truncate(Vector3 result, float prec)
	{
		return new Vector3((result.x > prec) ? result.x : 0f, (result.y > prec) ? result.y : 0f, (result.z > prec) ? result.z : 0f);
	}

	private static Type FindGenericSuperClass(Type t, Type genericBase)
	{
		if (t == typeof(object))
		{
			return null;
		}
		if (t.IsGenericType && t.GetGenericTypeDefinition() == genericBase)
		{
			return t;
		}
		return FindGenericSuperClass(t.BaseType, genericBase);
	}

	public static Vector3 ToEuler(Quaternion quaternion)
	{
		Vector3 euler = quaternion.eulerAngles;
		NormalizeEuler(ref euler);
		return euler;
	}

	public static void NormalizeEuler(ref Vector3 rEuler)
	{
		if (rEuler.x < -180f)
		{
			rEuler.x += 360f;
		}
		else if (rEuler.x > 180f)
		{
			rEuler.x -= 360f;
		}
		if (rEuler.y < -180f)
		{
			rEuler.y += 360f;
		}
		else if (rEuler.y > 180f)
		{
			rEuler.y -= 360f;
		}
		if (rEuler.z < -180f)
		{
			rEuler.z += 360f;
		}
		else if (rEuler.z > 180f)
		{
			rEuler.z -= 360f;
		}
	}

	public static float FromToAxisAngle(Vector3 from, Vector3 to, Vector3 normal)
	{
		Vector3 planeFrom = Vector3.ProjectOnPlane(from, normal);
		Vector3 planeTo = Vector3.ProjectOnPlane(to, normal);
		return Vector3.SignedAngle(planeFrom, planeTo, normal);
	}

	public static bool AddNeighbourComponent<TFind, TAdd>(Transform transform, Action<TAdd> tuning) where TFind : Component where TAdd : Component
	{
		TFind[] targets = transform.GetComponentsInChildren<TFind>();
		TFind[] array = targets;
		foreach (TFind c in array)
		{
			TAdd obj = c.transform.gameObject.AddComponent<TAdd>();
			tuning?.Invoke(obj);
		}
		return targets.Length != 0;
	}

	public static bool DisplaceComponent<TFind, TDisplace>(Transform transform, Action<TFind, TDisplace> tuning) where TFind : Component where TDisplace : Component
	{
		TFind[] targets = transform.GetComponentsInChildren<TFind>();
		TFind[] array = targets;
		foreach (TFind c in array)
		{
			TDisplace obj = c.transform.gameObject.AddComponent<TDisplace>();
			tuning?.Invoke(c, obj);
			UnityEngine.Object.DestroyImmediate(c);
		}
		return targets.Length != 0;
	}

	public static string GetNameInHierarchy(Transform t, Transform relativeTo)
	{
		List<string> path = new List<string>();
		while (t != null || t != relativeTo)
		{
			path.Insert(0, t.name);
			t = t.parent;
		}
		if (t == relativeTo)
		{
			return string.Join("/", path);
		}
		return null;
	}

	public static void DestroyComponent<T>(GameObject clone) where T : UnityEngine.Object
	{
		T c = clone.GetComponent<T>();
		if (c != null)
		{
			UnityEngine.Object.DestroyImmediate(c);
		}
	}
}
