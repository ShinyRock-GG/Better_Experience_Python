using UnityEngine;

namespace BetterExperience.Features.BetterHand;

internal class FloatAnimator
{
	private float from;

	private float to;

	private float dv;

	private float value;

	private bool stopped;

	public float Value => value;

	public bool Active => !stopped;

	public float To => to;

	public FloatAnimator(float from, float to, float duration)
	{
		this.from = from;
		this.to = to;
		dv = (to - from) / duration;
		Reset();
	}

	public float Update(float dt)
	{
		if (stopped)
		{
			return value;
		}
		float delta = dv * dt;
		value += delta;
		if (dv > 0f)
		{
			value = Mathf.Clamp(value, from, to);
		}
		else
		{
			value = Mathf.Clamp(value, to, from);
		}
		if (value == to)
		{
			stopped = true;
			from = to;
		}
		return value;
	}

	public void Reset()
	{
		value = from;
		stopped = false;
	}

	internal void Stop()
	{
		stopped = true;
	}
}
