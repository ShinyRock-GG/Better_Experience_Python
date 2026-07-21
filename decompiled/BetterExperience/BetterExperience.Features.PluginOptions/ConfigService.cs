using BepInEx.Configuration;
using BetterExperience.Utils;

namespace BetterExperience.Features.PluginOptions;

internal class ConfigService
{
	private ConfigFile cfg;

	private PerModuleConfigRegistry<bool> flagRegistry = new PerModuleConfigRegistry<bool>();

	public PerModuleConfigRegistry<bool> Flags => flagRegistry;

	public ConfigService(ConfigFile cfg)
	{
		this.cfg = cfg;
	}

	public ConfigEntry<bool> BindFlag(string section, string key, string module, string description, bool def)
	{
		return BindEntry(flagRegistry, section, key, module, description, def);
	}

	private ConfigEntry<T> BindEntry<T>(PerModuleConfigRegistry<T> registry, string section, string key, string module, string description, T def)
	{
		ConfigEntry<T> entry = cfg.Bind<T>(section, key, def, description);
		ListDictionary<string, ConfigEntry<T>> d1 = registry.GetValueOrAdd(module, () => new ListDictionary<string, ConfigEntry<T>>());
		d1[description] = entry;
		return entry;
	}
}
