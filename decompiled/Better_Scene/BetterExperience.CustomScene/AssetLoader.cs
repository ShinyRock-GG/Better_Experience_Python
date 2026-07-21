using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.ScenaManagers;
using Assets.Base.BeachGirl.HDRP.Runtime;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.GameScopes;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace BetterExperience.CustomScene;

internal class AssetLoader : StoryService
{
	private class ComponentPath
	{
		private string fieldName;

		private string componentName;

		private List<string> goPath;

		private GameObject targetGO;

		private object targetComponent;

		private Type accessorType;

		private Traverse accessor;

		public bool Valid { get; private set; }

		public Type TargetType => accessorType;

		public ComponentPath(string path)
		{
			List<string> components = path.Split(new char[1] { '/' }).ToList();
			if (components.Count < 2)
			{
				Valid = false;
				return;
			}
			fieldName = components.Last();
			components = components.GetRange(0, components.Count - 1);
			if (components.Last().StartsWith("@"))
			{
				componentName = components.Last().Substring(1);
				components = components.GetRange(0, components.Count - 1);
			}
			goPath = components;
			if (!ResolveGO())
			{
				Valid = false;
			}
			else if (!ResolveTargetComponent())
			{
				Valid = false;
			}
			else if (!ResolveReflectionTarget())
			{
				Valid = false;
			}
			else
			{
				Valid = true;
			}
		}

		private bool ResolveGO()
		{
			GameObject go = null;
			for (int i = 0; i < goPath.Count; i++)
			{
				string goname = goPath[i];
				if (go == null)
				{
					go = GameObject.Find(goname);
					if (go == null)
					{
						Logger.Global.Error("Unable to find GO by path {0}", string.Join("/", goPath));
						return false;
					}
					continue;
				}
				Transform c = go.transform.FindDeepChild(goname);
				if (c == null)
				{
					Logger.Global.Error("Unable to find GO by path {0}. Failure at {1}", string.Join("/", goPath), goname);
					return false;
				}
				go = c.gameObject;
			}
			if (go == null)
			{
				Logger.Global.Error("Unable to find GO by path {0}", string.Join("/", goPath));
				return false;
			}
			targetGO = go;
			return true;
		}

		private bool ResolveTargetComponent()
		{
			if (componentName == null)
			{
				targetComponent = targetGO;
				return true;
			}
			foreach (Component c in targetGO.transform)
			{
				if (c.GetType().Name == componentName || c.GetType().FullName == componentName)
				{
					targetComponent = c;
					return true;
				}
			}
			Logger.Global.Error("Unable to find component {0} at {1}", componentName, string.Join("/", goPath));
			return false;
		}

		private bool ResolveReflectionTarget()
		{
			Traverse root = Traverse.Create(targetComponent);
			Traverse asField = root.Field(fieldName);
			if (asField.FieldExists())
			{
				accessor = asField;
			}
			else
			{
				Traverse asProperty = root.Property(fieldName, (object[])null);
				if (asProperty.PropertyExists())
				{
					accessor = asProperty;
				}
			}
			if (accessor != null)
			{
				accessorType = accessor.GetValueType();
				return true;
			}
			Logger.Global.Error("Unable to find reflection target {0} at {1}", fieldName, string.Join("/", goPath));
			return false;
		}

		internal void SetValue(object target)
		{
			if (accessor != null)
			{
				accessor.SetValue(target);
			}
		}
	}

	public class SceneDef : Stored
	{
		public List<SceneOperation> sequence { get; set; } = new List<SceneOperation>();

		public List<SceneOperation> interview_sequence { get; set; } = new List<SceneOperation>();

		public Dictionary<string, List<SceneOperation>> on_scene_load { get; set; } = new Dictionary<string, List<SceneOperation>>();

		public List<string> include_before { get; set; } = new List<string>();

		public List<string> include_after { get; set; } = new List<string>();
	}

	public class SceneOperation
	{
		public bool disabled { get; set; }

		public string type { get; set; }

		public string name { get; set; }

		public string target { get; set; }

		public object value { get; set; }

		public float[] pos { get; set; }

		public float[] euler { get; set; }

		public float[] quat { get; set; }
	}

	public class AsyncWrapper
	{
		public AsyncOperation Operation { get; }

		public string Description { get; }

		public AsyncWrapper(AsyncOperation operation, string description)
		{
			Operation = operation;
			Description = description;
		}
	}

	private DataRepository assets = new DataRepository("bundle", "assets");

	private Repository<SceneDef> scenes = new Repository<SceneDef>("scene", "scenes", "scenes");

	private bool dirtyLightprobes;

	private Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();

	private Dictionary<string, Func<SceneOperation, IEnumerator>> opHandlers = new Dictionary<string, Func<SceneOperation, IEnumerator>>();

	private List<GameObject> loadedGameObjects = new List<GameObject>();

	private List<Scene> loadedScenes = new List<Scene>();

	private bool loadingSceneNow;

	public Observable BeginSceneLoading { get; } = new Observable();

	public override void OnInit()
	{
		base.OnInit();
		assets.Init(base.Story.VFS);
		base.AsyncHandles.Add(scenes.InitAsync(base.Story.VFS));
	}

	public override void OnStart()
	{
		base.OnStart();
		LightProbes.needsRetetrahedralization += LightProbes_needsRetetrahedralization;
		LightProbes.tetrahedralizationCompleted += LightProbes_tetrahedralizationCompleted;
		SceneManager.sceneLoaded += SceneManager_sceneLoaded;
		SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
		RegisterOperationHandler("load_scene", LoadUnityScene, base.Scope);
		RegisterOperationHandler("unload_scene", UnloadUnityScene, base.Scope);
		RegisterOperationHandler("apply_volume_profile", ApplyHdrpProfile, base.Scope);
		RegisterOperationHandler("apply_hdrp_profile", ApplyHdrpProfile, base.Scope);
		RegisterOperationHandler("dont_destroy", DontDestroyGO, base.Scope);
		RegisterOperationHandler("load_prefab", LoadPrefab, base.Scope);
		RegisterOperationHandler("disable_go", DisableGo, base.Scope);
		RegisterOperationHandler("destroy_go", DestroyGo, base.Scope);
		RegisterOperationHandler("set_value", SetProperty, base.Scope);
	}

	private void SceneManager_sceneUnloaded(Scene arg0)
	{
		loadedScenes.Remove(arg0);
	}

	private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
	{
		if (loadingSceneNow)
		{
			loadedScenes.Add(arg0);
		}
	}

	public void RegisterOperationHandler(string opcode, Func<SceneOperation, IEnumerator> handler, ScopeSupport scope)
	{
		opHandlers[opcode.ToLower()] = handler;
		if (scope == null)
		{
			return;
		}
		scope.OnDispose += delegate
		{
			if (opHandlers.TryGetValue(opcode.ToLower(), out var value) && value == handler)
			{
				opHandlers.Remove(opcode.ToLower());
			}
		};
	}

	public override void OnStop()
	{
		base.OnStop();
		LightProbes.needsRetetrahedralization -= LightProbes_needsRetetrahedralization;
		LightProbes.tetrahedralizationCompleted -= LightProbes_tetrahedralizationCompleted;
		SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
		SceneManager.sceneUnloaded -= SceneManager_sceneUnloaded;
		if (loadedScenes.Count == 0)
		{
			return;
		}
		foreach (Scene s in loadedScenes)
		{
			logger.Error("destroying scene {0}", s.name);
			SceneManager.UnloadScene(s);
		}
	}

	private void LightProbes_tetrahedralizationCompleted()
	{
		dirtyLightprobes = false;
	}

	private void LightProbes_needsRetetrahedralization()
	{
		dirtyLightprobes = true;
	}

	public IEnumerator LoadScene(string name, HashSet<string> referencedScenes = null)
	{
		if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
			logger.Info("[SceneLog] LoadScene: {0}", name);
		SceneDef scene = scenes.Get(name);
		if (scene == null)
		{
			logger.Error("Scene definition not exists: {0}", name);
			logger.Error("All registered scenes:");
			foreach (SceneDef x in scenes.All())
			{
				logger.Error("Scene: {0}", x.Id);
			}
			logger.Error("End of scenes");
			yield break;
		}
		if (referencedScenes == null)
		{
			loadingSceneNow = true;
			BeginSceneLoading.Invoke();
		}
		if (scene.include_before != null)
		{
			IEnumerator it = LoadSceneBatch(scene.include_before, new HashSet<string>(new string[1] { name }));
			while (it.MoveNext())
			{
				yield return it.Current;
			}
		}
		foreach (object item in ExecSeq(scene.sequence))
		{
			yield return item;
		}
		if (scene.include_after != null)
		{
			IEnumerator it = LoadSceneBatch(scene.include_after, new HashSet<string>(new string[1] { name }));
			while (it.MoveNext())
			{
				yield return it.Current;
			}
		}
		if (referencedScenes == null)
		{
			loadingSceneNow = false;
		}
	}

	public IEnumerator LoadSceneBatch(List<string> scenes, HashSet<string> referencedScenes)
	{
		foreach (string sid in scenes)
		{
			if (referencedScenes.Add(sid))
			{
				IEnumerator it = LoadScene(sid, referencedScenes);
				while (it.MoveNext())
				{
					yield return it.Current;
				}
			}
		}
	}

	internal IEnumerator SetupScene(string activeScene, string name)
	{
		SceneDef scene = scenes.Get(activeScene);
		if (scene == null)
		{
			logger.Error("Scene definition not exists: {0}", name);
			logger.Error("All registered scenes:");
			foreach (SceneDef x in scenes.All())
			{
				logger.Error("Scene: {0}", x.Id);
			}
			logger.Error("End of scenes");
		}
		else
		{
			if (scene.on_scene_load == null)
			{
				yield break;
			}
			if (scene.on_scene_load.TryGetValue(name, out var seq))
			{
				logger.Info("Execution scene tuning {0}@{1}", name, activeScene);
				foreach (object item in ExecSeq(seq))
				{
					yield return item;
				}
				logger.Info("Scene tuning complete");
			}
			else
			{
				logger.Info("No scene tuning for {0} at {1}", name, activeScene);
			}
		}
	}

	private IEnumerable ExecSeq(List<SceneOperation> ops)
	{
		foreach (SceneOperation op in ops)
		{
			if (op.disabled || op.type == null)
			{
				continue;
			}
			if (!opHandlers.TryGetValue(op.type.ToLower(), out var handler))
			{
				logger.Error("Unexpected scene operation type {0}", op.type);
				continue;
			}
			IEnumerator it = handler(op);
			if (it != null)
			{
				while (it.MoveNext())
				{
					yield return it.Current;
				}
			}
		}
		if (dirtyLightprobes)
		{
			yield return UpdateLightprobes();
		}
		if (loadedBundles.Count <= 0)
		{
			yield break;
		}
		foreach (AssetBundle ab in loadedBundles.Values)
		{
			yield return ab.UnloadAsync(unloadAllLoadedObjects: false);
		}
		loadedBundles.Clear();
	}

	private void InvalidateGotos(string name)
	{
		Scene scene = SceneManager.GetSceneByName(name);
		List<GoToScenaManager.GoTo> deadOnes = new List<GoToScenaManager.GoTo>();
		foreach (GoToScenaManager.GoTo gt in Singleton<GoToScenaManager>.instance.registrados)
		{
			if (gt.transform.gameObject.scene == scene)
			{
				deadOnes.Add(gt);
			}
		}
		foreach (GoToScenaManager.GoTo gt2 in deadOnes)
		{
			Singleton<GoToScenaManager>.instance.Remove(gt2);
		}
	}

	internal IEnumerator RunInterviewSequence(string sceneName)
	{
		SceneDef scene = scenes.Get(sceneName);
		if (scene == null)
		{
			logger.Error("Scene definition does not exist: {0}", sceneName);
			logger.Error("All registered scenes:");
			foreach (SceneDef x in scenes.All())
			{
				logger.Error("Scene: {0}", x.Id);
			}
			logger.Error("End of scenes");
			yield break;
		}
		foreach (object item in ExecSeq(scene.interview_sequence))
		{
			yield return item;
		}
	}

	private IEnumerator DisableGo(SceneOperation op)
	{
		string name = op.name;
		if (name == null)
		{
			logger.Error("[DisableGo] missing GO name");
			yield break;
		}
		GameObject go = GameObject.Find(name);
		if (go == null)
		{
			logger.Error("[DisableGo] missing GO {0}", name);
		}
		else
		{
			go.SetActive(value: false);
		}
	}

	private IEnumerator DestroyGo(SceneOperation op)
	{
		string name = op.name;
		if (name == null)
		{
			logger.Error("[DestroyGo] missing GO name");
			yield break;
		}
		GameObject go = GameObject.Find(name);
		if (go == null)
		{
			logger.Error("[DestroyGo] missing GO {0}", name);
		}
		else
		{
			UnityEngine.Object.DestroyImmediate(go);
		}
	}

	private IEnumerator DontDestroyGO(SceneOperation op)
	{
		string name = op.name;
		if (name == null)
		{
			logger.Error("[DontDestroyGO] missing GO name");
			yield break;
		}
		GameObject go = GameObject.Find(name);
		if (go == null)
		{
			logger.Error("[DontDestroyGO] missing GO {0}", name);
		}
		else
		{
			go.hideFlags |= HideFlags.HideAndDontSave;
			go.transform.parent = null;
			UnityEngine.Object.DontDestroyOnLoad(go);
		}
	}

	private WaitUntil UpdateLightprobes()
	{
		LightProbes.TetrahedralizeAsync();
		return new WaitUntil(() => dirtyLightprobes);
	}

	private IEnumerator ApplyHdrpProfile(SceneOperation op)
	{
		string name = op.name;
		if (name != null)
		{
			GameObject obj = GameObject.Find(name);
			if (obj != null)
			{
				Volume volume = obj.GetComponent<Volume>();
				if (volume != null)
				{
					Camera[] array = UnityEngine.Object.FindObjectsOfType<Camera>(includeInactive: true);
					foreach (Camera camera in array)
					{
						Volume cameraVolume = camera.GetComponentInChildren<Volume>();
						if (!(cameraVolume != null))
						{
							continue;
						}
						DiffusionProfileList diffusionProfileComponent = null;
						foreach (VolumeComponent c in cameraVolume.profile.components)
						{
							if (c is DiffusionProfileList dpl)
							{
								diffusionProfileComponent = dpl;
							}
						}
						cameraVolume.profile = volume.profile;
						cameraVolume.sharedProfile = volume.profile;
						if (diffusionProfileComponent != null)
						{
							if (!volume.profile.TryGet<DiffusionProfileList>(out var target))
							{
								target = volume.profile.Add<DiffusionProfileList>();
								target.diffusionProfiles.value = diffusionProfileComponent.diffusionProfiles.value;
							}
							if (!target.diffusionProfiles.overrideState)
							{
								target.diffusionProfiles.overrideState = true;
							}
							target.diffusionProfiles.value = diffusionProfileComponent.diffusionProfiles.value;
							if (cameraVolume.TryGetComponent<LoadExtraDiffuseProfiles>(out var diffuseProfileLoader))
							{
								UnityEngine.Object.DestroyImmediate(diffuseProfileLoader);
							}
							cameraVolume.gameObject.AddComponent<LoadExtraDiffuseProfiles>();
						}
					}
				}
				else
				{
					logger.Error("[ApplyHdrpProfile] Volume GO named {0} has no volume", name);
				}
			}
			else
			{
				logger.Error("[ApplyHdrpProfile] Volume GO named {0} not found", name);
			}
		}
		else
		{
			logger.Error("[ApplyHdrpProfile] Missing volume GO name");
		}
		yield break;
	}

	private IEnumerator UnloadUnityScene(SceneOperation op)
	{
		if (op.name == null)
		{
			logger.Error("[UnloadScene] Missing scene name parameter");
			yield break;
		}
		logger.Info("Unloading scene {0}", op.name);
		InvalidateGotos(op.name);
		yield return new AsyncWrapper(SceneManager.UnloadSceneAsync(op.name), "Unloading scene " + op.name);
	}

	private IEnumerator LoadUnityScene(SceneOperation op)
	{
		if (op.name == null)
		{
			logger.Error("[LoadScene] Missing scene name parameter");
			yield break;
		}
		string[] names = op.name.Split(new char[1] { ':' });
		if (names.Length != 2)
		{
			logger.Error("[LoadScene] Bad scene name {0}. assetbundle:scenename expected", op.name);
			yield break;
		}
		logger.Info("Loading scene {0}", op.name);
		string bundlename = names[0];
		string scenename = names[1];
		if (!loadedBundles.TryGetValue(bundlename, out var bundle))
		{
			byte[] bin = assets.Get(bundlename);
			if (bin == null)
			{
				logger.Error("[LoadScene] AssetBundle {0} not found", bundlename);
				yield break;
			}
			AssetBundleCreateRequest req = AssetBundle.LoadFromMemoryAsync(bin);
			yield return new AsyncWrapper(req, "Loading asset bundle " + bundlename);
			AssetBundle assetBundle = (loadedBundles[bundlename] = req.assetBundle);
			bundle = assetBundle;
		}
		if (!bundle.GetAllScenePaths().Contains(scenename + ".unity"))
		{
			logger.Error("Scene probably won't load. {0} is not present in bundle. Available scenes: {1}", scenename, string.Join(";", bundle.GetAllScenePaths()));
		}
		yield return new AsyncWrapper(SceneManager.LoadSceneAsync(scenename, LoadSceneMode.Additive), "Loading scene " + scenename);
	}

	private IEnumerator LoadPrefab(SceneOperation op)
	{
		if (op.name == null)
		{
			logger.Error("[LoadScene] Missing prefab name parameter");
			yield break;
		}
		string[] names = op.name.Split(new char[1] { ':' });
		if (names.Length != 2)
		{
			logger.Error("[LoadScene] Bad prefab name {0}. assetbundle:prefabname expected", op.name);
			yield break;
		}
		logger.Info("Loading asset {0}", op.name);
		string bundlename = names[0];
		string scenename = names[1];
		if (!loadedBundles.TryGetValue(bundlename, out var bundle))
		{
			byte[] bin = assets.Get(bundlename);
			if (bin == null)
			{
				logger.Error("[LoadScene] AssetBundle {0} not found", bundlename);
				yield break;
			}
			AssetBundleCreateRequest req = AssetBundle.LoadFromMemoryAsync(bin);
			yield return new AsyncWrapper(req, "Asset bundle " + bundlename);
			AssetBundle assetBundle = (loadedBundles[bundlename] = req.assetBundle);
			bundle = assetBundle;
		}
		if (!bundle.GetAllAssetNames().Contains(scenename + ".unity"))
		{
			logger.Error("Asset probably won't load. {0} is not present in bundle. Available assets: {1}", scenename, string.Join(";", bundle.GetAllAssetNames()));
		}
		AssetBundleRequest assetreq = bundle.LoadAssetAsync(scenename);
		yield return new AsyncWrapper(assetreq, "Loading prefab " + scenename);
		UnityEngine.Object asset = assetreq.asset;
		UnityEngine.Object.Instantiate(asset);
	}

	private IEnumerator SetProperty(SceneOperation op)
	{
		if (op.name == null)
		{
			logger.Error("[SetProperty] Missing GO name");
			yield break;
		}
		if (op.value == null)
		{
			logger.Error("[SetProperty] Missing property value at {0}", op.name);
			yield break;
		}
		ComponentPath path = new ComponentPath(op.name);
		if (!path.Valid)
		{
			logger.Error("[SetProperty] Unable to resolve reflection target {0}", op.name);
			yield break;
		}
		try
		{
			string tmp = JsonConvert.SerializeObject(op.value);
			object target = JsonConvert.DeserializeObject(tmp, path.TargetType);
			path.SetValue(target);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to convert value");
		}
	}

	internal UnityEngine.Object LoadPrefab(string bundlename, string assetname)
	{
		if (!loadedBundles.TryGetValue(bundlename, out var bundle))
		{
			byte[] bin = assets.Get(bundlename);
			if (bin == null)
			{
				logger.Error("[LoadScene] AssetBundle {0} not found", bundlename);
				return null;
			}
			AssetBundle assetBundle = (loadedBundles[bundlename] = AssetBundle.LoadFromMemory(bin));
			bundle = assetBundle;
		}
		if (!bundle.GetAllAssetNames().Contains(assetname + ".prefab"))
		{
			logger.Error("Asset probably won't load. {0} is not present in bundle. Available assets: {1}", assetname, string.Join(";", bundle.GetAllAssetNames()));
		}
		UnityEngine.Object asset = bundle.LoadAsset(assetname + ".prefab");
		if (asset != null)
		{
			return UnityEngine.Object.Instantiate(asset);
		}
		logger.Error("Unable to instantiate asset {0}", asset);
		return null;
	}
}
