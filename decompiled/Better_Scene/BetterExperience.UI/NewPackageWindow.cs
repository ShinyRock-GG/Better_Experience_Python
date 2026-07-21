using System;
using System.Collections.Generic;
using System.IO;
using BetterExperience.CustomScene.Packaging;
using UnityEngine.UIElements;
using static BetterExperience.UI.UIBuilder;

namespace BetterExperience.UI;

internal class NewPackageWindow
{
	private PopupWindow wnd;

	private DropdownField template;

	private TextField packageId;

	private TextField version;

	private TextField description;

	private TextField author;

	private IReadOnlyList<Package> packages;

	public NewPackageWindow(VisualElement root)
	{
		wnd = new PopupWindow();
		wnd.text = "New Package";
		wnd.style.top = new Length(50f, LengthUnit.Percent);
		wnd.style.left = new Length(50f, LengthUnit.Percent);
		wnd.style.width = 800f;
		wnd.style.height = 600f;
		wnd.style.marginLeft = -400f;
		wnd.style.marginTop = -300f;
		wnd.style.position = Position.Absolute;
		VisualElement wrapper = new VisualElement();
		wrapper.style.height = 600f;
		wnd.Add(wrapper);
		VisualElement ve = new VisualElement();
		ve.style.flexGrow = 1f;
		wrapper.Add(ve);
		CreateUI(ve);
		root.Add(wnd);
	}

	private void CreateUI(VisualElement ve)
	{
		VisualElement table = UIBuilder.VLayout(ve);
		int w0 = 100;
		TableBuilder val = UIBuilder.Row(table, (Action<VisualElement>)null);
		try
		{
			UIBuilder.StyleWidth<Label>(UIBuilder.Label(table, "Template"), w0);
			template = UIBuilder.StyleFlexGrow<DropdownField>(UIBuilder.Dropdown(table, (IEnumerable<string>)new string[1] { "None" }), 1f);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		TableBuilder val2 = UIBuilder.Row(table, (Action<VisualElement>)null);
		try
		{
			UIBuilder.StyleWidth<Label>(UIBuilder.Label(table, "Package ID"), w0);
			packageId = UIBuilder.StyleFlexGrow<TextField>(UIBuilder.TextBox(table, "mypackageid"), 1f);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		TableBuilder val3 = UIBuilder.Row(table, (Action<VisualElement>)null);
		try
		{
			UIBuilder.StyleWidth<Label>(UIBuilder.Label(table, "Author"), w0);
			author = UIBuilder.StyleFlexGrow<TextField>(UIBuilder.TextBox(table, ""), 1f);
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
		TableBuilder val4 = UIBuilder.Row(table, (Action<VisualElement>)null);
		try
		{
			UIBuilder.StyleWidth<Label>(UIBuilder.Label(table, "Version"), w0);
			version = UIBuilder.StyleFlexGrow<TextField>(UIBuilder.TextBox(table, "1.0.0"), 1f);
		}
		finally
		{
			((IDisposable)val4)?.Dispose();
		}
		TableBuilder val5 = UIBuilder.Row(table, (Action<VisualElement>)null);
		try
		{
			UIBuilder.StyleWidth<Label>(UIBuilder.Label(table, "Description"), w0);
			description = UIBuilder.StyleFlexGrow<TextField>(UIBuilder.TextBox(table, ""), 1f);
		}
		finally
		{
			((IDisposable)val5)?.Dispose();
		}
		VisualElement btns = UIBuilder.HLayout(ve);
		Button createBtn = UIBuilder.Button(btns, "Create");
		createBtn.clicked += CreateBtn_clicked;
		UIBuilder.StyleFlexGrow<Label>(UIBuilder.Label(btns, ""), 1f);
		Button closeBtn = UIBuilder.Button(btns, "Close");
		closeBtn.clicked += Hide;
	}

	private void CreateBtn_clicked()
	{
		string id = packageId.value.Trim();
		if (id.Length == 0)
		{
			return;
		}
		string verText = version.value.Trim();
		if (Version.TryParse(verText, out var _))
		{
			DirectoryInfo root = PackageManager.PackagesRoot;
			DirectoryInfo di = new DirectoryInfo(Path.Combine(root.FullName, id));
			if (!di.Exists)
			{
				PackageManifest manifest = new PackageManifest();
				manifest.id = id;
				manifest.author = author.value;
				manifest.version = verText;
			}
		}
	}

	public void Show()
	{
		Reset();
		wnd.BringToFront();
		UIBuilder.Show((VisualElement)wnd);
	}

	public void Hide()
	{
		UIBuilder.Hide((VisualElement)wnd);
	}

	private void Reset()
	{
		template.index = 0;
		packageId.value = "mypackageid";
		version.value = "1.0.0";
		author.value = "";
		description.value = "";
	}

	internal void SetPackages(IReadOnlyList<Package> packages)
	{
		this.packages = packages;
		List<string> choices = new List<string>();
		choices.Add("<None>");
		foreach (Package p in packages)
		{
			choices.Add(p.Id + ":" + p.Version.ToString());
		}
		template.choices = choices;
		template.index = 0;
	}
}
