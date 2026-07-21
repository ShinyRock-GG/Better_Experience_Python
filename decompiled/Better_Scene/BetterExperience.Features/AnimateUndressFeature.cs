using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using Assets._ReusableScripts.Miscellaneous;
using Better_Cloth;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using PixelCrushers.DialogueSystem;

namespace BetterExperience.Features;

internal class AnimateUndressFeature : PluginFeature
{
	private class AnimateUndressService : StoryService
	{
		public class Settings
		{
			public Dictionary<string, string> Mapping { get; set; } = new Dictionary<string, string>();

			public Dictionary<string, List<string>> Slots { get; set; } = new Dictionary<string, List<string>>();
		}

		private InteractionManager interactionManager;

		private PoseManager poseManager;

		private Dictionary<string, string> animationMap = new Dictionary<string, string>();

		private Dictionary<string, List<RopaCubre>> slotMap = new Dictionary<string, List<RopaCubre>>();

		private List<ClothFeature.ClothRemovalRequest> pendingRequests = new List<ClothFeature.ClothRemovalRequest>();

		public override void OnStart()
		{
			base.OnStart();
			Settings settings = base.Story.VFS.Persisted(() => new Settings(), "\\animate_undress.json");
			animationMap = settings.Mapping;
			if (animationMap == null)
			{
				animationMap = new Dictionary<string, string>();
			}
			slotMap = CreateSlotMap(settings.Slots);
			Lookup<ClothFeature.ClothRemovalService>().OnClothRemove.Add(OnClothRemoveRequest);
			interactionManager = Lookup<InteractionManager>();
			poseManager = Lookup<PoseManager>();
			Lookup<DispatcherService>().DoUpdate.Add(DoUpdate, base.Scope);
		}

		private Dictionary<string, List<RopaCubre>> CreateSlotMap(Dictionary<string, List<string>> slots)
		{
			Dictionary<string, List<RopaCubre>> result = new Dictionary<string, List<RopaCubre>>();
			foreach (KeyValuePair<string, List<string>> kv in slots)
			{
				string slot = kv.Key;
				List<string> strFlags = kv.Value;
				List<RopaCubre> t = new List<RopaCubre>();
				foreach (string s in strFlags)
				{
					if (Enum.TryParse<RopaCubre>(s, out var r))
					{
						t.Add(r);
						continue;
					}
					logger.Error("Bad ropacubre {0}", s);
				}
				result[slot] = t;
			}
			return result;
		}

		private void DoUpdate()
		{
			if (pendingRequests.Count <= 0)
			{
				return;
			}
			bool interruptAnimator = false;
			if (interactionManager.CurrentPosture == null)
			{
				interruptAnimator = true;
			}
			else if (!interactionManager.AnimationController.Enabled)
			{
				interruptAnimator = true;
			}
			Interaction i = new Interaction();
			i.DisplayName = "Take off";
			i.SourcePosture = interactionManager.CurrentPosture;
			i.TargetPosture = interactionManager.CurrentPosture;
			foreach (ClothFeature.ClothRemovalRequest req in pendingRequests)
			{
				RopaCubre cubreFlags = req.Cloth.dataDeRopa.cubreFlag;
				string slot = MapSlot(cubreFlags);
				if (slot == null || !animationMap.TryGetValue(slot, out var targetAnimation))
				{
					logger.Error("Unmapped RopaPreSetId {0} {1}", ((BaseGlobalUserData)req.Cloth.dataDeRopa).stringId, cubreFlags);
					req.Proceed();
				}
				else
				{
					i.DisplayName = i.DisplayName + " " + req.Cloth.name;
					i.Enqueue(new AnimateOp(targetAnimation, AnimatorLayer.Body));
					i.Enqueue(new LambdaOp(delegate
					{
						req.Proceed();
					}));
				}
			}
			if (interruptAnimator)
			{
				i.Enqueue(new LambdaOp(delegate(InteractionContext ctx)
				{
					ctx.AnimationController.InterruptPose("AnimateUndressComplete");
				}));
			}
			if (!interactionManager.StartInteraction(i))
			{
				pendingRequests.ForEach(delegate(ClothFeature.ClothRemovalRequest p)
				{
					p.Proceed();
				});
			}
			pendingRequests.Clear();
		}

		private string MapSlot(RopaCubre cubreFlags)
		{
			KeyValuePair<string, List<RopaCubre>>? selection = null;
			foreach (KeyValuePair<string, List<RopaCubre>> kv in slotMap)
			{
				if (Matches(kv.Value, cubreFlags) && (!selection.HasValue || selection.Value.Value.Count < kv.Value.Count))
				{
					selection = kv;
				}
			}
			if (selection.HasValue)
			{
				return selection.Value.Key;
			}
			return null;
		}

		private bool Matches(List<RopaCubre> value, RopaCubre cubreFlags)
		{
			foreach (RopaCubre f in value)
			{
				if ((cubreFlags & f) == 0)
				{
					return false;
				}
			}
			return true;
		}

		private void OnClothRemoveRequest(ClothFeature.ClothRemovalRequest req)
		{
			if (req.Cloth.transform.IsChildOf(base.Session.Player.GameObject.transform) || IsConversationActive())
			{
				return;
			}
			logger.Info("Undress {0}", ((BaseGlobalUserData)req.Cloth.dataDeRopa).stringId);
			RopaCubre cubreFlags = req.Cloth.dataDeRopa.cubreFlag;
			string slot = MapSlot(cubreFlags);
			if (slot == null || !animationMap.TryGetValue(slot, out var targetAnimation))
			{
				logger.Error("Unmapped RopaPreSetId {0} {1}", ((BaseGlobalUserData)req.Cloth.dataDeRopa).stringId, cubreFlags);
			}
			else if (interactionManager.CurrentPosture != null)
			{
				if (interactionManager.AnimationController.ResolveClip(targetAnimation) == null)
				{
					logger.Error("Unable to resolve animation clip for {0}", targetAnimation);
				}
				else
				{
					req.Intercepted = true;
					pendingRequests.Add(req);
				}
			}
		}

		private bool IsConversationActive()
		{
			return DialogueManager.IsConversationActive;
		}
	}

	public override bool Enabled => true;

	public override void OnStart()
	{
		base.OnStart();
		Lookup<StoryManager>().StoryInterviewServices.Add(() => new AnimateUndressService());
	}
}
