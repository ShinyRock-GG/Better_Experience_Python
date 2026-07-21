using System.Collections.Generic;
using Assets;
using Assets.Base.Bones.Gizmos.BeachGirl.Runtime;
using RootMotion.Dynamics;
using RootMotion.FinalIK;

namespace BetterExperience.CustomScene.Poser;

public class BoneMuscleData : Dictionary<string, MuscleConfig>
{
	private static Dictionary<(Muscle.GroupCompleto, Side), string> muscleKeyCache = new Dictionary<(Muscle.GroupCompleto, Side), string>();

	private static Dictionary<FullBodyBipedEffector, string> effectorKeyCache = new Dictionary<FullBodyBipedEffector, string>();

	internal void Save(GizmoDeBoneRMInfo x)
	{
		if (x.esMusculo)
		{
			string key = MuscleKey(x);
			if (!ContainsKey(key))
			{
				MuscleConfig cfg = new MuscleConfig();
				cfg.Pin = x.gizmoDeBone.boneMuscleConfig.musclePin;
				cfg.Spring = x.gizmoDeBone.boneMuscleConfig.muscleSpring;
				cfg.Damper = x.gizmoDeBone.boneMuscleConfig.muscleDamper;
				base[key] = cfg;
			}
		}
		if (x.isEffector)
		{
			string key2 = EffectorKey(x);
			if (!ContainsKey(key2))
			{
				MuscleConfig cfg2 = new MuscleConfig();
				cfg2.IsSupport = x.gizmoDeBone.boneMuscleConfig.puedeApoyarse;
				cfg2.CanInteract = x.gizmoDeBone.boneMuscleConfig.puedeInteractuar;
				cfg2.CanReact = x.gizmoDeBone.boneMuscleConfig.puedeReaccionar;
				base[key2] = cfg2;
			}
		}
	}

	internal void Apply(GizmoDeBoneRMInfo x)
	{
		if (x.esMusculo)
		{
			string key = MuscleKey(x);
			if (TryGetValue(key, out var cfg))
			{
				x.gizmoDeBone.boneMuscleConfig.musclePin = cfg.Pin;
				x.gizmoDeBone.boneMuscleConfig.muscleSpring = cfg.Spring;
				x.gizmoDeBone.boneMuscleConfig.muscleDamper = cfg.Damper;
			}
		}
		if (x.isEffector)
		{
			string key2 = EffectorKey(x);
			if (TryGetValue(key2, out var cfg2))
			{
				x.gizmoDeBone.boneMuscleConfig.puedeApoyarse = cfg2.IsSupport;
				x.gizmoDeBone.boneMuscleConfig.puedeInteractuar = cfg2.CanInteract;
				x.gizmoDeBone.boneMuscleConfig.puedeReaccionar = cfg2.CanReact;
			}
		}
	}

	private string MuscleKey(GizmoDeBoneRMInfo x)
	{
		(Muscle.GroupCompleto, Side) key = (x.muscle, x.side);
		if (!muscleKeyCache.TryGetValue(key, out var value))
		{
			value = $"M-{x.muscle}-{x.side}";
			muscleKeyCache[key] = value;
		}
		return value;
	}

	private string EffectorKey(GizmoDeBoneRMInfo x)
	{
		if (!effectorKeyCache.TryGetValue(x.effector, out var value))
		{
			value = $"E-{x.effector}";
			effectorKeyCache[x.effector] = value;
		}
		return value;
	}
}
