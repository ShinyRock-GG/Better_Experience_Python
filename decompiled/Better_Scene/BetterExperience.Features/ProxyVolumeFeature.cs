using System.Linq;
using BetterExperience.GameScopes;
using UnityEngine;
using UnityEngine.Rendering;

namespace BetterExperience.Features;

internal class ProxyVolumeFeature : PluginService
{
	public class ProxyVolumeService : SessionService
	{
		public override void OnStart()
		{
			base.OnStart();
			SkinnedMeshRenderer[] meshes = ((Component)(object)base.Session.Guest.Impl).GetComponentsInChildren<SkinnedMeshRenderer>();
			SkinnedMeshRenderer main = meshes.Where((SkinnedMeshRenderer x) => x.name == "CC_Base_Body").FirstOrDefault();
			if (main == null)
			{
				return;
			}
			LightProbeProxyVolume lppv = main.gameObject.AddComponent<LightProbeProxyVolume>();
			lppv.gridResolutionX = 10;
			lppv.gridResolutionY = 10;
			lppv.gridResolutionZ = 10;
			lppv.resolutionMode = LightProbeProxyVolume.ResolutionMode.Custom;
			lppv.probeDensity = 1f;
			SkinnedMeshRenderer[] array = meshes;
			foreach (SkinnedMeshRenderer renderer in array)
			{
				if (renderer != main)
				{
					renderer.lightProbeProxyVolumeOverride = main.gameObject;
				}
				renderer.lightProbeUsage = LightProbeUsage.UseProxyVolume;
			}
		}
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new ProxyVolumeService());
	}
}
