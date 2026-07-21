using System;
using Assets._ReusableScripts.CuchiCuchi.AI;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;

namespace BetterExperience.Features;

internal class EmoSpy : PluginFeature
{
	private class GuestEmotionView : SessionService
	{
		private event Action Update = delegate
		{
		};

		public override void OnStart()
		{
			Lookup<OverlayService>().TopRightPane.Add(CreateGUI(), base.Scope);
		}

		private GridLayout CreateGUI()
		{
			EmocionesFemeninas emotionsComponent = base.Session.Guest.Impl.GetComponentInChildren<EmocionesFemeninas>();
			GridLayout basegrid = new GridLayout();
			GridLayout root = new GridLayout();
			basegrid.Add(root);
			basegrid.NewLine();
			root.Label("Emotions");
			root.NewLine();
			int col = 0;
			foreach (Emocion e in emotionsComponent.emociones)
			{
				string name = e.GetType().Name;
				root.Label(name);
				DrawableLabel value = root.Label(e.value.total.ToString());
				Action<Emocion> cb = delegate
				{
					value.Text = e.value.total.ToString();
					this.Update();
				};
				e.afterUpdate += cb;
				base.Scope.OnDispose += delegate
				{
					e.afterUpdate -= cb;
				};
				if (++col % 2 == 0)
				{
					root.NewLine();
				}
			}
			root = new GridLayout();
			basegrid.Add(root);
			basegrid.NewLine();
			return basegrid;
		}
	}

	private ConfigEntry<bool> enableFeature;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "EnableEmoSpy", false, "Enable EmoSpy: Display numeric emotion values");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope);
	}

	public override void OnStart()
	{
		Lookup<SessionTracker>().InterviewServices.Add(() => new GuestEmotionView());
	}
}
