using System;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Features.Console;

public class ConsoleService : SessionService
{
	private OverlayService overlayService;

	private ConsoleWindow consoleWindow;

	private CommandProcessor commandProcessor = new CommandProcessor();

	public void RegisterCommand(Command command, ScopeSupport scope)
	{
		commandProcessor.Add(command, scope);
	}

	public void RegisterCommand(Func<string> command, ScopeSupport scope)
	{
		commandProcessor.Add(command, scope);
	}

	public void RegisterCommand<T>(Func<T, string> command, ScopeSupport scope)
	{
		commandProcessor.Add(command, scope);
	}

	public override void OnStart()
	{
		overlayService = base.Scope.Lookup<OverlayService>();
		consoleWindow = new ConsoleWindow();
		consoleWindow.RunCommand += ConsoleWindow_RunCommand;
		overlayService.AddAlwaysDrawable(consoleWindow.Drawable);
		Plugin.DoUpdate.Add(HandleInput, base.Scope);
	}

	private void ConsoleWindow_RunCommand(string obj)
	{
		consoleWindow.AddOutput(">" + obj);
		consoleWindow.AddOutput(commandProcessor.Run(obj));
	}

	private void HandleInput()
	{
		if (Input.GetKeyDown(KeyCode.BackQuote))
		{
			consoleWindow.Drawable.Visible = !consoleWindow.Drawable.Visible;
		}
		if (consoleWindow.Drawable.Visible && Input.GetKeyDown(KeyCode.UpArrow))
		{
			consoleWindow.RedoCommand();
		}
	}
}
