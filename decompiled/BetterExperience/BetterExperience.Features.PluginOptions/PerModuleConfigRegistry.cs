using BepInEx.Configuration;
using BetterExperience.Utils;

namespace BetterExperience.Features.PluginOptions;

internal class PerModuleConfigRegistry<T> : ListDictionary<string, ListDictionary<string, ConfigEntry<T>>>
{
}
