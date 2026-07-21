using System;
using System.Collections.Generic;

namespace BetterExperience.GameScopes;

public class ObservableValue<T> : Observable<T>
{
	private T _value;

	public T Value
	{
		get
		{
			return _value;
		}
		set
		{
			if (!EqualityComparer<T>.Default.Equals(_value, value))
			{
				_value = value;
				Invoke(_value);
			}
		}
	}

	public ObservableValue(T value)
	{
		_value = value;
	}

	public override void Add(Action<T> impl)
	{
		base.Add(impl);
		impl(_value);
	}
}
