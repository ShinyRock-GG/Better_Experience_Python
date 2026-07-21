using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BetterExperience.Features.Console;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers;
using UnityEngine;

namespace BetterExperience.Features;

internal class NaturalLanguageFeature : PluginFeature
{
	private class NatureLanguageProcessor : SessionService
	{
		[ConsoleCommand("Say something", new string[] { "say" })]
		public class CommandContext
		{
			[ConsoleCommandArg(Name = "Text to say", Key = "text", Mode = ConsoleArgMode.Tail)]
			public string Text { get; set; }
		}

		private DrawableTextBox textbox;

		private DockingContainer dock;

		private List<Expression<Action>> userExpressions;

		public NatureLanguageProcessor(List<Expression<Action>> userExpressions)
		{
			this.userExpressions = userExpressions;
		}

		public override void OnInit()
		{
			base.OnInit();
			ConsoleService console = Lookup<ConsoleService>();
			console.RegisterCommand<CommandContext>(CommandExecute, base.Scope);
			Plugin.DoUpdate.Add(HandleInput, base.Scope);
			GridLayout grid = new GridLayout();
			DrawableLabel label = grid.Label("Me:");
			textbox = grid.TextBox();
			textbox.OnSumbit += Textbox_OnSumbit;
			textbox.PreferredSize = new Vector2Int(300, 20);
			grid.Button("Say!").OnClick += NaturalLanguageProcessor_OnClick;
			dock = new DockingContainer(Vector2Int.down, grid);
			Lookup<OverlayService>().AddDrawable(dock, base.Scope);
			dock.Visible = false;
		}

		private void Textbox_OnSumbit()
		{
			GUI.FocusControl(null);
			NaturalLanguageProcessor_OnClick();
		}

		private void NaturalLanguageProcessor_OnClick()
		{
			if (dock.Visible)
			{
				if (base.Session.Guest != null)
				{
					Execute(textbox.Text);
				}
				textbox.Text = "";
				dock.Visible = false;
			}
		}

		private void HandleInput()
		{
			if (Input.GetKeyUp(KeyCode.Return) && base.Session.Guest != null && !dock.Visible)
			{
				dock.Visible = true;
				textbox.RequestFocus = true;
			}
		}

		public void Execute(string expr)
		{
			if (expr != null)
			{
				expr = expr.Trim();
				if (!(expr == ""))
				{
					expr = expr.ToLower();
					List<RadialMenu.RadialMenuEntry> menu = base.Session.Guest.RadialMenu.LoadMenu();
					Execute(expr, menu);
				}
			}
		}

		private void Execute(string expr, List<RadialMenu.RadialMenuEntry> menu)
		{
			List<Expression<Action>> flatmenu = FlattenMenuTree(menu);
			HashSet<string> exprtok = Tokenize(expr);
			flatmenu.AddRange(userExpressions);
			List<Tuple<int, List<Expression<Action>>>> results = ScoreExpressions(exprtok, flatmenu);
			if (results.Count == 0)
			{
				base.Session.Guest.HeadController.Say("What?");
				return;
			}
			Tuple<int, List<Expression<Action>>> r = results[0];
			if (r.Item2.Count == 1)
			{
				r.Item2[0].UserData();
				return;
			}
			string reply = "Can you be more specific? {0}?";
			reply = string.Format(reply, string.Join((r.Item2.Count == 2) ? " or " : ",", r.Item2.Select((Expression<Action> x) => x.Text).ToArray()));
			base.Session.Guest.HeadController.Say(reply);
		}

		public static HashSet<string> Tokenize(string text)
		{
			string t = Regex.Replace(text, "[^\\w]", " ");
			return new HashSet<string>(t.Split(new char[1] { ' ' }));
		}

		public string CommandExecute(CommandContext text)
		{
			Execute(text.Text);
			return "";
		}

		public List<Expression<Action>> FlattenMenuTree(List<RadialMenu.RadialMenuEntry> menus)
		{
			List<Expression<Action>> result = new List<Expression<Action>>();
			FlattenMenu(result, menus);
			return result;
		}

		private void FlattenMenu(List<Expression<Action>> result, List<RadialMenu.RadialMenuEntry> menus)
		{
			foreach (RadialMenu.RadialMenuEntry e in menus)
			{
				if (e.Children == null)
				{
					result.Add(CreateExpression(e));
				}
				else
				{
					FlattenMenu(result, e.Children);
				}
			}
		}

		private Expression<Action> CreateExpression(RadialMenu.RadialMenuEntry e)
		{
			Expression<Action> expr = new Expression<Action>();
			List<string> path = new List<string>();
			for (RadialMenu.RadialMenuEntry p = e; p != null; p = p.Parent)
			{
				string cleantext = Regex.Replace(p.Text, "[^\\w]", " ").Trim().ToLower();
				cleantext = string.Join(" ", cleantext.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
				path.Insert(0, cleantext);
			}
			string text = string.Join(" ", path);
			text = (expr.Text = text.Substring(0, 1).ToUpper() + text.Substring(1));
			expr.Tokens = Tokenize(text);
			expr.UserData = delegate
			{
				e.EmulateClick();
			};
			return expr;
		}

		private List<Tuple<int, List<Expression<T>>>> ScoreExpressions<T>(HashSet<string> token, List<Expression<T>> expressions)
		{
			Dictionary<int, List<Expression<T>>> tmp = new Dictionary<int, List<Expression<T>>>();
			foreach (Expression<T> e in expressions)
			{
				int score = token.Intersect(e.Tokens).Count();
				if (score > 0)
				{
					if (!tmp.TryGetValue(score, out var list))
					{
						list = (tmp[score] = new List<Expression<T>>());
					}
					list.Add(e);
				}
			}
			List<Tuple<int, List<Expression<T>>>> result = new List<Tuple<int, List<Expression<T>>>>();
			foreach (KeyValuePair<int, List<Expression<T>>> kv in tmp)
			{
				result.Add(new Tuple<int, List<Expression<T>>>(kv.Key, kv.Value));
			}
			result.Sort((Tuple<int, List<Expression<T>>> a, Tuple<int, List<Expression<T>>> b) => -a.Item1.CompareTo(b.Item1));
			return result;
		}
	}

	private class Expression<T>
	{
		public string Text { get; set; }

		public HashSet<string> Tokens { get; set; }

		public T UserData { get; set; }
	}

	private List<Expression<Action>> userExpressions = new List<Expression<Action>>();

	private ConfigEntry<bool> featureEnabled;

	public override bool Enabled => true;

	public override void Configure(ConfigFile config)
	{
		base.Configure(config);
		featureEnabled = config.Bind<bool>("Features", "NaturalLanguageFeature", false, "Natural Language Feature: Enable <enter> menu (restart required)");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(featureEnabled, base.Scope);
	}

	public override void OnStart()
	{
		base.OnStart();
		if (featureEnabled.Value)
		{
			Lookup<SessionTracker>().SessionServices.Add(() => new NatureLanguageProcessor(userExpressions));
		}
	}

	public void AddExpression(string command, Action action, ScopeSupport scope)
	{
		Expression<Action> expr = new Expression<Action>();
		expr.Text = command;
		expr.Tokens = NatureLanguageProcessor.Tokenize(command);
		expr.UserData = action;
		userExpressions.Add(expr);
		scope.OnDispose += delegate
		{
			userExpressions.Remove(expr);
		};
	}
}
