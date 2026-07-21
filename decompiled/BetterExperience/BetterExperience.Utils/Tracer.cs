using UnityEngine;

namespace BetterExperience.Utils;

public class Tracer
{
	public static readonly Color RED = new Color(1f, 0f, 0f);

	private static Material _sharedLineMat;

	private static Material LINEMAT
	{
		get
		{
			if (_sharedLineMat == null)
			{
				_sharedLineMat = new Material(Shader.Find("Sprites/Default"));
			}
			return _sharedLineMat;
		}
	}

	public static void DrawRay(Vector3 start, Vector3 dir, Color color = default(Color), float duration = 0.2f)
	{
		if (color == default(Color))
		{
			color = Color.magenta;
		}
		DrawLine(start, start + dir, color, duration);
	}

	public static void DrawLine(Vector3 start, Vector3 end, float duration = 0.2f, Color? optColor = null)
	{
		Color color = Color.red;
		if (optColor.HasValue)
		{
			color = optColor.Value;
		}
		DrawLine(start, end, color, duration);
	}

	public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration = 0.2f)
	{
		DebugLine pooled = DebugLine.Create();
		LineRenderer lr = pooled.Renderer;
		lr.transform.position = start;
		lr.material = LINEMAT;
		lr.startColor = color;
		lr.endColor = color;
		lr.startWidth = 0.001f;
		lr.endWidth = 0.001f;
		lr.SetPosition(0, start);
		lr.SetPosition(1, end);
		lr.alignment = LineAlignment.View;
		pooled.gameObject.SetActive(value: true);
		pooled.Expire(duration);
	}

	public static void DrawWireBox(Transform transform, Bounds bounds, Color? optColor = null)
	{
		Vector3 topFrontRight = transform.TransformPoint(bounds.center + bounds.extents);
		Vector3 topFrontLeft = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1f, 1f, 1f)));
		Vector3 topBackRight = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(1f, 1f, -1f)));
		Vector3 topBackLeft = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1f, 1f, -1f)));
		Vector3 bottomFrontRight = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(1f, -1f, 1f)));
		Vector3 bottomFrontLeft = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1f, -1f, 1f)));
		Vector3 bottomBackRight = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(1f, -1f, -1f)));
		Vector3 bottomBackLeft = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1f, -1f, -1f)));
		DrawLine(topFrontLeft, topFrontRight, 0.2f, optColor);
		DrawLine(bottomFrontLeft, bottomFrontRight, 0.2f, optColor);
		DrawLine(topBackLeft, topBackRight, 0.2f, optColor);
		DrawLine(bottomBackLeft, bottomBackRight, 0.2f, optColor);
		DrawLine(topFrontLeft, topBackLeft, 0.2f, optColor);
		DrawLine(topFrontRight, topBackRight, 0.2f, optColor);
		DrawLine(bottomFrontLeft, bottomBackLeft, 0.2f, optColor);
		DrawLine(bottomFrontRight, bottomBackRight, 0.2f, optColor);
		DrawLine(topFrontLeft, bottomFrontLeft, 0.2f, optColor);
		DrawLine(topFrontRight, bottomFrontRight, 0.2f, optColor);
		DrawLine(topBackLeft, bottomBackLeft, 0.2f, optColor);
		DrawLine(topBackRight, bottomBackRight, 0.2f, optColor);
	}

	public static void DrawWireBox(Matrix4x4 transform, Bounds bounds, Color? optColor = null)
	{
		Vector3 topFrontRight = transform.MultiplyPoint(bounds.center + bounds.extents);
		Vector3 topFrontLeft = transform.MultiplyPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1f, 1f, 1f)));
		Vector3 topBackRight = transform.MultiplyPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(1f, 1f, -1f)));
		Vector3 topBackLeft = transform.MultiplyPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1f, 1f, -1f)));
		Vector3 bottomFrontRight = transform.MultiplyPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(1f, -1f, 1f)));
		Vector3 bottomFrontLeft = transform.MultiplyPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1f, -1f, 1f)));
		Vector3 bottomBackRight = transform.MultiplyPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(1f, -1f, -1f)));
		Vector3 bottomBackLeft = transform.MultiplyPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(-1f, -1f, -1f)));
		DrawLine(topFrontLeft, topFrontRight, 0.2f, optColor);
		DrawLine(bottomFrontLeft, bottomFrontRight, 0.2f, optColor);
		DrawLine(topBackLeft, topBackRight, 0.2f, optColor);
		DrawLine(bottomBackLeft, bottomBackRight, 0.2f, optColor);
		DrawLine(topFrontLeft, topBackLeft, 0.2f, optColor);
		DrawLine(topFrontRight, topBackRight, 0.2f, optColor);
		DrawLine(bottomFrontLeft, bottomBackLeft, 0.2f, optColor);
		DrawLine(bottomFrontRight, bottomBackRight, 0.2f, optColor);
		DrawLine(topFrontLeft, bottomFrontLeft, 0.2f, optColor);
		DrawLine(topFrontRight, bottomFrontRight, 0.2f, optColor);
		DrawLine(topBackLeft, bottomBackLeft, 0.2f, optColor);
		DrawLine(topBackRight, bottomBackRight, 0.2f, optColor);
	}

	public static void DrawTransform(Transform t)
	{
		DrawRay(t.position, t.up, Color.red);
		DrawRay(t.position, t.right, Color.green);
		DrawRay(t.position, t.forward, Color.blue);
	}

	public static void DrawTransform(Matrix4x4 t)
	{
		DrawRay(t.GetPosition(), t.MultiplyVector(Vector3.up), Color.red);
		DrawRay(t.GetPosition(), t.MultiplyVector(Vector3.right), Color.green);
		DrawRay(t.GetPosition(), t.MultiplyVector(Vector3.forward), Color.blue);
	}
}
