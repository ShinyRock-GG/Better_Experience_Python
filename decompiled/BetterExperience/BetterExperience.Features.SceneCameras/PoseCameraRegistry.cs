using System.Collections.Generic;
using System.Linq;
using BetterExperience.GameScopes;
using BetterExperience.Utils;

namespace BetterExperience.Features.SceneCameras;

internal class PoseCameraRegistry
{
	private Logger logger = new Logger();

	private PersistenceService persistenceService;

	private SceneCameraSettingsNg settings;

	public PoseCameraRegistry(PersistenceService persistenceService)
	{
		this.persistenceService = persistenceService;
		settings = persistenceService.Persisted(() => new SceneCameraSettingsNg());
		foreach (List<CameraSettings> cameras in settings.PoseCameras.Values)
		{
			cameras.Sort((CameraSettings a, CameraSettings b) => a.Name.CompareTo(b.Name));
		}
	}

	private string GuessNextName(string pose)
	{
		List<string> names = (from x in settings.PoseCameras.GetValueOrDefault(pose, () => new List<CameraSettings>())
			select x.Name).ToList();
		for (int i = names.Count; i < 100; i++)
		{
			string name = pose + "_" + i;
			if (!names.Contains(name))
			{
				return name;
			}
		}
		return null;
	}

	public void Save(string pose, CameraSettings camera)
	{
		pose = Extensions.GetValueOrDefault(settings.PoseToPose, pose, pose);
		if (camera.Name == null)
		{
			camera.Name = GuessNextName(pose);
		}
		if (camera.Name == null)
		{
			logger.Error("Unable to guess filename for pose {0}", pose);
			return;
		}
		List<CameraSettings> cameras = settings.PoseCameras.GetValueOrAdd(pose, () => new List<CameraSettings>());
		CameraSettings prev = cameras.Find((CameraSettings cam) => cam.Name == camera.Name);
		if (prev != null)
		{
			cameras.Remove(prev);
		}
		cameras.Add(camera);
		cameras.Sort((CameraSettings a, CameraSettings b) => a.Name.CompareTo(b.Name));
		persistenceService.Persist(settings);
	}

	public void Delete(string pose, CameraSettings camera)
	{
		pose = Extensions.GetValueOrDefault(settings.PoseToPose, pose, pose);
		if (camera == null || camera.Name == null)
		{
			return;
		}
		List<CameraSettings> cams = settings.PoseCameras.GetValueOrDefault(pose, () => (List<CameraSettings>)null);
		if (cams != null)
		{
			CameraSettings existing = cams.Find((CameraSettings x) => x.Name == camera.Name);
			if (existing != null)
			{
				cams.Remove(existing);
				persistenceService.Persist(settings);
			}
		}
	}

	public List<CameraSettings> GetCamerasForPose(string pose)
	{
		pose = Extensions.GetValueOrDefault(settings.PoseToPose, pose, pose);
		return settings.PoseCameras.GetValueOrDefault(pose, () => (List<CameraSettings>)null);
	}

	internal CameraSettings NextCameraForPose(string pose, CameraSettings current)
	{
		List<CameraSettings> cameras = GetCamerasForPose(pose);
		if (cameras == null)
		{
			return null;
		}
		if (current == null && cameras.Count > 0)
		{
			return cameras[0];
		}
		int idx = cameras.IndexOf(current);
		if (idx < 0)
		{
			return null;
		}
		if (idx + 1 >= cameras.Count)
		{
			return null;
		}
		return cameras[idx + 1];
	}
}
