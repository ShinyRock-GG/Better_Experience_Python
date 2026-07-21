using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene;

public class PoseManager : StoryService
{
	public static readonly string STANDING_POSTURE_ID = "Stand";

	private POIManager waypointManager;

	private Repository<PostureData> postures = new Repository<PostureData>("pos", "postures", "postures");

	private Repository<PostureDescriptor> descriptors = new Repository<PostureDescriptor>("pod", "postures", "posture descriptors");

	private Repository<PoseAnimationClipData> clips = new Repository<PoseAnimationClipData>("pac", "poses", "clips");

	private Repository<InteractionDescriptor> clipDesc = new Repository<InteractionDescriptor>("pad", "poses", "clip descriptors");

	public Posture StandingPosture { get; private set; }

	public Observable PosturesChanged { get; } = new Observable();

	public Observable POIPosturesChanged { get; } = new Observable();

	public Observable<Posture> OnPosesChanged { get; } = new Observable<Posture>();

	public Dictionary<string, Posture> Postures { get; } = new Dictionary<string, Posture>();

	public Dictionary<string, POIPostureCollection> POIPostures { get; } = new Dictionary<string, POIPostureCollection>();

	public bool AnimatedIdles { get; set; }

	public override void OnInit()
	{
		base.OnInit();
		base.AsyncHandles.Add(postures.InitAsync(base.Story.VFS));
		base.AsyncHandles.Add(descriptors.InitAsync(base.Story.VFS));
		base.AsyncHandles.Add(clips.InitAsync(base.Story.VFS));
		base.AsyncHandles.Add(clipDesc.InitAsync(base.Story.VFS));
	}

	public override void OnStart()
	{
		base.OnStart();
		waypointManager = Lookup<POIManager>();
		InitStandingPosture();
		LoadPostures();
		foreach (PointOfInterest poi in waypointManager.Points)
		{
			StandingPostureAt(poi);
		}
		if (StandingPosture.Poses.IdlePoses.Count == 0)
		{
			StandingPosture.Poses.IdlePoses.Add(StandingPosture.Poses.PostureClip);
		}
		AnimatedIdles = !StandingPosture.Poses.IdlePoses[0].IsNativeIdle;
	}

	private void InitStandingPosture()
	{
		StandingPosture = new Posture();
		StandingPosture.Id = STANDING_POSTURE_ID;
		StandingPosture.Name = STANDING_POSTURE_ID;
		StandingPosture.Orientation = PoseOrientation.UNIVERSAL;
		StandingPosture.Configuration = new BoneConfiguration();
		StandingPosture.Poses = new PosturePoseCollection(StandingPosture);
		StandingPosture.Poses.PostureClip.IsNativeIdle = true;
		Postures[StandingPosture.Id] = StandingPosture;
	}

	private void LoadPostures()
	{
		List<PostureData> postureDatas = postures.All().ToList();
		foreach (PostureData pdata in postureDatas)
		{
			if (!pdata.Id.Contains("."))
			{
				LoadPosture(pdata.Id, pdata);
			}
		}
		foreach (PostureDescriptor pd in descriptors.All())
		{
			if (postures.Get(pd.Id) != null)
			{
				continue;
			}
			if (pd.ParentPosture == null)
			{
				logger.Error("Missing ParentPosture at transient {0}", pd.Id);
				continue;
			}
			PostureData pdata2 = postures.Get(pd.ParentPosture);
			if (pdata2 == null)
			{
				logger.Error("Missing posture {0} referenced by {1}", pd.ParentPosture, pd.Id);
			}
			else
			{
				LoadPosture(pd.Id, pdata2);
			}
		}
		LoadPosturePoses(StandingPosture);
		foreach (PostureData pdata3 in postureDatas)
		{
			if (pdata3.Id.Contains("."))
			{
				LoadPosture(pdata3.Id, pdata3);
			}
		}
		postures.Unload();
	}

	public List<PoseAnimationClip> FindClips(string targetAnimation)
	{
		List<PoseAnimationClip> result = new List<PoseAnimationClip>();
		foreach (Posture posture in Postures.Values)
		{
			List<PoseAnimationClip> t = posture.Poses.FindClips(targetAnimation);
			foreach (PoseAnimationClip x in t)
			{
				if (x.UniqueName.StartsWith(posture.Id + "."))
				{
					result.Add(x);
				}
			}
		}
		return result;
	}

	internal void InvalidateClips(Posture posture, string name, InteractionDescriptor desc)
	{
		List<PoseAnimationClip> clips = posture.Poses.AllClips.Values.Where((PoseAnimationClip x) => x.Name == name).ToList();
		foreach (PoseAnimationClip c in clips)
		{
			posture.Poses.AddClip(c, desc);
		}
		OnPosesChanged.Invoke(posture.Poses.Posture);
	}

	internal void SaveDescriptor(InteractionDescriptor desc)
	{
		clipDesc.Save(desc);
	}

	private void LoadPosture(string id, PostureData data)
	{
		logger.Debug("Loading posture {0}", id);
		string[] parts = id.Split(new char[1] { '.' });
		Posture posture = data.ToPosture(id);
		posture.Id = id;
		posture.Descriptor = descriptors.Get(id);
		if (parts.Length == 1)
		{
			if (posture.Descriptor == null)
			{
				posture.Descriptor = new PostureDescriptor();
				posture.Descriptor.Id = id;
			}
			if (posture.Id == STANDING_POSTURE_ID)
			{
				StandingPosture = posture;
				logger.Debug("Overriding standing posture");
			}
			Postures[posture.Id] = posture;
			if (posture.Id != STANDING_POSTURE_ID)
			{
				LoadPosturePoses(posture);
			}
			return;
		}
		string postureid = parts[0];
		string poiId = parts[1];
		PointOfInterest poi = waypointManager.FindPOI(poiId);
		if (poi == null)
		{
			logger.Error("Poi {0} not found for {1}", poiId, id);
			return;
		}
		POIPosture poiPosture = (POIPosture)posture;
		poiPosture.Descriptor = descriptors.Get(id);
		if (poiPosture.Descriptor == null)
		{
			logger.Warn("Posture descriptor {0} is missing", id);
			poiPosture.Descriptor = new PostureDescriptor();
			poiPosture.Descriptor.DisplayName = posture.Name;
			poiPosture.Descriptor.CancelDisplayName = "Cancel " + posture.Name;
		}
		POIPostureCollection collection = POIPostures.GetValueOrAdd(poiId, () => new POIPostureCollection());
		if (collection.ExactPostures.Count == 0)
		{
			CreateStandingPosture(poi, collection);
		}
		if (Postures.ContainsKey(postureid))
		{
			collection.Add(Postures[postureid], poiPosture);
			return;
		}
		logger.Error("Posture {0} is not registered", postureid);
	}

	private void LoadPosturePoses(Posture posture)
	{
		List<string> prefixes = ResolveCompatiblePrefixes(posture);
		foreach (PoseAnimationClipData animationData in clips.All())
		{
			if (!StartsWithPrefix(animationData.Id, prefixes))
			{
				continue;
			}
			PoseAnimationClip clip = animationData.ToClip(animationData.Id, this);
			if (clip == null)
			{
				continue;
			}
			InteractionDescriptor descriptor = null;
			foreach (string pid in prefixes)
			{
				descriptor = clipDesc.Get(pid + "." + clip.Name);
				if (descriptor != null)
				{
					logger.Debug("Using descriptor {0} for clip {1}", descriptor.Id, clip.UniqueName);
					break;
				}
			}
			if (descriptor == null && prefixes.Count > 0)
			{
				if (descriptor != null)
				{
					logger.Debug("Loaded descriptor for {0}", clip.UniqueName);
				}
				else
				{
					descriptor = new InteractionDescriptor();
					logger.Debug("Unsing default descriptor for {0}", clip.UniqueName);
				}
			}
			posture.Poses.AddClip(clip, descriptor);
		}
	}

	internal void SaveDescriptor(PostureDescriptor descriptor)
	{
		descriptors.Save(descriptor);
	}

	private bool StartsWithPrefix(string id, IReadOnlyList<string> prefixes)
	{
		foreach (string s in prefixes)
		{
			if (id.StartsWith(s + "."))
			{
				return true;
			}
		}
		return false;
	}

	private List<string> ResolveCompatiblePrefixes(Posture posture)
	{
		List<string> prefixes = new List<string>();
		prefixes.Add(posture.Id);
		while (posture != null && posture.Descriptor != null && posture.Descriptor.ParentPosture != null)
		{
			string ppid = posture.Descriptor.ParentPosture;
			if (Postures.TryGetValue(ppid, out posture))
			{
				prefixes.Add(posture.Id);
			}
			else
			{
				posture = null;
			}
		}
		return prefixes;
	}

	public POIPosture StandingPostureAt(PointOfInterest currentPlace)
	{
		if (!POIPostures.TryGetValue(currentPlace.Id, out var postures))
		{
			postures = new POIPostureCollection();
			POIPostures[currentPlace.Id] = postures;
		}
		return CreateStandingPosture(currentPlace, postures);
	}

	private POIPosture CreateStandingPosture(PointOfInterest poi, POIPostureCollection postures)
	{
		string id = StandingPosture.Id + "." + poi.Id;
		if (!postures.ExactPostures.TryGetValue(id, out var posture))
		{
			posture = new POIPosture();
			posture.Id = id;
			posture.Name = id;
			posture.Orientation = PoseOrientation.UNIVERSAL;
			posture.Poses = new PosturePoseCollection(StandingPosture);
			posture.Configuration = new BoneConfiguration();
			posture.Configuration.RootRotation = Quaternion.AngleAxis(-90f, new Vector3(1f, 0f, 0f));
			postures.Add(StandingPosture, posture);
		}
		return posture;
	}

	public POIPosture FindPOIPostureById(string postureId)
	{
		string[] parts = postureId.Split(new char[1] { '.' });
		if (parts.Length != 3 && parts.Length != 2)
		{
			logger.Error("Unexpected posture ID {0}", postureId);
			return null;
		}
		if (POIPostures.TryGetValue(parts[1], out var collection))
		{
			if (collection.ExactPostures.TryGetValue(postureId, out var posture))
			{
				return posture;
			}
			logger.Error("Unable to find poi-posture {0} at posture {1}", postureId, parts[0]);
		}
		else
		{
			logger.Error("No postures for POI {0} found. Registered POIs: {1}", parts[0], string.Join(",", POIPostures.Keys));
		}
		return null;
	}

	internal void WritePose(PoseAnimationClip currentClip, bool asTransition = false)
	{
		PoseAnimationClipData data = new PoseAnimationClipData(currentClip);
		data.Id = data.UniqueName;
		clips.Save(data);
		string descriptorId = currentClip.Posture.Id + "." + currentClip.Name;
		InteractionDescriptor descriptor = clipDesc.Get(descriptorId);
		if (descriptor == null)
		{
			Repository<InteractionDescriptor> repository = clipDesc;
			InteractionDescriptor obj = new InteractionDescriptor
			{
				Id = descriptorId,
				DisplayName = currentClip.Name,
				CancelDisplayName = "Un-" + currentClip.Name,
				Type = ((!asTransition) ? InteractionType.pose : InteractionType.transition)
			};
			descriptor = obj;
			repository.Save(obj);
		}
		SeqClipConverter.CLIP_CACHE.Clear();
		currentClip.Posture.Poses.AddClip(currentClip, descriptor);
		OnPosesChanged.Invoke(currentClip.Posture.Poses.Posture);
	}

	public void CreatePosture(Posture p)
	{
		if (!Postures.ContainsKey(p.Id))
		{
			PostureData data = new PostureData(p);
			postures.Save(data);
			LoadPosture(p.Id, data);
			PosturesChanged.Invoke();
		}
	}

	public void RemovePosture(Posture p)
	{
		if (p is POIPosture pp)
		{
			if (POIPostures.TryGetValue(pp.PoiId, out var coll) && coll.Contains(pp))
			{
				coll.Remove(pp);
				PostureData pd = new PostureData(pp);
				postures.Delete(pd);
				POIPosturesChanged.Invoke();
			}
		}
		else if (Postures.ContainsKey(p.Id))
		{
			Postures.Remove(p.Id);
			postures.Delete(new PostureData(p));
			PosturesChanged.Invoke();
		}
	}

	public void CreatePoiPosture(PointOfInterest poi, Posture p, Posture poiPosture)
	{
		string id = poiPosture.Id;
		PostureData pd = new PostureData(poiPosture);
		postures.Save(pd);
		if (descriptors.Get(id) == null)
		{
			PostureDescriptor pdd = new PostureDescriptor();
			pdd.Id = id;
			descriptors.Save(pdd);
		}
		LoadPosture(id, pd);
		POIPosturesChanged.Invoke();
	}

	internal void UpdatePosture(POIPosture poiPosture)
	{
		PostureData pd = new PostureData(poiPosture);
		postures.Save(pd);
		POIPosturesChanged.Invoke();
	}
}
