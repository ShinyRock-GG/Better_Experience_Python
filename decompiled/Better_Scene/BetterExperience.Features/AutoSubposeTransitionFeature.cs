using System;
using System.Collections.Generic;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;

namespace BetterExperience.Features;

internal class AutoSubposeTransitionFeature : PluginService
{
	public class PostureChangeAnimatorService : SessionService, InteractionPreprocessor
	{
		private InteractionManager interactionManager;

		public override void OnStart()
		{
			base.OnStart();
			interactionManager = Lookup<InteractionManager>();
			interactionManager.Preprocessors.Add(this);
		}

		public void Process(Interaction interaction)
		{
			if (interaction.Sequence.Count > 0)
			{
				BasicOperation op = interaction.Sequence[0];
				if (op is AnimateOp animateOp && op.Preprocessors.Add(this))
				{
					ProcessSetPosture(interaction, animateOp, 0);
				}
			}
		}

		private void ProcessSetPosture(Interaction interaction, AnimateOp animateOp, int index)
		{
			InteractionManager.InteractionQueryContext ctx = interactionManager.CreateQueryContext();
			PoseAnimationClip targetClip = TryGuessClip(ctx, animateOp);
			if (targetClip != null)
			{
				List<string> targetSequence = ComputeSequence(ctx, targetClip);
			}
		}

		private List<string> ComputeSequence(InteractionManager.InteractionQueryContext ctx, PoseAnimationClip targetClip)
		{
			List<string> seq = new List<string>();
			PoseAnimationClip clip = targetClip;
			while (!clip.IsIdle)
			{
				seq.Insert(0, clip.Name);
				if (clip.Posture.Poses.ClipDescriptors.TryGetValue(clip, out var cd) && cd.Type != InteractionType.subpose)
				{
					clip = GetIdle(ctx);
				}
			}
			throw new NotImplementedException();
		}

		private PoseAnimationClip GetIdle(InteractionManager.InteractionQueryContext ctx)
		{
			throw new NotImplementedException();
		}

		private PoseAnimationClip TryGuessClip(InteractionManager.InteractionQueryContext ctx, AnimateOp animateOp)
		{
			List<PoseAnimationClip> clips = animateOp.TryResolveClipsForContext(ctx);
			if (clips.Count > 0)
			{
				return clips[0];
			}
			return null;
		}
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<StoryManager>().StoryInterviewServices.Add(() => new PostureChangeAnimatorService());
	}
}
