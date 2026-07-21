using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using PixelCrushers.DialogueSystem;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BetterExperience.Features;

internal class SkipTextFeature : PluginFeature
{
	private const int WHERE_IS_MY_MONEY_CONVERSATION_ID = 84;

	private string lastClickedText;

	private string lastSpeaker;

	private float delay;

	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<float> speed;

	private ConfigEntry<bool> moneyTalksFix;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "EnableSkipText", true, "Autoplay text dialogs (Like always hold Ctrl)");
		speed = config.Bind<float>("SkipText", "DurationSecondsPerChar", 0.01f, "Autoplay dialog: duration in seconds per character");
		moneyTalksFix = config.Bind<bool>("SkipText", "MitigateMoneyTalksBug", true, "Autoplay dialog: Money dialog fix");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope);
		Lookup<PluginOptionsService>().Expose(moneyTalksFix, base.Scope);
		Lookup<PluginOptionsService>().Expose(speed, base.Scope);
	}

	public override void OnStart()
	{
		base.OnStart();
		Plugin.DoUpdate.Add(DoUpdate, base.Scope);
	}

	private void DoUpdate()
	{
		if (enableFeature.Value && DialogueManager.HasInstance && !DialogueManager.IsDialogueSystemInputDisabled())
		{
			UnityUIDialogueUI ui = DialogueManager.DialogueUI as UnityUIDialogueUI;
			if (ui != null && ui.IsOpen && (!moneyTalksFix.Value || DialogueManager.LastConversationID != 84 || !DialogueManager.CurrentConversationState.subtitle.dialogueEntry.isGroup) && !ContinueSubtitle(ui.Dialogue.PCSubtitle))
			{
				ContinueSubtitle(ui.Dialogue.NPCSubtitle);
			}
		}
	}

	private bool ContinueSubtitle(AbstractUISubtitleControls subtitle)
	{
		if (subtitle is UnityUISubtitleControls control && control.continueButton.isActiveAndEnabled && (lastClickedText != control.line.text || lastSpeaker != control.portraitName.text))
		{
			delay += Time.deltaTime;
			float duration = (float)control.line.text.Length * speed.Value;
			if (delay > duration)
			{
				lastClickedText = control.line.text;
				lastSpeaker = control.portraitName.text;
				delay = 0f;
				control.continueButton.OnPointerClick(new PointerEventData(EventSystem.current));
				return true;
			}
		}
		return false;
	}
}
