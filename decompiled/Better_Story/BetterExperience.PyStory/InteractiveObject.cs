using System;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.PyStory;

public class InteractiveObject : MonoBehaviour
{
	public Action handler;

	public Action on_enter;

	public Action on_leave;

	public ScopeSupport ScriptingScope { get; set; }

	public string label { get; set; }

	public void Awake()
	{
		MeshCollider mesh = GetComponentInParent<MeshCollider>();
		if (mesh != null)
		{
			MeshCollider collider = base.gameObject.AddComponent<MeshCollider>();
			collider.sharedMesh = mesh.sharedMesh;
			collider.convex = mesh.convex;
		}
		else
		{
			SphereCollider sphere = GetComponentInParent<SphereCollider>();
			if (sphere != null)
			{
				SphereCollider collider2 = base.gameObject.AddComponent<SphereCollider>();
				collider2.radius = sphere.radius;
				collider2.center = sphere.center;
			}
			else
			{
				BoxCollider box = GetComponentInParent<BoxCollider>();
				if (box != null)
				{
					BoxCollider collider3 = base.gameObject.AddComponent<BoxCollider>();
					collider3.size = box.size;
					collider3.center = box.center;
				}
				else
				{
					CapsuleCollider capsule = GetComponentInParent<CapsuleCollider>();
					if (capsule != null)
					{
						CapsuleCollider collider4 = base.gameObject.AddComponent<CapsuleCollider>();
						collider4.radius = capsule.radius;
						collider4.height = capsule.height;
						collider4.center = capsule.center;
						collider4.direction = capsule.direction;
					}
					else
					{
						Logger.Global.Error("Unable to create collider for {0}", base.gameObject.transform.parent.name);
					}
				}
			}
		}
		base.gameObject.layer = LayerMask.NameToLayer("DialogueSys");
		if (label == null)
		{
			label = base.gameObject.name;
		}
	}

	public void FireHandler()
	{
		if (handler != null)
		{
			InvokeAction(handler);
		}
	}

	private void InvokeAction(Action handler)
	{
		try
		{
			handler();
		}
		catch (Exception ex)
		{
			if (ScriptingScope != null)
			{
				ScriptingScope.NotifyCrash(ex);
			}
			else
			{
				Logger.Global.Error(ex, "Interaction failed");
			}
		}
	}

	public void FireOnEnter()
	{
		if (on_enter != null)
		{
			InvokeAction(on_enter);
		}
	}

	public void FireOnLeave()
	{
		if (on_leave != null)
		{
			InvokeAction(on_leave);
		}
	}
}
