namespace BetterExperience.CustomScene.Poser;

public class PostureData : BoneDispositionData
{
	public string Name { get; set; }

	public PoseOrientation Orientation { get; set; }

	public PostureData()
	{
	}

	public PostureData(Posture posture)
	{
		Name = posture.Name;
		base.Id = posture.Id;
		Orientation = posture.Orientation;
		ReadFrom(posture.Configuration);
	}

	public Posture ToPosture(string id)
	{
		Posture p = (id.Contains(".") ? new POIPosture() : new Posture());
		p.Name = Name;
		p.Orientation = Orientation;
		p.Configuration = new BoneConfiguration();
		WriteInto(p.Configuration);
		p.Poses = new PosturePoseCollection(p);
		return p;
	}
}
