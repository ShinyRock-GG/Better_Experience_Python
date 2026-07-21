using System;
using System.Collections.Generic;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class DynamicIKTarget : IEquatable<DynamicIKTarget>
{
	public string Target { get; set; }

	public Vector3 LocalPosition { get; set; }

	public Quaternion LocalRotation { get; set; }

	public DynamicIKTarget()
	{
	}

	public DynamicIKTarget(RelIKTargeting.IKTarget target, GameSession session)
	{
		LocalPosition = target.LocalOffset;
		LocalRotation = target.LocalRotation;
		string name = UnityUtils.GetNameInHierarchy(target.Transform, null);
		string oriname = name;
		string playername = UnityUtils.GetNameInHierarchy(session.Player.GameObject.transform, null);
		string guestname = UnityUtils.GetNameInHierarchy(((Component)(object)session.Guest.Impl).gameObject.transform, null);
		name = name.Replace(playername, "%player%");
		name = (Target = name.Replace(guestname, "%guest%"));
		Logger.Global.Info("Retargeting {0} {1} {2} = {3}", oriname, playername, guestname, name);
	}

	public override bool Equals(object obj)
	{
		return Equals(obj as DynamicIKTarget);
	}

	public bool Equals(DynamicIKTarget other)
	{
		if (other != null && Target == other.Target && LocalPosition.Equals(other.LocalPosition))
		{
			return LocalRotation.Equals(other.LocalRotation);
		}
		return false;
	}

	public override int GetHashCode()
	{
		int hashCode = 539024425;
		hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Target);
		hashCode = hashCode * -1521134295 + LocalPosition.GetHashCode();
		return hashCode * -1521134295 + LocalRotation.GetHashCode();
	}
}
