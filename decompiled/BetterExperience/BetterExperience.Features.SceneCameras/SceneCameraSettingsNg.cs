using System.Collections.Generic;

namespace BetterExperience.Features.SceneCameras;

internal class SceneCameraSettingsNg
{
	public Dictionary<string, string> PoseToPose { get; set; } = new Dictionary<string, string>();

	public Dictionary<string, List<CameraSettings>> PoseCameras { get; set; } = new Dictionary<string, List<CameraSettings>>();
}
