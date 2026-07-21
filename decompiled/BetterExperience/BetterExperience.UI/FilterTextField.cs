using BetterExperience.GameScopes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

public class FilterTextField : TextField
{
	private IVisualElementScheduledItem valueChangedTask;

	private Button clearBtn;

	private string placeholder;

	private Label placeholderElement;

	public Observable<string> ValueChanged { get; } = new Observable<string>();

	public string FilterValue { get; private set; }

	public int InputDelay { get; set; } = 500;

	public string Placeholder
	{
		get
		{
			return placeholder;
		}
		set
		{
			placeholder = value;
			InvalidatePlaceholder();
		}
	}

	public FilterTextField()
	{
		this.RegisterValueChangedCallback(OnChanged);
		base.textInputBase.StylePadding(1);
		int size = 20;
		clearBtn = new Button();
		clearBtn.style.position = Position.Absolute;
		clearBtn.style.width = size;
		clearBtn.style.height = size;
		clearBtn.SetVisible(value: false);
		clearBtn.text = "x";
		clearBtn.style.left = new Length(100f, LengthUnit.Percent);
		clearBtn.style.top = new Length(50f, LengthUnit.Percent);
		clearBtn.style.marginTop = -size / 2;
		clearBtn.style.marginLeft = -size - 5;
		RegisterCallback<MouseEnterEvent>(OnMouseEnter);
		RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
		Add(clearBtn);
		clearBtn.clicked += delegate
		{
			value = "";
		};
		placeholderElement = new Label();
		Add(placeholderElement);
		placeholderElement.style.position = Position.Absolute;
		placeholderElement.StyleMargin(0);
		placeholderElement.StylePadding(0);
		placeholderElement.style.marginTop = 1f;
		placeholderElement.style.marginLeft = 2f;
		placeholderElement.style.color = Color.gray;
	}

	private void OnMouseLeave(MouseLeaveEvent evt)
	{
		clearBtn.SetVisible(value: false);
	}

	private void OnMouseEnter(MouseEnterEvent evt)
	{
		clearBtn.SetVisible(value: true);
	}

	private void OnChanged(ChangeEvent<string> evt)
	{
		if (valueChangedTask != null)
		{
			valueChangedTask.Pause();
		}
		valueChangedTask = base.schedule.Execute(AfterValueChanged);
		valueChangedTask.ExecuteLater(InputDelay);
		InvalidatePlaceholder();
	}

	private void AfterValueChanged()
	{
		FilterValue = value;
		ValueChanged.Invoke(FilterValue);
	}

	private void InvalidatePlaceholder()
	{
		if (value == null || value == "")
		{
			placeholderElement.text = placeholder;
		}
		else
		{
			placeholderElement.text = "";
		}
		placeholderElement.style.fontSize = base.style.fontSize;
	}
}
