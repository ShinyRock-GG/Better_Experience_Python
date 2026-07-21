using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Monkey;

internal class MonkeyControlsService : SessionService
{
	public override void OnStart()
	{
		base.OnStart();
		Transform controlTransform = UnityUtils.NewTransform("BetterScene_CustomSceneControls", null, base.Scope);
		MonkeyHelper control = controlTransform.gameObject.AddComponent<MonkeyHelper>();
		control.Configure(base.Scope);
	}
}
