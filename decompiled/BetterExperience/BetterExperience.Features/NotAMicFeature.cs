using Assets.TValle.BeachGirl.Runtime;
using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Utils;

namespace BetterExperience.Features;

internal class NotAMicFeature : PluginFeature
{
	private class TalkSupressor : SessionService
	{
		public override void OnStart()
		{
			base.Session.Guest.HeadController.ActiveMimicChanged.Add(OnEmotionChanged, base.Scope);
		}

		private void OnEmotionChanged(BitMask<TiposDeGestosDeBoca> obj)
		{
			base.Session.Guest.HeadController.Mute = obj.ContainsAny(TiposDeGestosDeBoca.deseoOralBig, TiposDeGestosDeBoca.deseoOralMoster, TiposDeGestosDeBoca.deseoOralNormal);
		}
	}

	private ConfigEntry<bool> enableFeature;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "NotAMicEnabled", true, "Enable NotAMic: Mute when mouth is open");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope, PluginOptionsService.SettingsType.guest);
	}

	public override void OnStart()
	{
		Lookup<SessionTracker>().InterviewServices.Add(() => new TalkSupressor());
	}
}
