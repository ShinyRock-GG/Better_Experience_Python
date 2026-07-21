using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BetterExperience.GameScopes;

namespace BetterExperience.Features;

internal class MultithreadingFeature : PluginService
{
	public class ParallelFork
	{
		private MultithreadingFeature feature;

		private List<Action> joins = new List<Action>();

		public ParallelFork(MultithreadingFeature feature)
		{
			this.feature = feature;
		}

		internal void Join()
		{
			foreach (Action a in joins)
			{
				a();
			}
		}

		internal void Run<T>(Func<T> parallel, Action<T> sync)
		{
			Task<T> task = feature.Submit(parallel);
			joins.Add(delegate
			{
				task.Wait();
				sync(task.Result);
			});
		}

		internal void Run(Action parallel)
		{
			Task<object> task = feature.Submit(delegate
			{
				parallel();
				return (object)null;
			});
			joins.Add(delegate
			{
				task.Wait();
			});
		}
	}

	public Task<T> Submit<T>(Func<T> task)
	{
		if (Enabled)
		{
			return Task.Run(task);
		}
		return Task.FromResult(task());
	}

	internal ParallelFork CreateFork()
	{
		return new ParallelFork(this);
	}

	public Task<T> TrySubmit<T>(Func<T> task)
	{
		return Task.Run(task);
	}

	public ParallelFork Fork<T>(IEnumerable<T> enumerable, Action<T> processor)
	{
		ParallelFork fork = CreateFork();
		foreach (T t in enumerable)
		{
			fork.Run(delegate
			{
				processor(t);
			});
		}
		return fork;
	}

	public ParallelFork Fork<T, K>(IEnumerable<T> enumerable, Func<T, K> processor, Action<K> sync)
	{
		ParallelFork fork = CreateFork();
		foreach (T t in enumerable)
		{
			fork.Run(() => processor(t), sync);
		}
		return fork;
	}

	public ParallelFork Fork<T, K>(IEnumerable<T> enumerable, Func<T, K> processor, Action<T, K> sync)
	{
		ParallelFork fork = CreateFork();
		foreach (T t in enumerable)
		{
			fork.Run(() => processor(t), delegate(K r)
			{
				sync(t, r);
			});
		}
		return fork;
	}
}
