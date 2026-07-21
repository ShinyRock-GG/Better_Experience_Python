using BetterExperience.GameScopes;

namespace BetterExperience.Wrappers.Characters;

public class GesturesWeights
{
	private bool _dirty;

	public Observable Changed { get; } = new Observable();

	public bool Dirty
	{
		get
		{
			return _dirty;
		}
		set
		{
			if (_dirty != value)
			{
				_dirty = value;
				if (_dirty)
				{
					Changed.Invoke();
				}
			}
		}
	}

	public bool Enabled { get; set; }

	public bool EyesOverride { get; set; }

	public EnumWeightArray<EyeExpressionType> EyeExpressions { get; } = new EnumWeightArray<EyeExpressionType>();

	public bool MouthOverride { get; set; }

	public EnumWeightArray<MouthGesture> MouthExpression { get; } = new EnumWeightArray<MouthGesture>();

	public bool FaceOverride { get; set; }

	public EnumWeightArray<FaceExpressionType> FaceExpression { get; } = new EnumWeightArray<FaceExpressionType>();

	public bool FaceBlendShapesOverride { get; set; }

	public StringWeightArray FaceBlendShapes { get; set; }

	public GesturesWeights(string[] faceKeysIndex)
	{
		FaceBlendShapes = new StringWeightArray(faceKeysIndex);
	}

	public void CopyTo(GesturesWeights target)
	{
		target.Enabled = Enabled;
		target.EyesOverride = EyesOverride;
		EyeExpressionType[] keys = EyeExpressions.Keys;
		foreach (EyeExpressionType k in keys)
		{
			target.EyeExpressions[k] = EyeExpressions[k];
		}
		target.MouthOverride = MouthOverride;
		MouthGesture[] keys2 = MouthExpression.Keys;
		foreach (MouthGesture k2 in keys2)
		{
			target.MouthExpression[k2] = MouthExpression[k2];
		}
		target.FaceOverride = FaceOverride;
		FaceExpressionType[] keys3 = FaceExpression.Keys;
		foreach (FaceExpressionType k3 in keys3)
		{
			target.FaceExpression[k3] = FaceExpression[k3];
		}
		target.FaceBlendShapesOverride = FaceBlendShapesOverride;
		string[] keys4 = FaceBlendShapes.Keys;
		foreach (string k4 in keys4)
		{
			target.FaceBlendShapes[k4] = FaceBlendShapes[k4];
		}
	}
}
