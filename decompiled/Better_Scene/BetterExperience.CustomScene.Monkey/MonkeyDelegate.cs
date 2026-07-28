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
		// Unity time-slices async load integration by backgroundLoadingPriority
		// (default BelowNormal ≈ 4ms/frame), which drip-feeds a 389MB streamed
		// scene bundle over many seconds even on NVMe. The loading screen is up
		// for this entire coroutine, so raise the budget (High ≈ 50ms/frame) and
		// restore the previous value when done.
		ThreadPriority prevLoadPrio = Application.backgroundLoadingPriority;
		Application.backgroundLoadingPriority = ThreadPriority.High;
		try
		{
			// Phase timing so LogOutput.log shows where scene-load wall clock goes.
			System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
			AssetBundle assetBundle;
			if (!LoadedAssetBundles.TryGetValue(path, out var abi))
			{
				// LZMA-compressed bundles are fully decompressed inside
				// LoadFromFileAsync on a single thread (~25 MB/s — 16+s for this
				// 389 MB bundle, regardless of disk speed or loading priority).
				// One-time fix: recompress to an LZ4 sidecar cache, then always
				// load the LZ4 copy (LZ4 loads lazily, near-instant open).
				string lz4Path = path + ".lz4cache";
				bool cacheValid = File.Exists(lz4Path)
					&& File.GetLastWriteTimeUtc(lz4Path) >= File.GetLastWriteTimeUtc(path);
				if (!cacheValid)
				{
					sw.Restart();
					AssetBundleRecompressOperation rop = AssetBundle.RecompressAssetBundleAsync(
						path, lz4Path, BuildCompression.LZ4Runtime, 0u, ThreadPriority.High);
					yield return new AssetLoader.AsyncWrapper(rop, "MonkeyBridge: converting asset to fast format (one-time)");
					logger.Warn("[MonkeyPerf] LZ4 recompress({0}) success={1} result={2} took {3}ms",
						Path.GetFileName(path), rop.success, rop.result, sw.ElapsedMilliseconds);
					cacheValid = rop.success && File.Exists(lz4Path);
					if (!cacheValid && File.Exists(lz4Path))
					{
						File.Delete(lz4Path); // don't leave a broken cache behind
					}
				}
				string loadPath = (cacheValid ? lz4Path : path);
				sw.Restart();
				AssetBundleCreateRequest req = AssetBundle.LoadFromFileAsync(loadPath);
				yield return new AssetLoader.AsyncWrapper(req, "MonkeyBridge: loading asset");
				assetBundle = req.assetBundle;
				logger.Warn("[MonkeyPerf] LoadFromFileAsync({0}) took {1}ms", Path.GetFileName(loadPath), sw.ElapsedMilliseconds);
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
				sw.Restart();
				AsyncOperation scenereq = SceneManager.LoadSceneAsync(Path.GetFileNameWithoutExtension(assetBundle.GetAllScenePaths()[0]), new LoadSceneParameters(LoadSceneMode.Additive));
				yield return new AssetLoader.AsyncWrapper(scenereq, "MonkeyBridge: loading scene");
				logger.Warn("[MonkeyPerf] Additive scene load took {0}ms", sw.ElapsedMilliseconds);
				yield break;
			}
			sw.Restart();
			AssetBundleRequest assetreq = ((assetBundle != null) ? assetBundle.LoadAllAssetsAsync<GameObject>() : null);
			yield return new AssetLoader.AsyncWrapper(assetreq, "MonkeyBridge: loading all assets");
			Object[] array = assetreq.allAssets;
			for (int i = 0; i < array.Length; i++)
			{
				Object.Instantiate((GameObject)array[i], monkey.transform).hideFlags |= HideFlags.HideAndDontSave;
			}
			logger.Warn("[MonkeyPerf] LoadAllAssets+Instantiate({0}) took {1}ms", array.Length, sw.ElapsedMilliseconds);
		}
		finally
		{
			Application.backgroundLoadingPriority = prevLoadPrio;
		}
	}
}
