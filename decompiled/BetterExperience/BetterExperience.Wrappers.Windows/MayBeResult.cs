using System;

namespace BetterExperience.Wrappers.Windows;

public class MayBeResult<T>
{
	private bool hasResult;

	private T result;

	private event Action<T> OnResultInternal = delegate
	{
	};

	public event Action<T> OnResult
	{
		add
		{
			OnResultInternal += value;
			if (hasResult)
			{
				value(result);
			}
		}
		remove
		{
			OnResultInternal -= value;
		}
	}

	public void SetResult(T value)
	{
		result = value;
		hasResult = true;
		this.OnResultInternal(value);
	}
}
