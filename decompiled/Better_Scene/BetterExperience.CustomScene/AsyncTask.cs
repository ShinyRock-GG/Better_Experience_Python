using System;
using System.Threading.Tasks;

namespace BetterExperience.CustomScene;

public class AsyncTask
{
	public string Text { get; private set; }

	public Task Task { get; private set; }

	public AsyncTaskProgress Progress { get; private set; }

	public Action OnComplete { get; set; }

	public AsyncTask(string text, Task task, AsyncTaskProgress progress)
	{
		Text = text;
		Task = task;
		Progress = progress;
	}
}
