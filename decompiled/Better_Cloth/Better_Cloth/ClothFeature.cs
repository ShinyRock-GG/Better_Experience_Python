using System;
using System.Collections;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Chars;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using Assets._ReusableScripts.CuchiCuchi.Ropa.Glases;
using Assets.Scripts.MeshCalcules.ImplementacionLayer.WorkingMeshCalcules.Semis;
using Assets.TValle.BeachGirl.VertExmotions.Runtime.Scripts;
using Assets.TValle.BeachGirl.VertExmotions.Runtime.Scripts.Updaters;
using Assets.TValle.MeshCalcules.Runtime.ImplementacionLayer.WorkingMeshCalcules;
using Assets.TValle.MeshCalcules.TUpdaters.Runtime;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Physics;
using HarmonyLib;
using RootMotion.Dynamics;
using UnityEngine;

namespace Better_Cloth;

public class ClothFeature : PluginFeature
{
	public class CollidableClothService : SessionService
	{
		private PiezasDeRopaLoader ropaLoader;

		private CapsuleCollider[] capsules;

		private CharClothColliders clothColliders;

		private ClothSphereColliderPair[] handColliders;

		public override void OnStart()
		{
			base.OnStart();
			CreateHandColliders();
			ropaLoader = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<PiezasDeRopaLoader>();
			ropaLoader.changed += ClothRemovalService_changed;
			clothColliders = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<CharClothColliders>();
			PuppetMaster puppet = base.Session.Guest.RootObject.GetComponentInChildren<PuppetMaster>();
			if (puppet != null)
			{
				capsules = puppet.transform.GetComponentsInChildren<CapsuleCollider>();
			}
			else
			{
				capsules = new CapsuleCollider[0];
			}
		}

		private void CreateHandColliders()
		{
			float handradius = 0.05f;
			float fingerradius = 0.02f;
			int targetLayer = LayerMask.NameToLayer("DialogueSys");
			PhysicalPuppet puppet = new PhysicalPuppet(base.Session.Player.GameObject);
			Transform hand = puppet.GetIKBoneTransform(HumanBodyBones.RightHand);
			hand.gameObject.layer = targetLayer;
			SphereCollider shand = hand.gameObject.AddComponent<SphereCollider>();
			shand.radius = handradius;
			Transform arm = puppet.GetIKBoneTransform(HumanBodyBones.RightLowerArm);
			arm.gameObject.layer = targetLayer;
			SphereCollider sarm = arm.gameObject.AddComponent<SphereCollider>();
			sarm.radius = handradius;
			HumanBodyBones[][] fingers = new HumanBodyBones[4][]
			{
				new HumanBodyBones[3]
				{
					HumanBodyBones.RightIndexDistal,
					HumanBodyBones.RightIndexIntermediate,
					HumanBodyBones.RightIndexProximal
				},
				new HumanBodyBones[3]
				{
					HumanBodyBones.RightLittleDistal,
					HumanBodyBones.RightLittleIntermediate,
					HumanBodyBones.RightLittleProximal
				},
				new HumanBodyBones[3]
				{
					HumanBodyBones.RightMiddleDistal,
					HumanBodyBones.RightMiddleIntermediate,
					HumanBodyBones.RightMiddleProximal
				},
				new HumanBodyBones[3]
				{
					HumanBodyBones.RightRingDistal,
					HumanBodyBones.RightRingIntermediate,
					HumanBodyBones.RightRingProximal
				}
			};
			List<ClothSphereColliderPair> pairs = new List<ClothSphereColliderPair>();
			for (int i = 0; i < fingers.Length; i++)
			{
				float radius = fingerradius;
				HumanBodyBones[] finger = fingers[i];
				Transform b2 = puppet.GetIKBoneTransform(finger[0]);
				b2.gameObject.layer = targetLayer;
				Transform b3 = puppet.GetIKBoneTransform(finger[1]);
				b3.gameObject.layer = targetLayer;
				Transform b4 = puppet.GetIKBoneTransform(finger[2]);
				b4.gameObject.layer = targetLayer;
				SphereCollider s2 = b2.gameObject.AddComponent<SphereCollider>();
				s2.radius = radius;
				SphereCollider s3 = b3.gameObject.AddComponent<SphereCollider>();
				s3.radius = radius;
				SphereCollider s4 = b4.gameObject.AddComponent<SphereCollider>();
				s4.radius = radius;
				pairs.Add(new ClothSphereColliderPair(s2, s3));
				pairs.Add(new ClothSphereColliderPair(s3, s4));
				pairs.Add(new ClothSphereColliderPair(s4, shand));
			}
			pairs.Add(new ClothSphereColliderPair(shand, sarm));
			handColliders = pairs.ToArray();
		}

		private void ClothRemovalService_changed(RopaCubre last, RopaCubre @new, PiezasDeRopaLoader sender)
		{
			foreach (PiezaDeRopaBase ropa in ropaLoader.piezasPuestas)
			{
				Cloth ropaCloth = ((Component)(object)ropa).GetComponentInChildren<Cloth>();
				if (ropaCloth != null && (ropaCloth.capsuleColliders == null || ropaCloth.capsuleColliders.Length == 0))
				{
					ropaCloth.sphereColliders = handColliders;
				}
			}
		}

		private void CreateMeshCollider0(Cloth ropaCloth)
		{
			GameObject meshgo = UnityUtils.NewTransform("ShellCollider").gameObject;
			meshgo.AddComponent<Rigidbody>().isKinematic = true;
			SphereCollider[] scs = new SphereCollider[ropaCloth.vertices.Length];
			for (int i = 0; i < ropaCloth.vertices.Length; i++)
			{
				if (ropaCloth.coefficients[i].maxDistance > 0f)
				{
					Transform go = UnityUtils.NewTransform("Vertex_" + i, meshgo.transform);
					scs[i] = go.gameObject.AddComponent<SphereCollider>();
					scs[i].radius = 0.1f;
					scs[i].transform.position = ropaCloth.vertices[i];
					go.gameObject.layer = LayerMask.NameToLayer("f. Skin");
				}
			}
			Vector3[] cache = new Vector3[ropaCloth.vertices.Length];
			Lookup<DispatcherService>().DoUpdate.Add(delegate
			{
				if (Time.frameCount % 60 == 0)
				{
					for (int j = 0; j < ropaCloth.vertices.Length; j++)
					{
						if (!(scs[j] == null) && cache[j] != ropaCloth.vertices[j])
						{
							scs[j].transform.position = ropaCloth.transform.TransformPoint(ropaCloth.vertices[j]);
							cache[j] = ropaCloth.vertices[j];
						}
					}
				}
			}, base.Scope);
		}

		private void CreateMeshCollider(Cloth cloth)
		{
			SkinnedMeshRenderer smr = cloth.gameObject.GetComponent<SkinnedMeshRenderer>();
			Mesh physMesh = UnityEngine.Object.Instantiate(smr.sharedMesh);
			physMesh.MarkDynamic();
			GameObject meshgo = UnityUtils.NewTransform("ShellCollider", cloth.transform).gameObject;
			meshgo.layer = LayerMask.NameToLayer("f. Skin");
			meshgo.AddComponent<Rigidbody>().isKinematic = true;
			MeshCollider mc = meshgo.gameObject.AddComponent<MeshCollider>();
			mc.convex = false;
			mc.sharedMesh = physMesh;
			Lookup<DispatcherService>().DoUpdate.Add(delegate
			{
				smr.BakeMesh(physMesh);
				mc.sharedMesh = physMesh;
				mc.transform.position = cloth.transform.position;
			}, base.Scope);
		}
	}

	public class ClothRemovalService : SessionService
	{
		private PiezaDeRopaBase loader;

		private CapsuleCollider[] capsules;

		public Observable<ClothRemovalRequest> OnClothRemove { get; } = new Observable<ClothRemovalRequest>();

		public override void OnStart()
		{
			base.OnStart();
			ClothManagerHarmony.OnClothHidden.Add(OnPieceOfClothHidden, base.Scope);
			PuppetMaster puppet = base.Session.Guest.RootObject.GetComponentInChildren<PuppetMaster>();
			_ = puppet != null;
			capsules = new CapsuleCollider[0];
		}

		private void OnPieceOfClothHidden(PiezaDeRopaBase obj)
		{
			if ((UnityEngine.Object)(object)loader == null)
			{
				loader = base.Session.Guest.RootObject.GetComponentInChildren<PiezaDeRopaBase>();
			}
			if ((UnityEngine.Object)(object)loader != null && !((Behaviour)(object)obj).enabled)
			{
				Transform cloth = CloneCloth(obj);
				ClothRemovalRequest req = TransformToCloth(obj, cloth);
				OnClothRemove.Invoke(req);
				if (!req.Intercepted)
				{
					req.Proceed();
				}
			}
		}

		private Transform CloneCloth(PiezaDeRopaBase obj)
		{
			GameObject clone = UnityEngine.Object.Instantiate(((Component)(object)obj).transform.gameObject);
			clone.transform.parent = ((Component)(object)obj).transform.parent;
			UnityUtils.DestroyComponent<PiezaDeRopaBase>(clone);
			UnityUtils.DestroyComponent<GlasesSkin>(clone);
			UnityUtils.DestroyComponent<LoadSensoresDeMainCharacter>(clone);
			UnityUtils.DestroyComponent<VertExmotionUpdater>(clone);
			UnityUtils.DestroyComponent<WorkingMeshUpdater>(clone);
			UnityUtils.DestroyComponent<SkinnedWorkingMesh>(clone);
			UnityUtils.DestroyComponent<MeshSkeleton>(clone);
			UnityUtils.DestroyComponent<ShapeKeysWeightsGetter>(clone);
			UnityUtils.DestroyComponent<GenericShapeKeyCopier>(clone);
			Behaviour[] components = clone.GetComponents<Behaviour>();
			foreach (Behaviour c in components)
			{
				try
				{
					c.enabled = false;
					UnityEngine.Object.DestroyImmediate(c);
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Failed to destroy component " + c.GetType().Name);
				}
			}
			while (clone.transform.childCount > 0)
			{
				UnityEngine.Object.DestroyImmediate(clone.transform.GetChild(0).gameObject);
			}
			clone.gameObject.GetComponent<SkinnedMeshRenderer>().enabled = true;
			clone.name += " UCloth";
			return clone.transform;
		}

		private ClothRemovalRequest TransformToCloth(PiezaDeRopaBase obj, Transform dynamicCloth)
		{
			Cloth existsing = dynamicCloth.gameObject.GetComponent<Cloth>();
			if (existsing != null)
			{
				UnityEngine.Object.DestroyImmediate(existsing);
			}
			Cloth cloth = dynamicCloth.gameObject.AddComponent<Cloth>();
			if (cloth == null)
			{
				logger.Error("Failed to attach cloth component to {0}", dynamicCloth.name);
			}
			else
			{
				cloth.damping = 0.7f;
				cloth.capsuleColliders = capsules;
				cloth.enabled = false;
			}
			CascadeDestroyer destroyer = ((Component)(object)obj).gameObject.AddComponent<CascadeDestroyer>();
			destroyer.linkedObject = dynamicCloth.gameObject;
			return new ClothRemovalRequest(obj, dynamicCloth.gameObject, destroyer);
		}
	}

	public class ClothRemovalRequest
	{
		private CascadeDestroyer destroyer;

		public PiezaDeRopaBase Cloth { get; }

		public GameObject UnityClothRoot { get; }

		public bool Intercepted { get; set; }

		public ClothRemovalRequest(PiezaDeRopaBase cloth, GameObject unityClothRoot, CascadeDestroyer destroyer)
		{
			Cloth = cloth;
			UnityClothRoot = unityClothRoot;
			this.destroyer = destroyer;
		}

		public void Proceed()
		{
			Cloth c = UnityClothRoot.GetComponent<Cloth>();
			if (c != null)
			{
				c.enabled = true;
			}
			((MonoBehaviour)(object)Cloth).StartCoroutine(DestroyLater(UnityClothRoot.gameObject));
		}

		private IEnumerator DestroyLater(GameObject go)
		{
			yield return new WaitForSeconds(10f);
			if ((bool)go)
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
			if ((bool)destroyer)
			{
				UnityEngine.Object.DestroyImmediate(destroyer);
			}
		}
	}

	public override bool Enabled => true;

	public override void OnInit()
	{
		base.OnInit();
		Harmony.CreateAndPatchAll(typeof(ClothManagerHarmony), (string)null);
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new ClothRemovalService());
		Lookup<SessionTracker>().InterviewServices.Add(() => new CollidableClothService());
	}
}
