namespace BetterExperience.Features.Overlay;

public class TemporaryNotification
{
	public Drawable Drawable { get; set; }

	public DrawableContainer<Drawable> SpecificContainer { get; set; }

	public float Duration { get; set; }

	public float FadeOut { get; set; }

	public float FadeIn { get; set; }

	public float _timer { get; set; }
}
