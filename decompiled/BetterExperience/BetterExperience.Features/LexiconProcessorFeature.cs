using BepInEx.Configuration;
using BetterExperience.Features.Lexicon;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;

namespace BetterExperience.Features;

internal class LexiconProcessorFeature : PluginFeature
{
	public class LexiconService : SessionService
	{
		public override void OnStart()
		{
			base.OnStart();
			Lookup<LexiconProcessorFeature>().Process();
		}
	}

	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<bool> enableConvProcessor;

	private LexiconProcessor processor;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "EnableLexiconProcessor", false, "Enable Lexicon Processor: dialogs export and modification");
		enableConvProcessor = config.Bind<bool>("Features", "EnableLexiconConvProcessor", false, "Enable Lexicon Conversation Processor: cinversation.json import export");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope);
	}

	public void Process()
	{
		processor.Process();
	}

	public override void OnStart()
	{
		base.OnStart();
		processor = new LexiconProcessor(Lookup<PersistenceService>(), enableConvProcessor.Value);
		Lookup<SessionTracker>().InterviewServices.Add(() => new LexiconService());
	}
}
