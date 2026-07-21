namespace BetterExperience.CustomScene.Poser;

public class Posture
{
	private string id;

	public string Name { get; set; }

	public BoneConfiguration Configuration { get; set; }

	public string Id
	{
		get
		{
			if (id == null)
			{
				return Name;
			}
			return id;
		}
		set
		{
			id = value;
		}
	}

	public PosturePoseCollection Poses { get; set; }

	public PoseOrientation Orientation { get; internal set; }

	public PostureDescriptor Descriptor { get; set; }

	public virtual bool Is(Posture posture)
	{
		if (posture != null)
		{
			return Id == posture.Id;
		}
		return false;
	}
}
