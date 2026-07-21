using System;
using System.IO;
using BepInEx.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BetterExperience.GameScopes;

public class GlobalPersistenceService : PluginFeature, PersistenceService
{
	private ConfigEntry<string> persistenceDir;

	private ConfigEntry<string> exchangeDir;

	public override bool Enabled => true;

	public string Dir => persistenceDir.Value;

	public string ExchangeDir => exchangeDir.Value;

	public override void Configure(ConfigFile config)
	{
		base.Configure(config);
		persistenceDir = config.Bind<string>("Persistence", "PersistenceDir", Path.Combine(".", "Better_Dir"), (ConfigDescription)null);
		exchangeDir = config.Bind<string>("Persistence", "ExchangeDir", Path.Combine(".", "Better_Exchange"), (ConfigDescription)null);
	}

	public override void OnStart()
	{
		base.OnStart();
		new DirectoryInfo(Dir).Create();
		new DirectoryInfo(ExchangeDir).Create();
	}

	public void Persist<T>(T value, string customName, bool exchange)
	{
		string fpath = GetPathForType(typeof(T), customName, exchange);
		new FileInfo(fpath).Directory.Create();
		JsonSerializerSettings settings = new JsonSerializerSettings();
		settings.Formatting = Formatting.Indented;
		settings.NullValueHandling = NullValueHandling.Ignore;
		string blob = JsonConvert.SerializeObject(value, settings);
		File.WriteAllText(fpath, blob);
	}

	private string GetPathForType(Type value, string customName, bool exchange)
	{
		string fname = ((customName == null) ? value.Name : customName) + ".json";
		return Path.Combine(exchange ? exchangeDir.Value : persistenceDir.Value, fname);
	}

	public T Persisted<T>(Func<T> factory, string customName, bool exchange)
	{
		string fpath = GetPathForType(typeof(T), customName, exchange);
		if (File.Exists(fpath))
		{
			string blob = File.ReadAllText(fpath);
			return JsonConvert.DeserializeObject<T>(blob);
		}
		logger.Info("Missing file {0}", fpath);
		return factory();
	}

	public static string Serialize<T>(T value)
	{
		JsonSerializerSettings settings = new JsonSerializerSettings();
		settings.Formatting = Formatting.Indented;
		settings.NullValueHandling = NullValueHandling.Ignore;
		settings.Converters.Add(new StringEnumConverter());
		return JsonConvert.SerializeObject(value, settings);
	}

	public static T Deserialize<T>(string value)
	{
		return JsonConvert.DeserializeObject<T>(value);
	}
}
