using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomStory.ConstructionKit.AnimatorScripts;

internal class RootTransformCorrection
{
	private Logger logger = Logger.Create<RootTransformCorrection>();

	private PoseAnimationClip p;

	private AnimatorScriptRegistry reg;

	public RootTransformCorrection(PoseAnimationClip p, AnimatorScriptRegistry animatorScriptRegistry)
	{
		this.p = p;
		reg = animatorScriptRegistry;
	}

	internal void Process()
	{
		logger.Info("INput {0} {1}", reg.DeltaRootPos, reg.DeltaRootRot);
		foreach (BoneConfiguration f in p.Frames)
		{
			f.HipOffset = reg.DeltaRootRot * f.HipOffset;
			f.HipOffset += reg.DeltaRootPos;
			if (f.Rotations != null)
			{
				f.Rotations["CC_Base_Hip"] = reg.DeltaRootRot * f.Rotations["CC_Base_Hip"];
			}
		}
	}
}
