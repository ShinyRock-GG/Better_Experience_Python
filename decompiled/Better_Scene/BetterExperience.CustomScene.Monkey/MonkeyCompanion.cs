using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.GameScopes;
using Monkey;
using Monkey.Game;

namespace BetterExperience.CustomScene.Monkey;

internal class MonkeyCompanion : PluginService
{
	public override void OnStart()
	{
		base.OnStart();
		Lookup<StoryManager>().StoryServices.Add(() => new MonkeyDelegate());
		Lookup<StoryManager>().StoryInterviewServices.Add(() => new MonkeyControlsService());
		PackageManager pakman = Lookup<PackageManager>();
		List<string> paths = StorageManager.GetFileList(Settings.AssetPath, Settings.ASSET_EXTENSIONS);
		for (int i = 0; i < paths.Count; i++)
		{
			string name = Path.GetFileNameWithoutExtension(paths[i]);
			PackageManifest manifest = new PackageManifest();
			manifest.id = "monkey." + name;
			manifest.name = "[M] " + name;
			manifest.version = "0.0.0";
			manifest.type = PackageType.story;
			manifest.mainScene = "monkey";
			manifest.options["no_css"] = "true";
			manifest.description = "Bridge package for Monkey asset " + name;
			AssetLoader.SceneDef scene = new AssetLoader.SceneDef();
			scene.Id = "monkey";
			scene.sequence.Add(new AssetLoader.SceneOperation
			{
				type = "monkey_asset",
				name = paths[i]
			});
			MemoryStream ms = new MemoryStream();
			using (ZipArchive za = new ZipArchive(ms, ZipArchiveMode.Create))
			{
				ZipArchiveEntry pkgEntry = za.CreateEntry("manifest.json");
				using (Stream pkgStream = pkgEntry.Open())
				{
					byte[] data = Encoding.UTF8.GetBytes(GlobalPersistenceService.Serialize(manifest));
					pkgStream.Write(data, 0, data.Length);
				}
				ZipArchiveEntry sceneEntry = za.CreateEntry("monkey.scene");
				using Stream sceneStream = sceneEntry.Open();
				byte[] data2 = Encoding.UTF8.GetBytes(GlobalPersistenceService.Serialize(scene));
				sceneStream.Write(data2, 0, data2.Length);
			}
			pakman.AddInMemoryPackage(new ZipIO(new ZipArchive(new MemoryStream(ms.ToArray()), ZipArchiveMode.Read), "[monkey]" + name));
			logger.Info("Imported monkey package {0}", manifest.id);
		}
	}

	private IEnumerator LoadMonkeyAsset(AssetLoader.SceneOperation op)
	{
		try
		{
			return Lookup<MonkeyDelegate>().LoadAsset(op.name);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Monkey not available?");
			return null;
		}
	}
}
