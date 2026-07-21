using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;

namespace BetterExperience.Features;

internal class AnimatePostureChangeFeature : PluginService
{
	public class PostureChangeAnimatorService : SessionService, InteractionPreprocessor
	{
		private InteractionManager interactionManager;

		public override void OnStart()
		{
			logger.EnableDebug = true;
			base.OnStart();
			interactionManager = Lookup<InteractionManager>();
			interactionManager.Preprocessors.Add(this);
		}

		public void Process(Interaction interaction)
		{
			if (interaction.Sequence.Count > 0)
			{
				BasicOperation op = interaction.Sequence[0];
				if (op is SetPostureOp postureOp && op.Preprocessors.Add(this))
				{
					ProcessSetPosture(interaction, postureOp, 0);
				}
			}
		}

		private void ProcessSetPosture(Interaction interaction, SetPostureOp setPostureOp, int index)
		{
			InteractionManager.InteractionQueryContext ctx = interactionManager.CreateQueryContext();
			POIPosture currentPosture = interactionManager.CurrentPosture;
			string currentPoiPosture = currentPosture.Id;
			string targetPoiPosture = setPostureOp.TargetPosture.Id;
			string currentPostureId = currentPosture.PostureId;
			string targetPostureId = setPostureOp.TargetPosture.PostureId;
			POIPosture targetPosture = setPostureOp.TargetPosture;
			if (ctx.activeClip != null && !ctx.activeClip.IsIdle)
			{
				List<PoseAnimationClip> idle = currentPosture.Poses.FindClips("Idle");
				if (idle.Count == 0)
				{
					idle = currentPosture.Poses.FindClips("Binding");
				}
				if (idle.Count > 0)
				{
					interaction.Sequence.Insert(index, new AnimateOp(idle));
					index++;
				}
			}
			logger.Error("Looking up {0} {1}", currentPostureId, targetPostureId);
			if (targetPosture.Poses.TransitionClips.TryGetValue((currentPoiPosture, targetPoiPosture), out var transitionClips))
			{
				interaction.Sequence.Insert(index + 1, new AnimateOp(transitionClips));
				return;
			}
			if (currentPosture.Poses.TransitionClips.TryGetValue((currentPoiPosture, targetPoiPosture), out transitionClips))
			{
				interaction.Sequence.Insert(index, new AnimateOp(transitionClips));
				return;
			}
			if (targetPosture.Poses.TransitionClips.TryGetValue((currentPostureId, targetPostureId), out transitionClips))
			{
				interaction.Sequence.Insert(index + 1, new AnimateOp(transitionClips));
				return;
			}
			if (currentPosture.Poses.TransitionClips.TryGetValue((currentPostureId, targetPostureId), out transitionClips))
			{
				interaction.Sequence.Insert(index, new AnimateOp(transitionClips));
				return;
			}
			logger.Debug("No posture transition found {0}->{1}", currentPostureId, targetPostureId);
			logger.Debug("Available transitions:");
			logger.Debug("At source: {0}", string.Join(";", currentPosture.Poses.TransitionClips.Select((KeyValuePair<(string, string), List<PoseAnimationClip>> x) => x.Key.ToString() + "->" + x.Value).ToArray()));
			logger.Debug("At target: {0}", string.Join(";", targetPosture.Poses.TransitionClips.Select((KeyValuePair<(string, string), List<PoseAnimationClip>> x) => x.Key.ToString() + "->" + x.Value).ToArray()));
		}
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<StoryManager>().StoryInterviewServices.Add(() => new PostureChangeAnimatorService());
	}
}
