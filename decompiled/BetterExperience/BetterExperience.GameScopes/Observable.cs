using System;

namespace BetterExperience.GameScopes;

public class Observable : BaseObservable<Action>
{
	public void Invoke()
	{
		InvokeDynamic();
	}
}
public class Observable<T> : BaseObservable<Action<T>>
{
	public void Invoke(T arg1)
	{
		InvokeDynamic(arg1);
	}
}
public class Observable<T1, T2> : BaseObservable<Action<T1, T2>>
{
	public void Invoke(T1 arg1, T2 arg2)
	{
		InvokeDynamic(arg1, arg2);
	}
}
public class Observable<T1, T2, T3> : BaseObservable<Action<T1, T2, T3>>
{
	public void Invoke(T1 arg1, T2 arg2, T3 arg3)
	{
		InvokeDynamic(arg1, arg2, arg3);
	}
}
