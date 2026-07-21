using System;
using UnityEngine;

namespace BetterExperience.Features.Overlay;

public class GridLayout : VHLayout
{
	private float colspacing;

	public GridLayout(float rowspacing = 0f, float colspacing = 0f)
	{
		base.Spacing = rowspacing;
		this.colspacing = colspacing;
	}

	public override void Fit()
	{
		foreach (HLayout<Drawable> row in base.Children)
		{
			if (!row.Transient)
			{
				row.PreferredSizing = null;
			}
		}
		base.Fit();
		float[] colsizing = new float[0];
		foreach (HLayout<Drawable> row2 in base.Children)
		{
			if (!row2.Transient)
			{
				if (row2.ElementSizing.Length > colsizing.Length)
				{
					Array.Resize(ref colsizing, row2.ElementSizing.Length);
				}
				for (int i = 0; i < row2.ElementSizing.Length; i++)
				{
					colsizing[i] = Mathf.Max(colsizing[i], row2.ElementSizing[i]);
				}
			}
		}
		foreach (HLayout<Drawable> row3 in base.Children)
		{
			row3.PreferredSizing = colsizing;
			row3.UpdateNativeComponent();
		}
		base.Fit();
		base.Dirty = false;
	}

	public override void NewLine()
	{
		base.NewLine();
		base.Children[base.Children.Count - 1].Spacing = colspacing;
	}
}
