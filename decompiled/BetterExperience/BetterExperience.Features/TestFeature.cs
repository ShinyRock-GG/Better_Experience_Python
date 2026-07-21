using System;
using System.Linq;
using Assets._ReusableScripts.CuchiCuchi.Controllers;
using Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts;
using Assets._ReusableScripts.PhysicsScripts;
using BetterExperience.Features.Console;
using BetterExperience.GameScopes;
using BetterExperience.Utils;

namespace BetterExperience.Features;

internal class TestFeature : SessionService
{
	[ConsoleCommand("", new string[] { "test" })]
	public class GuestScoreQuery
	{
		[ConsoleCommandArg(Key = "i", Mode = ConsoleArgMode.KeyValue, Name = "123")]
		public float value { get; set; }
	}

	[ConsoleCommand("", new string[] { "test2" })]
	public class GuestScoreQuery2
	{
		[ConsoleCommandArg(Key = "i", Mode = ConsoleArgMode.Tail, Name = "123")]
		public string value { get; set; }
	}

	[ConsoleCommand("", new string[] { "test3" })]
	public class GuestScoreQuery3
	{
		[ConsoleCommandArg(Key = "i", Mode = ConsoleArgMode.KeyValue, Name = "123")]
		public float value { get; set; }
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<ConsoleService>().RegisterCommand<GuestScoreQuery>(test, base.Scope);
		Lookup<ConsoleService>().RegisterCommand<GuestScoreQuery2>(test2, base.Scope);
		Lookup<ConsoleService>().RegisterCommand<GuestScoreQuery3>(test3, base.Scope);
	}

	private string test(GuestScoreQuery query)
	{
		if (base.Session.Guest == null)
		{
			return "start interview first";
		}
		VagController ctl = base.Session.Guest.Impl.GetComponentInChildren<VagController>();
		UpdateChain(ctl.labiaController.l.chain, query.value);
		UpdateChain(ctl.labiaController.r.chain, query.value);
		return "ok";
	}

	private string test2(GuestScoreQuery2 query)
	{
		if (base.Session.Guest == null)
		{
			return "start interview first";
		}
		float[] values = query.value.Split(new char[1] { ' ' }).Select(float.Parse).ToArray();
		VagController ctl = base.Session.Guest.Impl.GetComponentInChildren<VagController>();
		UpdateChain(ctl.labiaController.l.chain, values);
		UpdateChain(ctl.labiaController.r.chain, values);
		return "ok";
	}

	private void UpdateChain(Linear7BoneChainBase lchain, float value)
	{
		float[] values = new float[lchain.puntosBase.Count].Fill(value);
		UpdateChain(lchain, values);
	}

	private void UpdateChain(Linear7BoneChainBase lchain, float[] values)
	{
		for (int i = 0; i < Math.Min(lchain.puntosBase.Count, values.Length); i++)
		{
			float value = values[i];
			JointDistancesAdmin admin = lchain.puntosBase[i].jointDistancesAdmin;
			admin.configuracion.finalTagetPoistionMod = value;
			admin.UpdateDistaceAndTargetMods();
		}
		lchain.estadoDePuntos.posicionesLocalesIniciales.Actializar();
	}

	private string test3(GuestScoreQuery3 query)
	{
		if (base.Session.Guest == null)
		{
			return "start interview first";
		}
		VagController ctl = base.Session.Guest.Impl.GetComponentInChildren<VagController>();
		UpdateChain2(ctl.labiaController.l.chain, query.value);
		UpdateChain2(ctl.labiaController.r.chain, query.value);
		return "ok";
	}

	private void UpdateChain2(Linear7BoneChainBase lchain, float value)
	{
		float[] values = new float[lchain.puntosBase.Count].Fill(value);
		UpdateChain2(lchain, values);
	}

	private void UpdateChain2(Linear7BoneChainBase lchain, float[] values)
	{
		for (int i = 0; i < Math.Min(lchain.puntosBase.Count, values.Length); i++)
		{
			float value = values[i];
			JointDistancesAdmin admin = lchain.puntosBase[i].jointDistancesAdmin;
			ModificadorDeDistanciasDeJoint mod = admin.estirable.ObtenerModificadorNotNull("mymod");
			mod.SetLinearLimitTo(value);
			admin.UpdateDistaceAndTargetMods();
		}
		lchain.estadoDePuntos.posicionesLocalesIniciales.Actializar();
	}
}
