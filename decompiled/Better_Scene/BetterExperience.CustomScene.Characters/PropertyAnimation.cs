namespace BetterExperience.CustomScene.Characters;

internal class PropertyAnimation
{
	public string Id { get; protected set; }

	public string Name { get; protected set; }

	public AnimPropertyType PropType { get; protected set; }

	public PropertyAnimation(string id, string name)
	{
		Id = id;
		Name = name;
	}
}
