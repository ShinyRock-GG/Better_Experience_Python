using System;
using System.Collections.Generic;
using System.Linq;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Puppet;
using Assets._ReusableScripts.CuchiCuchi.Skins;
using Assets._ReusableScripts.PhysicsScripts;
using Assets.TValle.BeachGirl;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using HarmonyLib;
using RootMotion.Dynamics;
using UnityEngine;

namespace BetterExperience.Wrappers.Physics;

public class PhysicalPuppet
{
	private static List<Collider> EMPTY_COLLIDER_LIST = new List<Collider>();

	private Logger logger = new Logger();

	private ICharacter character;

	private PuppetPartes puppetPartsInfo;

	private PuppetMaster puppetMaster;

	private Dictionary<PuppetPart, PuppetSenderPart> bodyPartMapping = new Dictionary<PuppetPart, PuppetSenderPart>();

	public Observable<PuppetSenderPart, ColisionPhysicaV2> OnCollisionStay = new Observable<PuppetSenderPart, ColisionPhysicaV2>();

	public Observable<HitSkin, Collision> OnSkinCollisionStay = new Observable<HitSkin, Collision>();

	private Dictionary<string, List<Collider>> colliderNames = new Dictionary<string, List<Collider>>();

	private Dictionary<string, PuppetBone> bones = new Dictionary<string, PuppetBone>();

	private HashSet<Collider> myColliders = new HashSet<Collider>();

	private Dictionary<Transform, Transform> ikBones = new Dictionary<Transform, Transform>();

	private GameObject root;

	public List<PuppetPart> BodyParts { get; private set; }

	public List<HitSkin> Skins { get; }

	public Observable OnBeforeIKsUpdate { get; } = new Observable();

	public Observable OnIKsUpdated { get; } = new Observable();

	public PuppetMaster PuppetMaster => puppetMaster;

	public GameObject Root => root;

	public PhysicalPuppet(GameObject root)
	{
		logger.Prefix = "[ Puppet " + root.name + "]";
		BodyParts = new List<PuppetPart>(root.GetComponentsInChildren<PuppetPart>());
		foreach (PuppetPart part in BodyParts)
		{
			part.collisionStay += delegate(PuppetPart.PartColision c2)
			{
				OnPartCollision(part, c2);
			};
			PuppetParte code = Traverse.Create((object)part).Field("m_parte").GetValue<PuppetParte>();
			bodyPartMapping[part] = (PuppetSenderPart)code;
		}
		Skins = new List<HitSkin>(root.GetComponentsInChildren<HitSkin>());
		foreach (HitSkin hs in Skins)
		{
			hs.onCollisionStay += delegate(Collision arg)
			{
				OnSkinCollisionStay.Invoke(hs, arg);
			};
		}
		character = root.GetComponentInChildren<ICharacter>();
		puppetPartsInfo = root.GetComponentInChildren<PuppetPartes>();
		puppetMaster = root.GetComponentInChildren<PuppetMaster>();
		Collider[] componentsInChildren = root.GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider c in componentsInChildren)
		{
			List<Collider> r = colliderNames.GetValueOrAdd(c.name, () => new List<Collider>());
			r.Add(c);
			myColliders.Add(c);
		}
		foreach (KeyValuePair<string, List<Collider>> kv in colliderNames)
		{
			Transform t = kv.Value[0].transform;
			bones[t.name] = new PuppetBone(t, kv.Value);
		}
		IIKUpdater updater = root.GetComponent<IIKUpdater>();
		updater.onAllIKsUpdating += Updater_onAllIKsUpdating;
		updater.onPhysicsIKUpdated += Updater_onPhysicsIKUpdated;
		this.root = root;
	}

	private void Updater_onAllIKsUpdating(IIKUpdater obj)
	{
		OnBeforeIKsUpdate.Invoke();
	}

	private void Updater_onPhysicsIKUpdated(IIKUpdater obj)
	{
		foreach (KeyValuePair<Transform, Transform> kv in ikBones)
		{
			kv.Value.position = kv.Key.position;
			kv.Value.rotation = kv.Key.rotation;
		}
		OnIKsUpdated.Invoke();
	}

	public bool ContainsCollider(Collider c)
	{
		return myColliders.Contains(c);
	}

	public List<Collider> ColliderByName(string name)
	{
		return Extensions.GetValueOrDefault(colliderNames, name, EMPTY_COLLIDER_LIST);
	}

	public Transform GetIKBoneTransform(HumanBodyBones bone)
	{
		Transform b = GetBoneTransform(bone);
		if (b == null)
		{
			return null;
		}
		if (!ikBones.TryGetValue(b, out var ikb))
		{
			ikb = UnityUtils.NewTransform("IK_" + b.name, root.transform);
			ikBones[b] = ikb;
		}
		return ikb;
	}

	public Transform GetIKBoneTransform(Transform b)
	{
		if (!ikBones.TryGetValue(b, out var ikb))
		{
			ikb = UnityUtils.NewTransform("IK_" + b.name, root.transform);
			ikBones[b] = ikb;
		}
		return ikb;
	}

	public Transform GetBoneTransform(HumanBodyBones bone)
	{
		return character.bodyAnimator.GetBoneTransform(bone);
	}

	public Transform GetBoneTransform(string boneName)
	{
		return character.bodyAnimator.GetBoneTransform(boneName);
	}

	public List<Collider> GetBoneColliders(HumanBodyBones bone)
	{
		Transform transform = GetBoneTransform(bone);
		return ColliderByName(transform.name);
	}

	public Muscle GetMuscle(HumanBodyBones bone)
	{
		Muscle m = puppetMaster.GetMuscle(bone);
		if (m == null)
		{
			logger.Warn("No muscle for {0}", bone);
		}
		return m;
	}

	private void OnPartCollision(PuppetPart part, PuppetPart.PartColision c)
	{
		OnCollisionStay.Invoke((PuppetSenderPart)c.parte, c);
	}

	public PuppetSenderPart? GetBodyPart(Rigidbody sender)
	{
		if (sender == null)
		{
			return null;
		}
		foreach (PuppetPart part in BodyParts)
		{
			if (CollideUsing(part.rigid, sender))
			{
				bodyPartMapping.TryGetValue(part, out var result);
				return result;
			}
		}
		return null;
	}

	internal Rigidbody GetDraggableRigidbody(PuppetReceiverPart grabPart)
	{
		if (Constants.skinToBoneMap.TryGetValue(grabPart, out var bone))
		{
			Muscle m = GetMuscle(bone);
			if (m != null)
			{
				return m.rigidbody;
			}
		}
		return null;
	}

	public void SetLimbRelaxed(PuppetReceiverPart part, bool relaxed)
	{
		if (!Constants.skinToBoneMap.TryGetValue(part, out var bone))
		{
			return;
		}
		HumanBodyBones[] limb = Constants.limbs.FirstOrDefault((HumanBodyBones[] x) => Array.IndexOf(x, bone) != -1);
		if (limb == null)
		{
			return;
		}
		HumanBodyBones[] array = limb;
		foreach (HumanBodyBones abone in array)
		{
			if (relaxed)
			{
				puppetMaster.SetMuscleWeights(abone, 0.1f, 0f, 1f, 10f);
			}
			else
			{
				puppetMaster.SetMuscleWeights(abone, 1f);
			}
		}
	}

	private bool CollideUsing(Rigidbody who, Rigidbody with)
	{
		if (!(who == with) && !(who.transform == with.transform))
		{
			return with.transform.IsChildOf(who.transform);
		}
		return true;
	}
}
