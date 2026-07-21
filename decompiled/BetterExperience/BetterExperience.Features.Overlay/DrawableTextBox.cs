using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class DrawableTextBox : Drawable
{
	private TextField textField = new TextField();

	private string id = Guid.NewGuid().ToString();

	public string Text { get; set; }

	public bool RequestFocus { get; set; }

	public event Action OnSumbit = delegate
	{
	};

	public event Action OnTextChange = delegate
	{
	};

	public event Action OnArrowUp = delegate
	{
	};

	public DrawableTextBox()
	{
		textField.isReadOnly = false;
		base.NativeComponent = textField;
		textField.RegisterCallback(delegate(KeyDownEvent ke)
		{
			if (ke.keyCode == KeyCode.Return)
			{
				this.OnSumbit();
			}
			else if (ke.keyCode == KeyCode.UpArrow)
			{
				this.OnArrowUp();
			}
		});
	}

	protected override void OnDraw(DrawContext context)
	{
		textField.value = Text;
		if (context.Native && RequestFocus)
		{
			RequestFocus = false;
			textField.Focus();
		}
		if (context.Native)
		{
			return;
		}
		GUI.SetNextControlName(id);
		string newtext = GUI.TextField(context.CanvasRect, Text);
		if (newtext != Text)
		{
			Text = newtext;
			this.OnTextChange();
		}
		if (RequestFocus)
		{
			RequestFocus = false;
			GUI.FocusControl(id);
			return;
		}
		if (Event.current.isKey && Event.current.keyCode == KeyCode.Return && GUI.GetNameOfFocusedControl() == id)
		{
			this.OnSumbit();
		}
		if (Event.current.isKey && Event.current.keyCode == KeyCode.UpArrow && GUI.GetNameOfFocusedControl() == id)
		{
			this.OnArrowUp();
		}
	}
}
