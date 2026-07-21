using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.CustomScene.Monkey;

public class MonkeyHelper : MonoBehaviour
{
	private class SeqPlayState
	{
		private static readonly Logger logger = Logger.Create<SeqPlayState>();

		public string Seq { get; }

		public List<List<PoseAnimationClip>> Clips { get; private set; } = new List<List<PoseAnimationClip>>();

		public int Index { get; private set; }

		public PoseAnimationClip CurrentClip
		{
			get
			{
				List<PoseAnimationClip> tmp = null;
				if (Index >= 0 && Index < Clips.Count)
				{
					tmp = Clips[Index];
				}
				if (tmp != null && tmp.Count > 0)
				{
					return tmp.RandomItem();
				}
				return null;
			}
		}

		public SeqPlayState(string cmd, string[] request, POIPosture targetPosture)
		{
			Seq = cmd;
			foreach (string id in request)
			{
				List<PoseAnimationClip> result = targetPosture.Poses.FindClips(id);
				if (result.Count == 0)
				{
					logger.Error("No clips match ID {0}", id);
				}
				else
				{
					Clips.Add(result);
				}
			}
		}

		public void NextIndex()
		{
			Index++;
			if (Index >= Clips.Count)
			{
				Index = 0;
			}
		}
	}

	private Logger logger = Logger.Create<MonkeyHelper>();

	private DispatcherService dispatcher;

	private ScopeSupport scope;

	private SeqPlayState seqPlayState;

	private InteractionManager InteractionManager { get; set; }

	private POIManager poiManager { get; set; }

	private PoseManager PoseManager { get; set; }

	public void Configure(ScopeSupport scope)
	{
		InteractionManager = scope.Lookup<InteractionManager>();
		poiManager = scope.Lookup<POIManager>();
		PoseManager = scope.Lookup<PoseManager>();
		dispatcher = scope.Lookup<DispatcherService>();
		this.scope = scope;
	}

	public void Play(string cmd)
	{
		if (cmd == null)
		{
			logger.Error("Null request");
			return;
		}
		string[] request = cmd.Split(new char[1] { ';' });
		if (request.Length == 0)
		{
			logger.Error("Empty request");
			return;
		}
		EnforceStaningPosturesExistance();
		for (int i = 0; i < request.Length; i++)
		{
			request[i] = request[i].Trim();
		}
		PoseAnimationClip targetClip = null;
		string postureId = request[0];
		POIPosture targetPosture = PoseManager.FindPOIPostureById(postureId);
		if (targetPosture == null)
		{
			logger.Error("Posture {0} not found", postureId);
			return;
		}
		if (request.Length > 1)
		{
			string[] poseIds = request.Skip(1).ToArray();
			List<PoseAnimationClip> pacs = new List<PoseAnimationClip>();
			string[] array = poseIds;
			foreach (string id in array)
			{
				List<PoseAnimationClip> result = targetPosture.Poses.FindClips(id);
				if (result.Count == 0)
				{
					logger.Error("No clips match ID {0}", id);
				}
				else
				{
					pacs.AddRange(result);
				}
			}
			if (pacs.Count > 0)
			{
				targetClip = pacs.RandomItem();
			}
		}
		if (seqPlayState != null)
		{
			seqPlayState = null;
			logger.Info("Clear existing seqstate");
		}
		dispatcher.StartCoroutine(ExecuteTransition(targetPosture, targetClip), scope);
	}

	private IEnumerator ExecuteTransition(POIPosture posture, PoseAnimationClip clip)
	{
		if (InteractionManager.HasActiveInteraction)
		{
			logger.Error("Cannot perform transition: interaction is in progress");
			yield break;
		}
		if (InteractionManager.CurrentPlace.POI.Id != posture.PoiId)
		{
			logger.Info("Changing poi to {0}", posture.PoiId);
			PointOfInterest poi = poiManager.FindPOI(posture.PoiId);
			InteractionManager.InteractionQueryContext ctx = InteractionManager.CreateQueryContext();
			Interaction i = new Interaction();
			i.SourcePosture = ctx.CurrentPosture;
			i.TargetPosture = posture;
			i.Enqueue(new GotoOp(poi, PoseOrientation.FRONT));
			if (!InteractionManager.StartInteraction(i))
			{
				logger.Error("Failed to start interaction");
				yield break;
			}
			while (InteractionManager.HasActiveInteraction)
			{
				yield return null;
			}
			logger.Info("POI change complete");
			yield return new WaitForSeconds(0.1f);
		}
		Interaction change = InteractionManager.CreatePostureChangeInteraction(InteractionManager.CreateQueryContext(), posture);
		if (change != null)
		{
			logger.Info("Changing posture to {0}", posture.Id);
			if (!InteractionManager.StartInteraction(change))
			{
				logger.Error("Failed to start interaction");
				yield break;
			}
			while (InteractionManager.HasActiveInteraction)
			{
				yield return null;
			}
			logger.Info("Posture change complete");
			yield return new WaitForSeconds(0.1f);
		}
		if (posture.Poses.Posture != PoseManager.StandingPosture && clip == null)
		{
			List<PoseAnimationClip> idles = posture.Poses.FindClips("Idle");
			if (idles.Count == 0)
			{
				idles = posture.Poses.FindClips("Binding");
			}
			if (idles.Count != 0)
			{
				clip = idles.RandomItem();
			}
		}
		if (clip != null)
		{
			Interaction i2 = InteractionManager.CreatePlayClipInteraction(InteractionManager.CreateQueryContext(), clip.Name, new List<PoseAnimationClip>(new PoseAnimationClip[1] { clip }));
			if (i2 != null && !InteractionManager.StartInteraction(i2))
			{
				logger.Error("Failed to start interaction");
			}
			yield break;
		}
		List<PoseAnimationClip> idles2 = PoseManager.StandingPosture.Poses.IdlePoses;
		clip = idles2[0];
		Interaction i3 = InteractionManager.CreatePlayClipInteraction(InteractionManager.CreateQueryContext(), clip.Name, new List<PoseAnimationClip>(new PoseAnimationClip[1] { clip }));
		if (i3 != null && !InteractionManager.StartInteraction(i3))
		{
			logger.Error("Failed to start interaction");
		}
	}

	private void EnforceStaningPosturesExistance()
	{
		foreach (PointOfInterest poi in poiManager.Points)
		{
			PoseManager.StandingPostureAt(poi);
		}
	}

	public void SeqPlay(string cmd)
	{
		if (cmd == null)
		{
			logger.Error("Null request");
			return;
		}
		string[] request = cmd.Split(new char[1] { ';' });
		if (request.Length == 0)
		{
			logger.Error("Empty request");
			return;
		}
		EnforceStaningPosturesExistance();
		for (int i = 0; i < request.Length; i++)
		{
			request[i] = request[i].Trim();
		}
		string postureId = request[0];
		POIPosture targetPosture = PoseManager.FindPOIPostureById(postureId);
		if (targetPosture == null)
		{
			logger.Error("Posture {0} not found", postureId);
			return;
		}
		if (seqPlayState != null && seqPlayState.Seq != cmd)
		{
			seqPlayState = null;
		}
		if (seqPlayState == null)
		{
			seqPlayState = new SeqPlayState(cmd, request.Skip(1).ToArray(), targetPosture);
			logger.Info("New seqstate");
		}
		else
		{
			seqPlayState.NextIndex();
			logger.Info("Fwd seqstate");
		}
		if (seqPlayState.Clips.Count == 0)
		{
			logger.Error("No clips resolved");
			return;
		}
		logger.Info("Index {0}/{1}", seqPlayState.Index, seqPlayState.Clips.Count);
		PoseAnimationClip targetClip = seqPlayState.CurrentClip;
		if (targetClip != null)
		{
			dispatcher.StartCoroutine(ExecuteTransition(targetPosture, targetClip), scope);
		}
	}
}
