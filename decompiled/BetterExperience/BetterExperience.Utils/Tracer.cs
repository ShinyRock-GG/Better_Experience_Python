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

	/// <summary>Metres to pull debug lines toward the camera so body meshes stop hiding them.
	/// Capped at a quarter of the eye distance so close-up lines are not yanked into the lens.
	/// </summary>
	public static float OnTopBias = 0.35f;

	private static Material _overlayLineMat;

	/// <summary>
	/// Material that ignores the depth buffer, so a line stays visible through geometry. This is
	/// a SECOND material rather than a change to LINEMAT: LINEMAT is shared with every other
	/// Tracer caller, and AutoSeek's lines should keep occluding normally. A diagnostic line that
	/// disappears inside a body, however, is useless.
	/// </summary>
	private static Material OVERLAYMAT
	{
		get
		{
			if (_overlayLineMat == null)
			{
				// START FROM THE SHADER THAT IS PROVEN TO RENDER. Hidden/Internal-Colored drew
				// NOTHING here: the caller logged 491 valid readings while producing zero visible
				// lines, and the same geometry through plain DrawLine had been visible (merely
				// occluded) the build before. AutoSeek's lines use LINEMAT and work. So this is
				// LINEMAT's shader with the depth test relaxed on top — if the ZTest override is
				// not supported the lines are occluded again, which is strictly better than gone.
				// THIRD ATTEMPT, and this time nothing is changed except the width at the call
				// site. Sprites/Default hardcodes ZTest and ZWrite inside its shader pass, so the
				// SetInt calls that used to be here were no-ops — which left renderQueue = 5000
				// (past the Overlay queue at 4000) as the ONLY real difference from the DrawLine
				// path that demonstrably renders. That is the remaining suspect, so it goes.
				//
				// Visible-and-occluded beats invisible. Drawing through geometry is a separate
				// improvement and needs its own evidence, not a fourth guess stacked on this one.
				_overlayLineMat = new Material(LINEMAT.shader);
				_overlayLineMat.hideFlags = HideFlags.HideAndDontSave;
			}
			return _overlayLineMat;
		}
	}

	/// <summary>Draws through geometry, and thicker than DrawLine, for readable diagnostics.</summary>
	public static void DrawLineOnTop(Vector3 start, Vector3 end, Color color, float duration = 0.2f,
		float width = 0.004f)
	{
		// IDENTICAL TO DrawLine EXCEPT FOR WIDTH. AutoSeek's lines are always visible through
		// geometry and have been the whole time — so a working configuration already existed and
		// every attempt to build a better one (a different shader, a raised render queue, a
		// camera-facing depth bias) was solving a problem that only my own material clone had.
		// The lesson is the cheap one: when something in the codebase already does the thing,
		// call it rather than reconstructing it.
		DebugLine pooled = DebugLine.Create();
		LineRenderer lr = pooled.Renderer;
		lr.transform.position = start;
		lr.material = LINEMAT;
		lr.startColor = color;
		lr.endColor = color;
		lr.startWidth = width;
		lr.endWidth = width;
		lr.SetPosition(0, start);
		lr.SetPosition(1, end);
		lr.alignment = LineAlignment.View;
		pooled.gameObject.SetActive(value: true);
		pooled.Expire(duration);
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
