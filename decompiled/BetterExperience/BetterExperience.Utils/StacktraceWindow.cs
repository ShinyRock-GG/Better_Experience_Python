using System;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Utils;

internal class StacktraceWindow : DrawableWindow
{
	private DrawableLabel output;

	private DrawableButton closeBtn;

	public StacktraceWindow(Exception e, ScopeSupport scope)
		: base(1200, 600)
	{
		base.Text = "Stacktrace";
		output = Add(new DrawableScrollView
		{
			PreferredSize = new Vector2(base.ClientSize.x, base.ClientSize.y - 20f)
		}).Add(new DrawableLabel(""));
		closeBtn = Add(new DockingContainer(Vector2Int.down + Vector2Int.right)).Add(new DrawableButton());
		closeBtn.PreferredSize = new Vector2(base.ClientSize.x - 10f, 20f);
		closeBtn.Text = "Close";
		closeBtn.OnClick += scope.Dispose;
		SetStackTrace(e);
	}

	public void SetStackTrace(Exception e)
	{
		output.Text = e.ToString().Replace("--->", "\n--->");
	}
}
