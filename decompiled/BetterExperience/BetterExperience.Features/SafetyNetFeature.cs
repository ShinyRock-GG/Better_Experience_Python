using System;
using System.Collections;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.AI;
using Assets._ReusableScripts.CuchiCuchi.AI.Emociones;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Holders;
using Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts;
using Assets._ReusableScripts.CuchiCuchi.Skins;
using Assets._ReusableScripts.PhysicsScripts;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Characters;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.Features;

internal class SafetyNetFeature : PluginFeature
{
	public class SafetyNetService : SessionService
	{
		private class ManagedHitSkin
		{
			private RecalculableJointBase joint;

			public SkinnedMeshRenderer smr { get; }

			public JointBodyAdmin admin { get; }

			public ManagedHitSkin(BaseDeTetaSkin l)
			{
				smr = l.skinnedMeshRenderer;
				admin = l.recalculableJoint.bodyAdmin;
				joint = l.recalculableJoint;
				l.rigid.maxLinearVelocity = 1f;
				l.rigid.maxDepenetrationVelocity = 1f;
				if (admin.joint.xMotion == ConfigurableJointMotion.Free)
				{
					admin.joint.xMotion = ConfigurableJointMotion.Limited;
				}
				if (admin.joint.yMotion == ConfigurableJointMotion.Free)
				{
					admin.joint.yMotion = ConfigurableJointMotion.Limited;
				}
				if (admin.joint.zMotion == ConfigurableJointMotion.Free)
				{
					admin.joint.zMotion = ConfigurableJointMotion.Limited;
				}
				SoftJointLimit limit = admin.joint.linearLimit;
				limit.limit = 0.5f;
				admin.joint.linearLimit = limit;
			}

			public ManagedHitSkin(MedioDeTetaSkin l)
			{
				smr = l.skinnedMeshRenderer;
				admin = l.recalculableJoint.bodyAdmin;
				joint = l.recalculableJoint;
			}

			internal bool IsExploded()
			{
				Vector3 sz = smr.bounds.size;
				float volume = sz.x * sz.y * sz.z;
				if (volume > 1f)
				{
					return true;
				}
				return false;
			}

			internal void Recover()
			{
				joint.FixAdmins();
				admin.KillForces();
			}
		}

		private List<ManagedHitSkin> hitskins = new List<ManagedHitSkin>();

		private List<AlteratorModifier> modifiers = new List<AlteratorModifier>();

		private List<AlteradorDeScalaDeBone> alteradors = new List<AlteradorDeScalaDeBone>();

		private PlacerBase placer;

		public override void OnStart()
		{
			// SMA 23.1 COMPATIBILITY — SafetyNet uses components and modifier sliders that are
			// absent from static ScenaConMainProtagonistaFemenina characters.
			//
			// The three failing dependencies, and why:
			//
			//   1. ModifierManager.Modifiers["Scaler_Seno_R/L"]: The modifier dictionary is
			//      populated from AlteradoresDeAparienciaFemenina.mapaDeValores, which is null in
			//      23.1 (initialization model changed for static characters). The dictionary is
			//      therefore empty — direct indexing throws KeyNotFoundException.
			//
			//   2. FemaleSkins: May not be present on the static character rig. The 23.1 character
			//      uses a different hierarchy than pool-generated NPCs.
			//
			//   3. AlteracionesDeMeshDeSenosGeneral / EmocionesFemeninas: Same — components present
			//      on genetics-backed characters, may be absent or uninitialized on static ones.
			//
			// The right fix for 23.1 is a full early-return when ANY required dependency is missing.
			// SafetyNet's job is physics correction for exploding breast physics — this feature is
			// simply not available for static characters until the component hierarchy is understood.
			//
			// TODO: Once the 23.1 component layout is mapped (see ModifierManager.cs TODO), revisit
			// whether these components exist under a different path on the static character.
			base.OnStart();

			var modifierDict = base.Session.Guest.ModifierManager.Modifiers;
			if (!modifierDict.ContainsKey(DiccionarioDeNombresDeAlteradoresFemeninos.Scaler_Seno_R) ||
			    !modifierDict.ContainsKey(DiccionarioDeNombresDeAlteradoresFemeninos.Scaler_Seno_L))
			{
				logger.Info("[BE] SafetyNetService: modifier sliders not found on '{0}' — SafetyNet disabled (static character, modifier map unavailable)",
					base.Session.Guest.Impl?.name ?? "null");
				return;
			}

			FemaleSkins skins = base.Session.Guest.Impl.GetComponentInChildren<FemaleSkins>();
			AlteracionesDeMeshDeSenosGeneral gen = base.Session.Guest.Impl.GetComponentInChildren<AlteracionesDeMeshDeSenosGeneral>();
			EmocionesFemeninas emotionsComponent = base.Session.Guest.Impl.GetComponentInChildren<EmocionesFemeninas>();

			if (skins == null || gen == null || emotionsComponent == null)
			{
				logger.Info("[BE] SafetyNetService: required components missing on '{0}' (skins={1} gen={2} emotions={3}) — SafetyNet disabled",
					base.Session.Guest.Impl?.name ?? "null",
					skins == null ? "NULL" : "found",
					gen == null ? "NULL" : "found",
					emotionsComponent == null ? "NULL" : "found");
				return;
			}

			placer = emotionsComponent.placer;
			AlteradorDeScalaDeBone a = Traverse.Create((object)gen).Field<AlteradorDeScalaDeBone>("scaler_R").Value;
			if (a != null)
			{
				alteradors.Add(a);
			}
			a = Traverse.Create((object)gen).Field<AlteradorDeScalaDeBone>("scaler_L").Value;
			if (a != null)
			{
				alteradors.Add(a);
			}
			hitskins.Add(new ManagedHitSkin(skins.hitSkins.partes.senos000.l));
			hitskins.Add(new ManagedHitSkin(skins.hitSkins.partes.senos000.r));
			hitskins.Add(new ManagedHitSkin(skins.hitSkins.partes.senos001.l));
			hitskins.Add(new ManagedHitSkin(skins.hitSkins.partes.senos001.r));
			modifiers.Add(modifierDict[DiccionarioDeNombresDeAlteradoresFemeninos.Scaler_Seno_R]);
			modifiers.Add(modifierDict[DiccionarioDeNombresDeAlteradoresFemeninos.Scaler_Seno_L]);
			Lookup<DispatcherService>().StartCoroutine(CheckLoop(), base.Scope);
		}

		private IEnumerator CheckLoop()
		{
			while (base.Scope.Started)
			{
				if (Allowed() && Any(hitskins, (ManagedHitSkin x) => x.IsExploded()))
				{
					hitskins.ForEach(delegate(ManagedHitSkin x)
					{
						x.Recover();
					});
					modifiers.ForEach(delegate(AlteratorModifier x)
					{
						x.Invalidate();
					});
					logger.Error("recover {0}", alteradors.Count);
					foreach (AlteradorDeScalaDeBone a in alteradors)
					{
						a.flagForceUpdate = true;
					}
				}
				yield return new WaitForSeconds(0.1f);
			}
		}

		private bool Allowed()
		{
			return !placer.valueAtMax;
		}

		private bool Any(List<ManagedHitSkin> hitskins, Func<ManagedHitSkin, bool> p)
		{
			foreach (ManagedHitSkin hs in hitskins)
			{
				if (p(hs))
				{
					return true;
				}
			}
			return false;
		}
	}

	public override bool Enabled => true;

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new SafetyNetService());
	}
}
