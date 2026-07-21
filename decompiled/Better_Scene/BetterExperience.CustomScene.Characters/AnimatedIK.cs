using System.Linq;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Features;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene.Characters;

internal class AnimatedIK : AnimatedSystem
{
	private static EffectorData NULL_DATA = new EffectorData();

	private static EffectorOverride NULL_OVERRIDE = new EffectorOverride();

	private ScopeSupport scope;

	private RelIK2Feature.RelIK2Service relik;

	private RelIK2Feature.IKEffectorSet _ikSet;

	private EffectorData data;

	public RelIK2Feature.IKEffectorSet EffectorSet
	{
		get
		{
			if (_ikSet == null)
			{
				_ikSet = relik.RequestEffectorSet(scope);
			}
			return _ikSet;
		}
	}

	public bool Enabled { get; set; }

	public AnimatedIK(RelIK2Feature.RelIK2Service relik, ScopeSupport scope)
	{
		this.relik = relik;
		this.scope = scope;
		data = new EffectorData();
		data.FootLeft = new EffectorOverride();
		data.FootRight = new EffectorOverride();
		data.HandLeft = new EffectorOverride();
		data.HandRight = new EffectorOverride();
		data.ShoulderLeft = new EffectorOverride();
		data.ShoulderRight = new EffectorOverride();
		data.PlayerRoot = new EffectorOverride();
	}

	public override void Apply(ExtensibleAnimator.AnimationClipState state, float dt)
	{
		EffectorData effectordata = state?.Clip?.EffectorData;
		if (effectordata == null)
		{
			effectordata = NULL_DATA;
		}
		data = effectordata;
	}

	private void ApplySettings(EffectorData effectordata)
	{
		RelIK2Feature.IKEffectorSet ikSet = EffectorSet;
		ApplyEffectorSettings(effectordata.HandLeft, ikSet.HandLeft);
		ApplyEffectorSettings(effectordata.HandRight, ikSet.HandRight);
		ApplyEffectorSettings(effectordata.ShoulderLeft, ikSet.ShoulderLeft);
		ApplyEffectorSettings(effectordata.ShoulderRight, ikSet.ShoulderRight);
		ApplyEffectorSettings(effectordata.PlayerRoot, ikSet.PlayerRoot);
	}

	private void ApplyEffectorSettings(EffectorOverride settings, RelIK2Feature.RelIKEffector effector)
	{
		if (settings == null)
		{
			settings = NULL_OVERRIDE;
		}
		if (settings.Anchor != null)
		{
			if (effector.Anchor == null || effector.Anchor.Id != settings.Anchor)
			{
				effector.Anchor = relik.Anchors.Where((RelIK2Feature.IKAnchor x) => x.Id == settings.Anchor).FirstOrDefault();
			}
		}
		else
		{
			effector.Anchor = null;
		}
		effector.EnableAngle = settings.EnableAngle;
		effector.EnableOffset = settings.EnableOffset;
		effector.Angle = settings.Angle;
		effector.Offset = settings.Offset;
		effector.Weight = ((effector.Anchor != null) ? 1 : 0);
	}

	public override void Initialize(ExtensibleAnimator.AnimationClipState state)
	{
	}

	public override void Update(ExtensibleAnimator.AnimationClipState state, float dt)
	{
		if (IsPrimary(state))
		{
			ApplySettings(data);
		}
	}

	internal void Clear()
	{
		data = NULL_DATA;
		ApplySettings(data);
	}
}
