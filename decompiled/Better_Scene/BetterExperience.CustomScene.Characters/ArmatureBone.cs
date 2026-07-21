using System;
using System.Collections.Generic;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using RootMotion.Dynamics;
using UnityEngine;

namespace BetterExperience.CustomScene.Characters;

public class ArmatureBone : MonoBehaviour
{
	private LineRenderer lineRenderer;

	private ScopeSupport boneScope = new ScopeSupport();

	private Vector3 offset = Vector3.right;

	public Transform Target { get; set; }

	public Armature Armature { get; set; }

	public Muscle Muscle { get; internal set; }

	public List<ArmatureBone> Children { get; } = new List<ArmatureBone>();

	public ArmatureBone Parent { get; private set; }

	public ArmatureBone()
	{
		((BaseObservable<Action<bool>>)(object)Armature.DrawArmature).Add((Action<bool>)delegate
		{
			UpdateDrawArmatureState();
		}, boneScope);
	}

	internal void MapTo(float dt)
	{
		if (Target != null)
		{
			Target.rotation = base.transform.rotation;
			Target.position = Vector3.MoveTowards(Target.position, base.transform.position, Armature.TranslationVelocity * dt);
		}
	}

	internal void MapToRecursive(float dt)
	{
		MapTo(dt);
		for (int i = 0; i < Children.Count; i++)
		{
			Children[i].MapToRecursive(dt);
		}
	}

	internal void ReadFrom()
	{
		if (Target != null)
		{
			if (Armature.RootBone == this || Armature.RootBone.Target == Target.parent)
			{
				base.transform.position = Target.position;
			}
			Quaternion localRot = Quaternion.Inverse(Armature.RootBone.Target.rotation) * Target.rotation;
			Quaternion rot = Armature.RootBone.Target.rotation * localRot;
			base.transform.rotation = rot;
			base.transform.localScale = Target.localScale;
		}
	}

	internal void ReadRecursive()
	{
		ReadFrom();
		for (int i = 0; i < Children.Count; i++)
		{
			Children[i].ReadRecursive();
		}
	}

	private void OnDestroy()
	{
		boneScope.Dispose();
	}

	private void OnEnable()
	{
		UpdateDrawArmatureState();
		Parent = base.transform.parent.GetComponentInParent<ArmatureBone>();
		if (Parent != null)
		{
			Parent.Children.Add(this);
		}
		Armature.dirtyBonesList = true;
	}

	private void OnDisable()
	{
		if (Parent != null)
		{
			Parent.Children.Remove(this);
		}
		Armature.dirtyBonesList = true;
	}

	private void UpdateDrawArmatureState()
	{
		if (!base.enabled)
		{
			return;
		}
		if (CanDrawArmature())
		{
			if (GetComponent<LineRenderer>() == null)
			{
				lineRenderer = base.gameObject.AddComponent<LineRenderer>();
				lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
				lineRenderer.startColor = Color.white;
				lineRenderer.endColor = Color.white;
				lineRenderer.startWidth = 0.01f;
				lineRenderer.endWidth = 0.05f;
				lineRenderer.alignment = LineAlignment.View;
			}
		}
		else
		{
			if ((bool)lineRenderer)
			{
				UnityEngine.Object.DestroyImmediate(lineRenderer);
			}
			lineRenderer = null;
		}
	}

	private bool CanDrawArmature()
	{
		if (base.transform.parent.GetComponent<ArmatureBone>() != null)
		{
			return Armature.DrawArmature.Value;
		}
		return false;
	}

	private void Update()
	{
		if (lineRenderer != null)
		{
			float dist = Vector3.Distance(base.transform.position, base.transform.parent.position);
			Vector3 vec = base.transform.parent.forward * dist;
			lineRenderer.SetPosition(0, base.transform.parent.position + vec + offset);
			lineRenderer.SetPosition(1, base.transform.parent.position + offset);
		}
	}

	public ArmatureBone SplitBone(string name)
	{
		Transform t = UnityUtils.NewTransform(name, base.transform.parent);
		t.localPosition = Vector3.zero;
		t.localRotation = Quaternion.identity;
		base.transform.parent = t;
		ArmatureBone bone = t.gameObject.AddComponent<ArmatureBone>();
		bone.Armature = Armature;
		return bone;
	}

	internal void ReadHierarchy(List<ArmatureBone> bonesList)
	{
		foreach (ArmatureBone c in Children)
		{
			if (!bonesList.Contains(c))
			{
				bonesList.Add(c);
			}
		}
		foreach (ArmatureBone c2 in Children)
		{
			c2.ReadHierarchy(bonesList);
		}
	}
}
