using System.Runtime.CompilerServices;

// SEEKLAB — the hot-reload testbed's two hooks into BetterExperience, kept in one file so that
// removing the testbed later is one deletion plus two guard lines, not an archaeology exercise.
//
// WHY A TESTBED EXISTS AT ALL. AutoSeek and AutoThrust are control loops. Control loops are not
// reasoned into correctness; they are tuned by iteration — change a gain, watch the response,
// change it again. Living inside this plugin, a single gain change cost a rebuild, a game
// RESTART, and walking the character back into position. Minutes per iteration, and the restart
// destroys the very state under investigation. That cost, not the difficulty of the control
// problem, has dominated this work: sessions have been spent re-establishing a scenario rather
// than studying it.
//
// So the two loops also exist as SeekLab.dll in BepInEx\scripts, which ScriptEngine reloads on
// F6 with no restart and no repositioning. SeekLab is the same source, copied.
[assembly: InternalsVisibleTo("SeekLab")]

namespace BetterExperience;

/// <summary>
/// Which assembly currently owns the pelvis.
///
/// The one thing that must never happen is BOTH copies running: two independent controllers
/// commanding <c>AddProfundidadDelta</c>/<c>AddVerticalDelta</c> on the same frame do not merely
/// fight — they produce a plausible-looking motion that belongs to neither control law, so the
/// measurements taken to judge either one are meaningless. That is strictly worse than a plain
/// failure, because it wastes the runs as well as the fix.
///
/// So SeekLab raises <see cref="ExternalOwner"/> as it loads and lowers it as it unloads, and
/// BE's own AutoSeeker/AutoThrust services stand down while it is raised — checked per tick
/// rather than at startup, because ScriptEngine can load and unload SeekLab at any moment and a
/// startup-time decision would be stale the instant F6 is pressed.
///
/// Standing down is not the same as being idle: a service frozen mid-sequence leaves the pelvis
/// holding whatever it last commanded. Each service therefore ABORTS its running sequence on the
/// transition into external ownership, exactly as if the user had pressed the stop hotkey.
///
/// This flag is deliberately not persisted and not configurable. It describes a fact about what
/// is loaded right now, and a stale saved value would silently disable the shipped feature for a
/// user who has never heard of the testbed.
/// </summary>
public static class SeekLabHandoff
{
	/// <summary>True while SeekLab.dll is loaded and owns placement/thrust.</summary>
	public static bool ExternalOwner;

	// THE LAB'S STATUS SURFACE LIVES HERE, NOT IN SEEKLAB, and that is the whole point of putting
	// it in this file. A reloaded assembly is a NEW assembly; the previous generation stays in the
	// AppDomain with identically-named types. A probe query for `T:SeekLabControl.Status` would
	// therefore have several equally valid answers, and the wrong one is a value nobody writes any
	// more — an instrument that silently reports the past. BetterExperience loads exactly once, so
	// a static here has exactly one instance for the life of the process and the query is
	// unambiguous by construction.
	//
	//     curl "http://localhost:8910/get?path=T:SeekLabHandoff.Status"

	/// <summary>One line describing what the lab is doing. Read this first.</summary>
	public static string Status = "not loaded";

	/// <summary>Live lab services currently attached to the guest scope.</summary>
	public static int Attached;

	/// <summary>
	/// Set true to tear down and re-attach the lab against the current guest without a reload —
	/// the recovery path when a scene change has left it detached.
	/// </summary>
	public static bool RequestReattach;

	/// <summary>Last error, empty when healthy. Attach failures are otherwise invisible.</summary>
	public static string LastError = "";
}
