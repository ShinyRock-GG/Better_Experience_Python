using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BetterExperience.CustomScene;

namespace BetterExperience.CustomScene.Packaging;

public class Repository<T> where T : Stored
{
	private class DataEntry<T>
	{
		public T Value { get; set; }

		public List<VirtIOAccessor> Accessors { get; }

		public VirtIOAccessor MainAccessor { get; }

		public VirtIOAccessor WriteAccessor { get; }

		public DataEntry(List<VirtIOAccessor> accessors, VirtIOAccessor writeAccessor)
		{
			Accessors = accessors;
			MainAccessor = accessors.Last();
			WriteAccessor = writeAccessor;
		}

		public DataEntry(VirtIOAccessor accessor)
		{
			Accessors = new List<VirtIOAccessor>();
			Accessors.Add(accessor);
			MainAccessor = accessor;
			WriteAccessor = accessor;
		}
	}

	private Dictionary<string, DataEntry<T>> data = new Dictionary<string, DataEntry<T>>();

	private string defaultDir;

	private string ext;

	private string displayName;

	private VirtIO root;

	private Task asyncHandle;

	protected readonly Logger logger;

	public Repository(string extension, string defaultDir, string displayName)
	{
		logger = new Logger(GetType());
		this.defaultDir = defaultDir;
		ext = "." + extension;
		this.displayName = displayName;
	}

	public AsyncTask InitAsync(VirtIO root)
	{
		if (this.root != null)
		{
			throw new InvalidOperationException("Already initialized");
		}
		AsyncTaskProgress progress = new AsyncTaskProgress();
		this.root = root;
		asyncHandle = Task.Run(delegate
		{
			InitDataStore(progress);
		});
		return new AsyncTask("Initializing " + displayName, asyncHandle, progress);
	}

	private unsafe void InitDataStore(AsyncTaskProgress progress)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
			logger.Info("[SceneLog] Repository<{0}> InitDataStore starting", typeof(T).Name);
		MeasureTime val = MeasureTime.Create(logger, (Func<long, string>)((long time) => $"Repository {typeof(T).Name} loading took {time}ms"), true);
		try
		{
			InitDataStoreImpl(progress);
			if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
				logger.Info("[SceneLog] Repository<{0}> InitDataStore complete", typeof(T).Name);
		}
		catch (Exception ex)
		{
			logger.Error("[SceneLog] Repository<{0}> InitDataStore EXCEPTION: {1}", typeof(T).Name, ex);
			throw;
		}
		finally
		{
			((IDisposable)(*(MeasureTime*)(&val))/*cast due to constrained. prefix*/).Dispose();
		}
	}

	private void InitDataStoreImpl(AsyncTaskProgress progress)
	{
		if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
			logger.Info("[SceneLog] Repository<{0}> enumerating VFS (ext={1})", typeof(T).Name, ext);
		List<VirtIOEntry> entries = new List<VirtIOEntry>();
		foreach (VirtIOEntry e in root.Enumerate())
		{
			if (e.Name.EndsWith(ext) && e.Accessors.Count > 0)
			{
				entries.Add(e);
			}
		}
		if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
			logger.Info("[SceneLog] Repository<{0}> found {1} file(s)", typeof(T).Name, entries.Count);
		progress.Report(0, entries.Count);
		for (int i = 0; i < entries.Count; i++)
		{
			VirtIOEntry e2 = entries[i];
			string id = e2.Name.Substring(0, e2.Name.Length - ext.Length);
			if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
				logger.Info("[SceneLog] Repository<{0}> loading [{1}/{2}]: {3}", typeof(T).Name, i + 1, entries.Count, e2.Name);
			DataEntry<T> entry = new DataEntry<T>(e2.Accessors, e2.WriteAccessor);
			entry.Value = entry.MainAccessor.Persisted(() => (T)null);
			if (entry.Value != null)
			{
				entry.Value.Id = id;
			}
			if (data.ContainsKey(id))
			{
				string path1 = data[id].MainAccessor.ToString() + "/" + e2.Name;
				string path2 = entry.MainAccessor.ToString() + "/" + e2.Name;
				logger.Error("Duplicate asset found. Id: {0}. Path 1: {1}. Path 2: {2}", id, path1, path2);
				SceneWarnings.Instance.Report("Duplicate asset found. Id: {0}. Path 1: {1}. Path 2: {2}", id, path1, path2);
			}
			data[id] = entry;
			progress.Report(i + 1, entries.Count);
		}
	}

	private void EnsureLoaded()
	{
		if (asyncHandle != null && asyncHandle.IsCompleted)
		{
			asyncHandle.Wait();
		}
	}

	public IEnumerable<T> All()
	{
		EnsureLoaded();
		foreach (KeyValuePair<string, DataEntry<T>> datum in data)
		{
			T d = Get(datum.Key);
			if (d != null)
			{
				yield return d;
			}
		}
	}

	public T Get(string id)
	{
		EnsureLoaded();
		if (data.TryGetValue(id, out var a) && !a.Value.Deleted)
		{
			return a.Value;
		}
		return null;
	}

	public void Save(T poi)
	{
		EnsureLoaded();
		string fsid = poi.Id + ext;
		if (!data.TryGetValue(poi.Id, out var accessor))
		{
			DataEntry<T> dataEntry = (data[poi.Id] = new DataEntry<T>(new VirtIOAccessor(root.Dir(defaultDir), fsid)));
			accessor = dataEntry;
		}
		accessor.Value = poi;
		lock (accessor.WriteAccessor)
		{
			accessor.WriteAccessor.Persist(poi);
		}
	}

	public Task SaveAsync(T poi)
	{
		EnsureLoaded();
		string fsid = poi.Id + ext;
		if (!data.TryGetValue(poi.Id, out var accessor))
		{
			DataEntry<T> dataEntry = (data[poi.Id] = new DataEntry<T>(new VirtIOAccessor(root.Dir(defaultDir), fsid)));
			accessor = dataEntry;
		}
		accessor.Value = poi;
		return Task.Run(delegate
		{
			lock (accessor.WriteAccessor)
			{
				accessor.WriteAccessor.Persist(poi);
			}
		});
	}

	internal void Delete(T obj)
	{
		EnsureLoaded();
		obj.Deleted = true;
		if (data.TryGetValue(obj.Id, out var accessor))
		{
			lock (accessor.WriteAccessor)
			{
				accessor.WriteAccessor.PersistOrDelete(obj);
			}
			accessor.Value = obj;
		}
	}

	public void Unload()
	{
		EnsureLoaded();
		data.Clear();
	}
}
