using System.Collections;
using System.Collections.Generic;
using System.IO;
using BetterExperience.GameScopes;
using HarmonyLib;
using Monkey;
using Monkey.Game;
using Monkey.UI.Windows;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetterExperience.CustomScene.Monkey;

internal class MonkeyDelegate : SessionService
{
	private Traverse<int> pAssetId;

	private GameObject monkey;

	private Dictionary<string, AssetManager.BundleInfo> LoadedAssetBundles { get; set; }

	private int AssetIdRef
	{
		get
		{
			return pAssetId.Value;
		}
		set
		{
			pAssetId.Value = value;
		}
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<AssetLoader>().RegisterOperationHandler("monkey_asset", LoadMonkeyAsset, base.Scope);
		LoadedAssetBundles = (Dictionary<string, AssetManager.BundleInfo>)Traverse.Create(typeof(AssetManager)).Field("_loadedAssetBundle").GetValue();
		pAssetId = Traverse.Create(typeof(AssetManager)).Field<int>("_assetId");
		monkey = GameObject.Find("Monkey");
	}

	private IEnumerator LoadMonkeyAsset(AssetLoader.SceneOperation arg)
	{
		return LoadAsset(arg.name);
	}

	internal IEnumerator LoadAsset(string path)
	{
		logger.Info("Delegting monkey loadBundle {0}", path);
		IEnumerator it = LoadMonkeyBundleAsync(path);
		while (it.MoveNext())
		{
			yield return it.Current;
		}
		string text = Path.ChangeExtension(path, Settings.JSON_EXTENSION);
		if (File.Exists(text))
		{
			AssetManager.BundleInfo assetInfo = AssetManager.GetBundleInfo(path);
			if (assetInfo != null)
			{
				PluginWindow.LoadParseUI(assetInfo.assetTree, text, assetInfo.assetId);
				FemaleCustomManager.OnRefreshEvent(FemaleCustomManager.RefreshType.Load, assetInfo.assetId);
			}
		}
	}

	private IEnumerator LoadMonkeyBundleAsync(string path)
	{
		AssetBundle assetBundle;
		if (!LoadedAssetBundles.TryGetValue(path, out var abi))
		{
			AssetBundleCreateRequest req = AssetBundle.LoadFromFileAsync(path);
			yield return new AssetLoader.AsyncWrapper(req, "MonkeyBridge: loading asset");
			assetBundle = req.assetBundle;
			if (assetBundle != null)
			{
				LoadedAssetBundles.Add(path, new AssetManager.BundleInfo(assetBundle, AssetIdRef++));
			}
			else
			{
				logger.Error("Unable to load asset bundle {0}", path);
			}
		}
		else
		{
			assetBundle = abi.ab;
		}
		if (!(assetBundle != null))
		{
			yield break;
		}
		if (assetBundle.isStreamedSceneAssetBundle)
		{
			AsyncOperation scenereq = SceneManager.LoadSceneAsync(Path.GetFileNameWithoutExtension(assetBundle.GetAllScenePaths()[0]), new LoadSceneParameters(LoadSceneMode.Additive));
			yield return new AssetLoader.AsyncWrapper(scenereq, "MonkeyBridge: loading scene");
			yield break;
		}
		AssetBundleRequest assetreq = ((assetBundle != null) ? assetBundle.LoadAllAssetsAsync<GameObject>() : null);
		yield return new AssetLoader.AsyncWrapper(assetreq, "MonkeyBridge: loading all assets");
		Object[] array = assetreq.allAssets;
		for (int i = 0; i < array.Length; i++)
		{
			Object.Instantiate((GameObject)array[i], monkey.transform).hideFlags |= HideFlags.HideAndDontSave;
		}
	}
}
