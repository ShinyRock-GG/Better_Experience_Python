using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BetterExperience.CustomScene.Packaging;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

internal class StoryLauncherWindow
{
	private PopupWindow wnd;

	private Button closeBtn;

	private VisualElement packagesWrapper;

	private VisualElement extensionsWrapper;

	private Button continueBtn;

	private Label preview;

	private CheckListView<Package> packages;

	private CheckListView<Package> extPackages;

	private Texture2D sharedTexture;

	private Label packageInfo;

	private Label dependenciesInfo;

	private Label packageDescription;

	private CheckBox singleModeToggle;

	private NewPackageWindow newPackageWindow;

	private FilterTextField packageFilter;

	public List<string> DisabledExtensions { get; set; } = new List<string>();

	public Package SelectedPackage { get; private set; }

	public IReadOnlyList<Package> SelectedExtensions => extPackages.Selection;

	public bool SingleMode => singleModeToggle.value;

	public bool IsVisible => wnd.style.display.value != DisplayStyle.None;

	public event Action Play = delegate
	{
	};

	public event Action Hidden = delegate
	{
	};

	public StoryLauncherWindow(VisualElement root)
	{
		wnd = new PopupWindow();
		wnd.text = "Story Selector";
		wnd.style.top = new Length(50f, LengthUnit.Percent);
		wnd.style.left = new Length(50f, LengthUnit.Percent);
		wnd.style.width = 800f;
		wnd.style.height = 600f;
		wnd.style.marginLeft = -400f;
		wnd.style.marginTop = -300f;
		VisualElement wrapper = new VisualElement();
		wrapper.style.height = 600f;
		wnd.Add(wrapper);
		VisualElement ve = new VisualElement();
		ve.style.flexGrow = 1f;
		wrapper.Add(ve);
		CreateUI(ve);
		VisualElement buttons = new VisualElement();
		wrapper.Add(buttons);
		CreateButtons(buttons);
		root.Add(wnd);
		sharedTexture = new Texture2D(2, 2);
		SelectPackage(null);
		newPackageWindow = new NewPackageWindow(root);
		newPackageWindow.Hide();
	}

	public void Show()
	{
		UIBuilder.Show((VisualElement)wnd);
	}

	public void Hide()
	{
		UIBuilder.Hide((VisualElement)wnd);
	}

	private void CreateButtons(VisualElement buttons)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Expected O, but got Unknown
		buttons.style.flexDirection = FlexDirection.Row;
		Button obj = new Button
		{
			text = "Close"
		};
		Button child = obj;
		closeBtn = obj;
		buttons.Add(child);
		closeBtn.style.backgroundColor = new Color(1f, 0.3f, 0.3f);
		closeBtn.clicked += delegate
		{
			SetVisible(v: false);
		};
		buttons.Add(singleModeToggle = new CheckBox("Single mode", check: true));
		singleModeToggle.tooltip = "Single interview or rating game";
		singleModeToggle.AddManipulator((IManipulator)new TooltipSupport((VisualElement)wnd));
		VisualElement spacer = new VisualElement();
		spacer.style.flexGrow = 1f;
		buttons.Add(spacer);
		Button obj2 = new Button
		{
			text = "Play"
		};
		child = obj2;
		continueBtn = obj2;
		buttons.Add(child);
		continueBtn.style.backgroundColor = new Color(0.3f, 1f, 0.3f);
		continueBtn.clicked += ContinueBtn_clicked;
	}

	private void ContinueBtn_clicked()
	{
		if (SelectedPackage != null)
		{
			this.Play();
		}
	}

	private void SetVisible(bool v)
	{
		wnd.style.display = ((!v) ? DisplayStyle.None : DisplayStyle.Flex);
		if (!v)
		{
			this.Hidden();
		}
	}

	internal void SetLastStoryId(string id)
	{
		foreach (CheckListView<Package>.CheckListRow<Package> pkg in packages.Items)
		{
			if (pkg.Item.Id == id)
			{
				packages.SetSelection(new Package[1] { pkg.Item });
				break;
			}
		}
	}

	private void CreateUI(VisualElement wnd)
	{
		wnd.style.flexDirection = FlexDirection.Row;
		wnd.style.flexWrap = Wrap.NoWrap;
		VisualElement lPane = new VisualElement();
		lPane.style.width = new Length(60f, LengthUnit.Percent);
		VisualElement rPane = new VisualElement();
		rPane.style.width = new Length(40f, LengthUnit.Percent);
		wnd.Add(lPane);
		wnd.Add(rPane);
		CreateLeftPane(lPane);
		CreateRightPane(rPane);
	}

	private void CreateLeftPane(VisualElement lPane)
	{
		lPane.style.flexDirection = FlexDirection.Column;
		preview = UIBuilder.Label(lPane, "");
		preview.style.height = 300f;
		preview.style.unityTextAlign = TextAnchor.MiddleCenter;
		preview.style.color = Color.white;
		preview.style.backgroundColor = Color.black;
		VisualElement desc = UIBuilder.VLayout(lPane);
		desc.style.flexGrow = 1f;
		packageInfo = UIBuilder.Label(desc, "");
		ScrollView scroll = UIBuilder.Scroll(desc);
		VisualElement wrapper = UIBuilder.VLayout((VisualElement)scroll);
		packageDescription = UIBuilder.Label(wrapper, "");
		dependenciesInfo = UIBuilder.Label(wrapper, "");
	}

	private static int NameSorter(string a, string b)
	{
		if (a == null)
		{
			if (b != null)
			{
				return -1;
			}
			return 0;
		}
		if (b == null)
		{
			return 1;
		}
		int minLength = Math.Min(a.Length, b.Length);
		for (int i = 0; i < minLength; i++)
		{
			char c1 = char.ToUpperInvariant(a[i]);
			char c2 = char.ToUpperInvariant(b[i]);
			bool alnum1 = char.IsLetterOrDigit(c1);
			bool alnum2 = char.IsLetterOrDigit(c2);
			if (alnum1 && !alnum2)
			{
				return -1;
			}
			if (alnum2 && !alnum1)
			{
				return 1;
			}
			int cmp = c1.CompareTo(c2);
			if (cmp != 0)
			{
				return cmp;
			}
		}
		return a.Length.CompareTo(b.Length);
	}

	private void CreateRightPane(VisualElement panel)
	{
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Expected O, but got Unknown
		panel.style.flexDirection = FlexDirection.Column;
		panel.Add(packagesWrapper = new ScrollView());
		panel.Add(extensionsWrapper = new ScrollView());
		packagesWrapper.style.height = new Length(60f, LengthUnit.Percent);
		VisualElement row = UIBuilder.HLayout(packagesWrapper);
		Button utilsBtn = UIBuilder.StyleHeight<Button>(UIBuilder.StyleWidth<Button>(UIBuilder.Button(row, ""), 10), 10);
		utilsBtn.clicked += delegate
		{
			GenericDropdownMenu genericDropdownMenu = new GenericDropdownMenu();
			PreparePackageUtilsMenu(genericDropdownMenu);
			genericDropdownMenu.DropDown(utilsBtn.worldBound, utilsBtn);
		};
		UIBuilder.Hide((VisualElement)utilsBtn);
		packageFilter = UIBuilder.AddElement<FilterTextField>(row, new FilterTextField
		{
			Placeholder = "Story Packages"
		});
		UIBuilder.StyleFlexGrow<FilterTextField>(packageFilter, 1f);
		packageFilter.ValueChanged.Add(delegate
		{
			FilterPackages();
		});
		extensionsWrapper.Add(new Label("Modification Packages"));
		extensionsWrapper.Add(extPackages = new CheckListView<Package>((Package p) => p.Name + ":" + p.Version));
		extPackages.SingleSelection = false;
		extPackages.style.backgroundColor = (Color)new Color32(150, 150, 150, byte.MaxValue);
		packagesWrapper.Add(packages = new CheckListView<Package>((Package p) => p.Name + ":" + p.Version));
		packages.style.backgroundColor = (Color)new Color32(150, 150, 150, byte.MaxValue);
		packages.SelectionChanged += Packages_SelectionChanged;
		packages.Sorter = Comparer<Package>.Create((Package a, Package b) => NameSorter(a.Name, b.Name));
	}

	private void FilterPackages()
	{
		string filter = packageFilter.FilterValue;
		if (filter != null)
		{
			filter = filter.Trim().ToLower();
			if (filter.Length == 0)
			{
				filter = null;
			}
		}
		packages.Filter(delegate(Package y)
		{
			if (filter == null)
			{
				return true;
			}
			return y.Name.ToLower().Contains(filter) || y.Id.ToLower().Contains(filter);
		});
	}

	private void PreparePackageUtilsMenu(GenericDropdownMenu gdm)
	{
		gdm.AddItem("New Package...", isChecked: false, newPackageWindow.Show);
	}

	private void Packages_SelectionChanged()
	{
		if (packages.Selection.Count == 0)
		{
			SelectPackage(null);
		}
		else
		{
			SelectPackage(packages.Selection[0]);
		}
	}

	public void SetPackages(IReadOnlyList<Package> packages)
	{
		this.packages.SetElements(packages);
		if (packages.Count != 0)
		{
			this.packages.SetSelection(new Package[1] { packages[0] });
		}
		else
		{
			this.packages.SetSelection(new Package[0]);
		}
		newPackageWindow.SetPackages(packages);
		FilterPackages();
	}

	public void SelectPackage(Package package)
	{
		SelectedPackage = package;
		preview.text = "";
		preview.style.backgroundImage = null;
		packageInfo.text = "";
		packageDescription.text = "";
		dependenciesInfo.text = "";
		continueBtn.SetEnabled(value: false);
		extPackages.SetElements(Array.Empty<Package>());
		if (package == null)
		{
			return;
		}
		byte[] bytes = package.LocalFS.Read("cover.jpg");
		if (bytes != null)
		{
			sharedTexture.LoadImage(bytes);
			preview.style.backgroundImage = sharedTexture;
			preview.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
		}
		else
		{
			preview.text = "[ No Picture Available ]";
		}
		packageInfo.text = package.Name + " by " + package.Manifest.author + " [" + package.Id + ":" + package.Version?.ToString() + "]";
		packageDescription.text = "Description:\n" + package.Manifest.description;
		dependenciesInfo.text = "Dependencies:";
		StringBuilder problems = null;
		if (package.ErrorDescription != null)
		{
			problems = new StringBuilder("\n\nProblems:");
			problems.Append("\n  ").Append(package.ErrorDescription);
		}
		else if (package.Dependencies.Count == 0)
		{
			dependenciesInfo.text = " None";
		}
		else
		{
			foreach (Package p in Enumerable.Reverse(package.Dependencies))
			{
				Label label = dependenciesInfo;
				label.text = label.text + "\n  " + p.Id + ":" + p.Version?.ToString() + " by " + p.Manifest.author;
				if (p.ErrorDescription != null)
				{
					if (problems == null)
					{
						problems = new StringBuilder("\n\nProblems:");
					}
					problems.Append("\n  ").Append(p.Id).Append(":")
						.Append(p.Version)
						.Append(" - ")
						.Append(p.ErrorDescription);
				}
			}
		}
		if (problems != null)
		{
			dependenciesInfo.text += problems.ToString();
		}
		continueBtn.SetEnabled(problems == null);
		if (package.AllDependencies != null)
		{
			Package[] exts = package.AllDependencies.Where((Package x) => x.Manifest.type == PackageType.extension).ToArray();
			extPackages.SetElements(exts);
			List<Package> enabled = exts.Where((Package x) => !DisabledExtensions.Contains(x.Id)).ToList();
			extPackages.SetSelection(enabled);
		}
		else
		{
			extPackages.SetElements(new List<Package>());
		}
	}
}
