using System;
using System.Collections.Generic;
using BetterExperience.CustomScene.ConstructionKit;
using BetterExperience.CustomScene.ConstructionKit.AnimatorScripts;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.CustomStory.ConstructionKit.AnimatorScripts;

public class AnimatorScriptRegistry
{
	private static List<(string, Func<AnimatorScriptRegistry, Action<PoseAnimationClip>>)> factory = new List<(string, Func<AnimatorScriptRegistry, Action<PoseAnimationClip>>)>();

	public List<KeyValuePair<string, Action<PoseAnimationClip>>> Scripts { get; } = new List<KeyValuePair<string, Action<PoseAnimationClip>>>();

	public Vector3 DeltaRootPos { get; set; }

	public Quaternion DeltaRootRot { get; set; }

	public ClipEditorPanelService Service { get; internal set; }

	public AnimatorScriptRegistry()
	{
		Reset();
	}

	public void Reset()
	{
		Scripts.Clear();
		Scripts.Add(new KeyValuePair<string, Action<PoseAnimationClip>>("Smooth Forward Loop", delegate(PoseAnimationClip p)
		{
			SmoothMotionLoop smoothMotionLoop = new SmoothMotionLoop(p);
			smoothMotionLoop.HasForwardMotion = true;
			smoothMotionLoop.Process();
		}));
		Scripts.Add(new KeyValuePair<string, Action<PoseAnimationClip>>("Smooth Rotation Loop", delegate(PoseAnimationClip p)
		{
			SmoothMotionLoop smoothMotionLoop = new SmoothMotionLoop(p);
			smoothMotionLoop.HasForwardMotion = false;
			smoothMotionLoop.Process();
		}));
		Scripts.Add(new KeyValuePair<string, Action<PoseAnimationClip>>("Hyper Pin Muscles", delegate(PoseAnimationClip p)
		{
			new HyperPinMuscles(p).Process();
		}));
		Scripts.Add(new KeyValuePair<string, Action<PoseAnimationClip>>("Root transform correction", delegate(PoseAnimationClip p)
		{
			new RootTransformCorrection(p, this).Process();
		}));
		Scripts.Add(new KeyValuePair<string, Action<PoseAnimationClip>>("Delete frames range", delegate(PoseAnimationClip p)
		{
			new FrameRangeRemoval(p, this).Process();
		}));
		Scripts.Add(new KeyValuePair<string, Action<PoseAnimationClip>>("Delete unused keyframes", delegate(PoseAnimationClip p)
		{
			new KeyframeCleanup(p, this).Process();
		}));
		Scripts.Add(new KeyValuePair<string, Action<PoseAnimationClip>>("Find loop", delegate(PoseAnimationClip p)
		{
			new FindLoopFrames(p, this).Process();
		}));
		foreach (var x in factory)
		{
			Action<PoseAnimationClip> fn = x.Item2(this);
			Scripts.Add(new KeyValuePair<string, Action<PoseAnimationClip>>(x.Item1, fn));
		}
	}

	public static void AddScriptFactory(string name, Func<AnimatorScriptRegistry, Action<PoseAnimationClip>> fn, ScopeSupport scope)
	{
		(string name, Func<AnimatorScriptRegistry, Action<PoseAnimationClip>> fn) t = (name: name, fn: fn);
		factory.Add(t);
		if (scope != null)
		{
			scope.OnDispose += delegate
			{
				factory.Remove(t);
				Logger.Global.Info("script disposed " + name);
			};
		}
	}
}
