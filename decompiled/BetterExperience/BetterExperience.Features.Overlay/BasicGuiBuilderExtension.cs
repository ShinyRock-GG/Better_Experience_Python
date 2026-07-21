using System;
using BetterExperience.GameScopes;

namespace BetterExperience.Features.Overlay;

public static class BasicGuiBuilderExtension
{
	private class NewRowOnDispose : IDisposable
	{
		private GridLayout layout;

		public NewRowOnDispose(GridLayout layout)
		{
			this.layout = layout;
		}

		public void Dispose()
		{
			layout.NewLine();
		}
	}

	public static DrawableLabel Label(this IGuiContainer<Drawable> builder, string text, ScopeSupport scope = null)
	{
		return builder.Add(new DrawableLabel(text), scope);
	}

	public static DrawableScrollView ScrollPane(this IGuiContainer<Drawable> builder, ScopeSupport scope = null)
	{
		return builder.Add(new DrawableScrollView(), scope);
	}

	public static GridLayout Grid(this IGuiContainer<Drawable> builder, float colspacing = 0f, float rowspacing = 0f, ScopeSupport scope = null)
	{
		return builder.Add(new GridLayout(rowspacing, colspacing), scope);
	}

	public static VLayout<Drawable> VLayout(this IGuiContainer<Drawable> builder, ScopeSupport scope = null)
	{
		return builder.Add(new VLayout<Drawable>(), scope);
	}

	public static HLayout<Drawable> HLayout(this IGuiContainer<Drawable> builder, ScopeSupport scope = null)
	{
		return builder.Add(new HLayout<Drawable>(), scope);
	}

	public static DrawableButton Button(this IGuiContainer<Drawable> builder, string text = null, ScopeSupport scope = null)
	{
		return builder.Add(new DrawableButton(text), scope);
	}

	public static DrawableTextBox TextBox(this IGuiContainer<Drawable> builder, string text = null, ScopeSupport scope = null)
	{
		return builder.Add(new DrawableTextBox
		{
			Text = text
		}, scope);
	}

	public static DrawableToggle Toggle(this IGuiContainer<Drawable> builder, string text, ScopeSupport scope = null)
	{
		return builder.Add(new DrawableToggle(text), scope);
	}

	public static DrawableSlider HSlider(this IGuiContainer<Drawable> builder, float value, float min, float max, ScopeSupport scope = null)
	{
		return builder.Add(new DrawableSlider(value, min, max), scope);
	}

	public static IDisposable Row(this GridLayout grid)
	{
		return new NewRowOnDispose(grid);
	}
}
