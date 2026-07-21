using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace BetterExperience.Utils;

public class DebugLine : MonoBehaviour
{
	public static ObjectPool<DebugLine> linesPool = new ObjectPool<DebugLine>(delegate
	{
		GameObject gameObject = new GameObject();
		LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
		return lineRenderer.gameObject.AddComponent<DebugLine>();
	});

	public LineRenderer Renderer { get; internal set; }

	public static DebugLine Create()
	{
		return linesPool.Get();
	}

	public void Expire(float time)
	{
		StartCoroutine(ExpireLoop(time));
	}

	public void Awake()
	{
		Renderer = base.gameObject.GetComponent<LineRenderer>();
	}

	private IEnumerator ExpireLoop(float time)
	{
		yield return new WaitForSeconds(time);
		base.gameObject.SetActive(value: false);
		linesPool.Release(this);
	}
}
