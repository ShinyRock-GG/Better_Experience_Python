using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assets;
using Assets._ReusableScripts;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.AI;
using Assets._ReusableScripts.CuchiCuchi.AI.Emociones.Handlers;
using Assets._ReusableScripts.CuchiCuchi.AI.Estimulos.ObjetosEstimulantes;
using Assets._ReusableScripts.CuchiCuchi.AI.Ropa;
using Assets._ReusableScripts.CuchiCuchi.Controllers.Ojos.Parpadeos;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.Camaras;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Ropa.Interacciones;
using Assets._ReusableScripts.CuchiCuchi.Estimulos;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using Assets._ReusableScripts.CuchiCuchi.Skins;
using Assets.Base.Behaviours.Runtime.Cameras;
using Assets.Productos.Juegos.Reception.Scripts.Dependientes.Controlladores;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.PyStory.AI;
using BetterExperience.PyStory.UI;
using BetterExperience.UI;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Characters;
using BetterExperience.Wrappers.Pools;
using BetterExperience.Wrappers.Windows;
using HarmonyLib;
using IronPython.Runtime;
using PixelCrushers.DialogueSystem.SequencerCommands;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.PyStory.Scripting;

public class PyStoryRuntime
{
	public class MayBeAdapter<T>
	{
		private MayBeResult<T> maybe;

		private bool resolved;

		public T Value { get; private set; }

		public MayBeAdapter(MayBeResult<T> maybe)
		{
			this.maybe = maybe;
			maybe.OnResult += delegate(T v)
			{
				Value = v;
				resolved = true;
			};
		}

		public IEnumerator Await(float timeout = 0f)
		{
			float start = Time.time;
			float abortAt = ((timeout > 0f) ? (start + timeout) : 0f);
			while (!resolved || (abortAt > 0f && Time.time >= abortAt))
			{
				yield return null;
			}
		}
	}

	private class CameraVision
	{
		private static readonly Plane[] PLANES = new Plane[6];

		private Camera camera;

		private IRopaManager ropaManager;

		private FemaleSkins.HitSkins.Partes hitskins;

		private Dictionary<HumanBodyPartsEng, StimulusTrackingService.StimulusFlags> parts = new Dictionary<HumanBodyPartsEng, StimulusTrackingService.StimulusFlags>();

		public CameraVision(Camera camera, IRopaManager ropaManager, FemaleSkins.HitSkins.Partes hitskins)
		{
			this.camera = camera;
			this.ropaManager = ropaManager;
			this.hitskins = hitskins;
		}

		public Dictionary<HumanBodyPartsEng, StimulusTrackingService.StimulusFlags> Capture()
		{
			StimulusTrackingService.StimulusFlags flags = StimulusTrackingService.StimulusFlags.none;
			if (IsSkinVisible(hitskins.senos000, out flags) || IsSkinVisible(hitskins.senos001, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.breasts, flags);
			}
			if (IsSkinVisible(hitskins.senos002, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.nipples, flags);
			}
			if (IsSkinVisible(hitskins.nalgas, out flags) || IsSkinVisible(hitskins.nalgaAperturas, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.buttocks, flags);
			}
			if (IsSkinVisible(hitskins.anusParedes, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.anus, flags);
			}
			if (IsSkinVisible(hitskins.clitoris))
			{
				AddVisiblePart(HumanBodyPartsEng.clit, StimulusTrackingService.StimulusFlags.none);
			}
			if (IsSkinVisible(hitskins.labiosVaginales, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.labia, flags);
			}
			if (IsSkinVisible(hitskins.vagParedes, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.vag, flags);
			}
			if (IsSkinVisible(hitskins.cabeza, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.head, flags);
			}
			if (IsSkinVisible(hitskins.brazos, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.arms, flags);
			}
			if (IsSkinVisible(hitskins.anteBrazos, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.forearms, flags);
			}
			if (IsSkinVisible(hitskins.manos, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.hands, flags);
			}
			if (IsSkinVisible(hitskins.canillas, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.thighs, flags);
			}
			if (IsSkinVisible(hitskins.piernas, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.legs, flags);
			}
			if (IsSkinVisible(hitskins.pies, out flags))
			{
				AddVisiblePart(HumanBodyPartsEng.feet, flags);
			}
			CaptureCameraFocusedPart();
			return parts;
		}

		private void CaptureCameraFocusedPart()
		{
			HitSkinBasica skin9 = default(HitSkinBasica);
			RaycastHit hit9 = default(RaycastHit);
			ParteDelCuerpoHumano parteDelCuerpoHumano = default(ParteDelCuerpoHumano);
			Side side9 = default(Side);
			if (!BodyPartEnumHelpler.ViendoParteDelCuerpo(camera.transform.position, camera.transform.forward, 10f, out skin9, out hit9, out parteDelCuerpoHumano, out side9, 1f))
			{
				return;
			}
			HumanBodyPartsEng part = (HumanBodyPartsEng)parteDelCuerpoHumano;
			if (!parts.ContainsKey(part))
			{
				StimulusTrackingService.StimulusFlags flags = StimulusTrackingService.StimulusFlags.focus;
				switch (side9)
				{
				case Side.L:
					flags |= StimulusTrackingService.StimulusFlags.left;
					break;
				case Side.R:
					flags |= StimulusTrackingService.StimulusFlags.right;
					break;
				default:
					Logger.Global.Info("Focus point {0} at {1}", part, side9);
					break;
				case Side.none:
					break;
				}
				AddVisiblePart(part, flags);
			}
		}

		private void AddVisiblePart(HumanBodyPartsEng part, StimulusTrackingService.StimulusFlags flags)
		{
			if (!ropaManager.Cubriendo((ParteDelCuerpoHumano)part))
			{
				flags |= StimulusTrackingService.StimulusFlags.bare;
			}
			parts.Add(part, flags);
		}

		private bool IsSkinVisible(HitSkinBasica hitSkin, out StimulusTrackingService.StimulusFlags flags)
		{
			flags = StimulusTrackingService.StimulusFlags.none;
			return SkinInFrame(camera, hitSkin) && SkinInVistaAny(camera.transform.position, hitSkin);
		}

		private bool IsSkinVisible<T>(FemaleSkins.HitSkins.ParBase<T> par, out StimulusTrackingService.StimulusFlags flags) where T : HitSkinBasica
		{
			flags = StimulusTrackingService.StimulusFlags.none;
			return SkinInFrameAny(camera, par) && SkinInVistaAny(camera.transform.position, par, ref flags);
		}

		private bool IsSkinVisible<Ta, Tb>(FemaleSkins.HitSkins.Duo<Ta, Tb> par) where Ta : HitSkinBasica where Tb : HitSkinBasica
		{
			return SkinInFrameAny(camera, par) && SkinInVistaAny(camera.transform.position, par);
		}

		private static bool SkinInVistaAny(Vector3 camPos, HitSkinBasica skin)
		{
			for (int i = 0; i < skin.skinColliders.Count; i++)
			{
				MeshCollider mc = skin.skinColliders[i] as MeshCollider;
				if (mc != null && !mc.convex)
				{
					return true;
				}
				if (BodyPartEnumHelpler.ViendoCollider(camPos, skin.skinColliders[i], out var _))
				{
					return true;
				}
			}
			return false;
		}

		private static bool SkinInVistaAny<T>(Vector3 camPos, FemaleSkins.HitSkins.ParBase<T> par, ref StimulusTrackingService.StimulusFlags flags) where T : HitSkinBasica
		{
			bool r = false;
			if (SkinInVistaAny(camPos, par.r))
			{
				r = true;
				flags |= StimulusTrackingService.StimulusFlags.right;
			}
			if (SkinInVistaAny(camPos, par.l))
			{
				r = true;
				flags |= StimulusTrackingService.StimulusFlags.left;
			}
			return r;
		}

		private static bool SkinInVistaAny<Ta, Tb>(Vector3 camPos, FemaleSkins.HitSkins.Duo<Ta, Tb> par) where Ta : HitSkinBasica where Tb : HitSkinBasica
		{
			return SkinInVistaAny(camPos, par.a) || SkinInVistaAny(camPos, par.b);
		}

		private static bool SkinInFrameAny<Ta, Tb>(Camera cam, FemaleSkins.HitSkins.Duo<Ta, Tb> par) where Ta : HitSkinBasica where Tb : HitSkinBasica
		{
			return ColliderInFrame(cam, par.a.skinColliders) || ColliderInFrame(cam, par.b.skinColliders);
		}

		private static bool SkinInFrameAny<T>(Camera cam, FemaleSkins.HitSkins.ParBase<T> par) where T : HitSkinBasica
		{
			return ColliderInFrame(cam, par.r.skinColliders) || ColliderInFrame(cam, par.l.skinColliders);
		}

		public static bool SkinInFrame(Camera cam, HitSkinBasica skin)
		{
			return ColliderInFrame(cam, skin.skinColliders);
		}

		private static bool ColliderInFrame(Camera cam, IReadOnlyList<Collider> cols)
		{
			if (cols.Count == 0)
			{
				return false;
			}
			Bounds bounds = cols[0].bounds;
			for (int i = 1; i < cols.Count; i++)
			{
				bounds.Encapsulate(cols[1].bounds);
			}
			GeometryUtility.CalculateFrustumPlanes(cam, PLANES);
			return GeometryUtility.TestPlanesAABB(PLANES, bounds);
		}
	}

	private Logger logger = Logger.Create<PyStoryRuntime>();

	private GameSession session;

	private POIManager poiManager;

	private InteractionManager interactionManager;

	private PoseManager poseManager;

	private DialogueManager dialogueManager;

	private OverlayService overlayService;

	private StoryManager storyManager;

	private bool pronounce_complete = false;

	private bool continue_clicked;

	private int last_response_key = -1;

	private GuestHeadController.LookAt scriptedLookAtTarget;

	private ScriptingContext scriptingContext;

	private Action<AnimatorFrameChangedEvent> basicAnimationListener;

	private Personalidad personalidad;

	public ScopeSupport Scope { get; }

	public POIManager _poiManager => poiManager;

	public InteractionManager _interactionManager => interactionManager;

	public PoseManager _poseManager => poseManager;

	public GameSession Session => session;

	public Story Story => storyManager.Current;

	internal PyStoryRuntime(GameSession session, ScopeSupport scope, ScriptingContext scriptingContext)
	{
		this.session = session;
		Scope = scriptingContext.ScriptingScope;
		this.scriptingContext = scriptingContext;
		storyManager = scope.Lookup<StoryManager>();
		Story.SceneScopeCreated.Add(InitSceneScope, Scope);
		if (Story.SceneScope != null)
		{
			InitSceneScope();
		}
		storyManager.Current.InterviewScopeCreated.Add(InitInterviewScope, Scope);
		if (Story.SceneInterviewScope != null)
		{
			InitInterviewScope();
		}
		Scope.OnDispose += delegate
		{
			if (interactionManager != null)
			{
				interactionManager.InterruptInteraction();
			}
		};
	}

	private void InitInterviewScope()
	{
		interactionManager = storyManager.Current.SceneInterviewScope.Lookup<InteractionManager>();
		scriptedLookAtTarget = session.Guest.HeadController.CreateLookAt(scriptingContext.ScriptingScope);
		scriptingContext.StrandEpilogueGens.Add(StrandEpilogue);
		interactionManager.AnimationController.OnActiveFrameChanged.Add(OnArmatureActiveFrameChanged, Scope);
		personalidad = ((Component)(object)Session.Guest.Impl).GetComponentInChildren<Personalidad>();
	}

	private void InitSceneScope()
	{
		poiManager = storyManager.Current.SceneScope.Lookup<POIManager>();
		poseManager = storyManager.Current.SceneScope.Lookup<PoseManager>();
		dialogueManager = storyManager.Current.SceneScope.Lookup<DialogueManager>();
		overlayService = storyManager.Current.SceneScope.Lookup<OverlayService>();
		dialogueManager.OnRespond.Add(delegate(string key)
		{
			last_response_key = int.Parse(key);
		}, Scope);
		dialogueManager.OnPronounceComplete.Add(delegate
		{
			pronounce_complete = true;
		}, Scope);
		dialogueManager.OnContinue.Add(delegate
		{
			continue_clicked = true;
		});
	}

	private void OnArmatureActiveFrameChanged(AnimatorFrameChangedEvent evt)
	{
		if (basicAnimationListener != null)
		{
			try
			{
				basicAnimationListener(evt);
			}
			catch (Exception e)
			{
				scriptingContext.ScriptingScope.NotifyCrash(e);
			}
		}
	}

	public void set_basic_animation_listener(Action<AnimatorFrameChangedEvent> listener)
	{
		basicAnimationListener = listener;
	}

	private IEnumerator StrandEpilogue(PyStrand strand)
	{
		yield return null;
		if (strand.HasDialogueSequence)
		{
			IEnumerator it = stop_dialogue();
			while (it.MoveNext())
			{
				yield return it;
			}
		}
	}

	public object guest_goto_poi(string poiId, int orientation = 2)
	{
		PointOfInterest poi = poiManager.FindPOI(poiId);
		if (poi == null)
		{
			logger.Error("Poi {0} doest not exist", poiId);
			return null;
		}
		Interaction interaction = new Interaction();
		interaction.DisplayName = "Goto " + poiId;
		interaction.SourcePosture = interactionManager.CurrentPosture;
		interaction.TargetPosture = poseManager.StandingPostureAt(poi);
		interaction.Enqueue(new GotoOp(poi, (PoseOrientation)orientation));
		interaction.Enqueue(new SetPostureOp(poseManager.StandingPostureAt(poi)));
		interaction.Enqueue(new AnimateOp(poseManager.StandingPosture.Poses.IdlePoses));
		interactionManager.StartInteraction(interaction);
		return new WaitUntil(() => !interactionManager.HasActiveInteraction && interactionManager.CurrentPlace != null && interactionManager.CurrentPlace.POI == poi);
	}

	public object guest_teleport_poi(string poiId, int orientation = 2)
	{
		PointOfInterest poi = poiManager.FindPOI(poiId);
		if (poi == null)
		{
			logger.Error("Poi {0} doest not exist", poiId);
			return null;
		}
		Interaction interaction = new Interaction();
		interaction.DisplayName = "Teleport " + poiId;
		interaction.SourcePosture = interactionManager.CurrentPosture;
		interaction.TargetPosture = poseManager.StandingPostureAt(poi);
		interaction.Enqueue(new TeleportOp(poi, (PoseOrientation)orientation));
		interaction.Enqueue(new SetPostureOp(poseManager.StandingPostureAt(poi)));
		interaction.Enqueue(new LambdaOp(delegate(InteractionContext ctx)
		{
			ctx.AnimationController.InterruptPose("Standing pose");
		}));
		interactionManager.StartInteraction(interaction);
		return new WaitUntil(() => !interactionManager.HasActiveInteraction && interactionManager.CurrentPlace != null && interactionManager.CurrentPlace.POI == poi);
	}

	public object apply_posture(string postureId)
	{
		string poiId = interactionManager.CurrentPlace.POI.Id;
		if (!poseManager.POIPostures.TryGetValue(poiId, out var poses))
		{
			logger.Error("(1) Posture {0} does not exist at poi {1}", postureId, poiId);
			return null;
		}
		if (!poses.ExactPostures.TryGetValue(postureId, out var posture) && !poses.ExactPostures.TryGetValue(poiId + "." + postureId, out posture))
		{
			if (postureId == "Stand")
			{
				posture = poseManager.StandingPostureAt(interactionManager.CurrentPlace.POI);
			}
			else
			{
				string[] p = postureId.Split(new char[1] { '.' });
				if (p.Length != 3 && (p.Length != 2 || !postureId.StartsWith("Stand.")))
				{
					logger.Error("(2) Posture {0} does not exist at poi {1}", postureId, poses.DefaultPosture.PoiId);
					return null;
				}
				string targetPoiId = p[1];
				if (!poseManager.POIPostures.TryGetValue(targetPoiId, out poses))
				{
					logger.Error("(3) Posture {0} does not exist at poi {1}", postureId, targetPoiId);
					return null;
				}
				if (!poses.ExactPostures.TryGetValue(postureId, out posture))
				{
					logger.Error("(4) Posture {0} does not exist at poi {1}", postureId, poses.DefaultPosture.PoiId);
					return null;
				}
			}
		}
		PoseOrientation finalOrientation = ((!poseManager.StandingPosture.Is(posture)) ? PoseOrientation.UNIVERSAL : PoseOrientation.FRONT);
		Interaction interaction = interactionManager.CreatePostureChangeInteraction(interactionManager.CreateQueryContext(ignoreOrientation: true, finalOrientation), posture);
		if (interaction == null)
		{
			logger.Error("Posture change interaction was not created. See logs above for details");
			return null;
		}
		interactionManager.StartInteraction(interaction);
		return new WaitUntil(() => !interactionManager.HasActiveInteraction);
	}

	public object play_clip(object name_or_clip, float blendingTime = -1f, List<AnimatorLayer> layers = null, AnimationCompletionMode completionMode = AnimationCompletionMode.Default, string label = null)
	{
		POIPosture posture = interactionManager.CurrentPosture;
		if (name_or_clip == null)
		{
			if (layers == null || layers.Contains(AnimatorLayer.Additive))
			{
				PosturePoseCollection poses = interactionManager.CurrentPosture.Poses;
				InteractionManager.InteractionQueryContext qctx = interactionManager.CreateQueryContext();
				Interaction i = interactionManager.CreatePlayClipInteraction(qctx, "Idle", poses.IdlePoses);
				if (interactionManager.StartInteraction(i))
				{
					return new WaitUntil(() => !interactionManager.HasActiveInteraction);
				}
				logger.Error("Failed to interrupt animation clip at primary layer");
				return null;
			}
			for (int i2 = 10; i2 > 0; i2--)
			{
				AnimatorLayer layer = (AnimatorLayer)i2;
				if (layers.Contains(layer))
				{
					IAnimationClipState additiveClip = interactionManager.AnimationController.GetPlayingClipByLayer(AnimatorLayer.Face);
					if (additiveClip != null)
					{
						additiveClip.FadeOut();
						logger.Error("Fade out clip {0} at {1}", additiveClip.Clip.FullName, layer);
					}
				}
			}
			return null;
		}
		InteractionManager.InteractionQueryContext ctx = interactionManager.CreateQueryContext();
		List<PoseAnimationClip> clips;
		if (name_or_clip is string name)
		{
			clips = ctx.CurrentPosture.Poses.FindClips(name);
			if (name == "Idle" && clips.Count == 0)
			{
				clips = ctx.CurrentPosture.Poses.FindClips("Binding");
			}
			if (clips.Count == 0 && layers != null && layers.Count > 0 && !layers.Contains(AnimatorLayer.Primary))
			{
				clips = poseManager.FindClips(name);
			}
			if (clips.Count == 0)
			{
				logger.Error("Unable to start animation named {0}. No clips found.", name);
				return null;
			}
		}
		else
		{
			if (!(name_or_clip is PoseAnimationClip pac))
			{
				throw new ArgumentException("cannot interpret " + name_or_clip.GetType().Name + " as clip");
			}
			clips = new List<PoseAnimationClip>();
			clips.Add(pac);
		}
		Interaction interaction = interactionManager.CreatePlayClipInteraction(ctx, clips[0].Name, clips, blendingTime, layers, completionMode, label);
		if (interactionManager.StartInteraction(interaction))
		{
			return new WaitUntil(() => !interactionManager.HasActiveInteraction);
		}
		logger.Error("Unable to start animation. See logs above for details.");
		return null;
	}

	public bool is_guest_busy()
	{
		return interactionManager.HasActiveInteraction;
	}

	private void InitDialogueManagerContext()
	{
		if (PyStrandFrame.CurrentFrame != null)
		{
			PyStrandFrame.CurrentFrame.Strand.HasDialogueSequence = true;
		}
		if (!dialogueManager.IsActive)
		{
			dialogueManager.SetActive(value: true);
		}
	}

	public object dialogue(string who, string text)
	{
		InitDialogueManagerContext();
		if (dialogueManager.ShowingSubtitle)
		{
			return wait_to_display(who, text);
		}
		pronounce_complete = false;
		dialogueManager.SetResponses(new List<DialogueResponse>());
		dialogueManager.SetSubtitle(who, text);
		return new WaitUntil(() => pronounce_complete);
	}

	private IEnumerator wait_to_display(string who, string text)
	{
		continue_clicked = false;
		dialogueManager.SetRequestContinuation();
		while (!continue_clicked)
		{
			yield return null;
		}
		dialogueManager.ClearSubtitle();
		yield return dialogue(who, text);
	}

	public IEnumerator stop_dialogue()
	{
		if (!dialogueManager.IsActive)
		{
			yield break;
		}
		if (dialogueManager.ShowingSubtitle)
		{
			continue_clicked = false;
			dialogueManager.SetRequestContinuation();
			while (!continue_clicked)
			{
				yield return null;
			}
			dialogueManager.ClearSubtitle();
		}
		dialogueManager.SetActive(value: false);
		if (PyStrandFrame.CurrentFrame != null)
		{
			PyStrandFrame.CurrentFrame.Strand.HasDialogueSequence = false;
		}
	}

	public object dialogue_response(PythonList responses)
	{
		InitDialogueManagerContext();
		List<DialogueResponse> dialogueResponses = new List<DialogueResponse>();
		for (int i = 0; i < responses.Count; i++)
		{
			object resp = responses[i];
			if (resp is string str)
			{
				dialogueResponses.Add(new DialogueResponse
				{
					Label = str,
					Key = i.ToString()
				});
			}
			else if (resp != null)
			{
				logger.Error("Unexpected dialog response type {0}", resp.GetType());
			}
		}
		dialogueManager.SetResponses(dialogueResponses);
		last_response_key = -1;
		if (dialogueResponses.Count == 0)
		{
			return null;
		}
		return new WaitUntil(() => last_response_key != -1);
	}

	public int get_last_response()
	{
		return last_response_key;
	}

	public object find_go_by_name(GameObject root, params string[] name)
	{
		for (int i = 0; i < name.Length; i++)
		{
			if (root == null)
			{
				root = GameObject.Find(name[i]);
			}
			else
			{
				Transform t = root.transform.FindDeepChild(name[i]);
				root = ((!(t != null)) ? null : t.gameObject);
			}
			if (root == null)
			{
				break;
			}
		}
		return root;
	}

	public InteractiveObject make_interactive(GameObject obj)
	{
		InteractiveObject existing = obj.GetComponentInChildren<InteractiveObject>(includeInactive: true);
		if ((bool)existing)
		{
			existing.ScriptingScope = scriptingContext.ScriptingScope;
			return existing;
		}
		Transform it = UnityUtils.NewTransform("InteractiveTarget", obj.transform, Scope);
		it.localPosition = Vector3.zero;
		it.localRotation = Quaternion.identity;
		it.localScale = Vector3.one;
		InteractiveObject io = it.gameObject.AddComponent<InteractiveObject>();
		io.ScriptingScope = scriptingContext.ScriptingScope;
		return io;
	}

	public PyStrand get_main_strand()
	{
		return scriptingContext.MainStrand;
	}

	public bool can_invoke_immediate(PyStrand strand)
	{
		return strand.Frames.Count == 0;
	}

	public bool invoke_next(IEnumerable e, PyStrand strand)
	{
		scriptingContext.SpawnNext(e.GetEnumerator(), strand);
		return true;
	}

	public bool invoke_last(IEnumerable e, PyStrand strand)
	{
		scriptingContext.SpawnLast(e.GetEnumerator(), strand);
		return true;
	}

	public PyStrand new_strand()
	{
		return new PyStrand(scriptingContext.ScriptingScope);
	}

	public InteractiveObject install_character_interaction()
	{
		Transform activator = ((Component)(object)session.Guest.Impl).transform.FindDeepChild("DonaActivator");
		if (activator == null)
		{
			throw new Exception("Unable to find activator");
		}
		activator.gameObject.SetActive(value: false);
		scriptingContext.ScriptingScope.OnDispose += delegate
		{
			if ((bool)activator && (bool)activator.gameObject)
			{
				activator.gameObject.SetActive(value: true);
			}
		};
		Transform head = session.Guest.Puppet.PuppetMaster.transform.FindDeepChild("CC_Base_Head");
		if (head == null)
		{
			throw new Exception("Unable to find head");
		}
		InteractiveObject existing = head.GetComponentInChildren<InteractiveObject>();
		if (existing != null)
		{
			existing.ScriptingScope = scriptingContext.ScriptingScope;
			return existing;
		}
		Transform t = UnityUtils.NewTransform("A", head, Scope);
		t.localPosition = Vector3.zero;
		t.localRotation = Quaternion.identity;
		InteractiveObject io = t.gameObject.AddComponent<InteractiveObject>();
		io.ScriptingScope = scriptingContext.ScriptingScope;
		SphereCollider sphere = io.GetComponent<SphereCollider>();
		if (sphere != null)
		{
			sphere.radius *= 1.5f;
		}
		return io;
	}

	public object get_guest_interaction_context()
	{
		return interactionManager.CreateQueryContext();
	}

	public List<SimpleCloth> enumerate_clothes(bool ignoreHidden = true)
	{
		List<SimpleCloth> result = new List<SimpleCloth>();
		IRopaManager manager = ((Component)(object)Session.Guest.Impl).GetComponentInChildren<IRopaManager>();
		ICollection<string> ids = new HashSet<string>();
		manager.ObtenerPiezasIDs(ids, ignoreHidden);
		RopaParaAvatarUnificado mapa = AsyncSingleton<RopaParaAvatarUnificado>.instance;
		if (mapa != null)
		{
			foreach (string id in ids)
			{
				MapaDeRopa.RopaData ropa = mapa.ObtenerData(id);
				result.Add(new SimpleCloth(ropa));
			}
		}
		return result;
	}

	public void take_off_cloth(string id)
	{
		IRopaManager manager = ((Component)(object)Session.Guest.Impl).GetComponentInChildren<IRopaManager>();
		manager.OcultarPieza(id, ocultar: true, null);
	}

	public IEnumerator expose_cloth(string id)
	{
		FemaleChar impl = Session.Guest.Impl;
		ControlladorDeGuiasDeInteraccionDeRopa ctl = ((Component)(object)impl).GetComponentInChildren<ControlladorDeGuiasDeInteraccionDeRopa>();
		RopaParaAvatarUnificado mapa = AsyncSingleton<RopaParaAvatarUnificado>.instance;
		IInteraccionesDeCharacter interactions = ((Component)(object)impl).GetComponentInChildren<IInteraccionesDeCharacter>();
		if (interactions == null)
		{
			logger.Error("No IInteraccionesDeCharacter");
			yield break;
		}
		MapaDeRopa.RopaData ropaData = mapa.ObtenerData(id);
		if (ropaData == null)
		{
			logger.Error("Ropa not found: {0}", id);
			yield break;
		}
		RopaInteractableInteraccion intHandR = null;
		RopaInteractableInteraccion intHandL = null;
		RopaInteractableInteraccion intHandLR = null;
		// TryObtenerSiEsValida now takes int (enum value) directly — __InteraccionName_Ext source generator removed in SMA 23.1
		if (interactions.TryObtenerSiEsValida(13, out var tmpInt))
		{
			intHandR = ((Component)(object)tmpInt).GetComponent<RopaInteractableInteraccion>();
		}
		if (interactions.TryObtenerSiEsValida(14, out tmpInt))
		{
			intHandL = ((Component)(object)tmpInt).GetComponent<RopaInteractableInteraccion>();
		}
		if (interactions.TryObtenerSiEsValida(15, out tmpInt))
		{
			intHandLR = ((Component)(object)tmpInt).GetComponent<RopaInteractableInteraccion>();
		}
		GenericAgarranteObjeto lHand = Session.Guest.Puppet.PuppetMaster.GetMuscle(HumanBodyBones.LeftHand).rigidbody.GetComponentInChildren<GenericAgarranteObjeto>();
		GenericAgarranteObjeto rHand = Session.Guest.Puppet.PuppetMaster.GetMuscle(HumanBodyBones.RightHand).rigidbody.GetComponentInChildren<GenericAgarranteObjeto>();
		List<(GuiaDeRopaInteractable, GuiaDeRopaInteractable, GuiaDeRopaInteractable)> result = new List<(GuiaDeRopaInteractable, GuiaDeRopaInteractable, GuiaDeRopaInteractable)>();
		SequencerCommandQuitarPiezaRopaCurrentClickedSend.GetInteractable(ctl, mapa, id, result);
		foreach (var cmd in result)
		{
			logger.Info("Starting expose");
			if ((UnityEngine.Object)(object)cmd.Item3 != null && (UnityEngine.Object)(object)intHandLR != null)
			{
				bool[] completion = new bool[1];
				((MonoBehaviour)(object)ctl).StartCoroutine(__play_interaction(cmd.Item3, intHandLR, lHand, completion));
				while (!completion[0])
				{
					yield return null;
				}
			}
			else
			{
				bool[] c1 = new bool[1];
				bool[] c2 = new bool[1];
				((MonoBehaviour)(object)ctl).StartCoroutine(__play_interaction(cmd.Item1, intHandR, rHand, c1));
				((MonoBehaviour)(object)ctl).StartCoroutine(__play_interaction(cmd.Item2, intHandL, lHand, c2));
				while (!c1[0] || !c2[0])
				{
					yield return null;
				}
			}
			logger.Info("Ending expose");
		}
		logger.Info("Exiting expose");
	}

	private IEnumerator __play_interaction(GuiaDeRopaInteractable target, RopaInteractableInteraccion interaction, GenericAgarranteObjeto agarrante, bool[] completion)
	{
		if ((UnityEngine.Object)(object)target.interactableCon != null)
		{
			interaction.FollowStartPose(target);
			yield return null;
			if (interaction.interaccion.Ejecutar(int.MaxValue, -1f, ControllerPrioridadConfig.prioridad, 1f, 1f, false))
			{
				while (interaction.interaccion.currentEstado.EstadosTimerWeigthPromedio() < 1f)
				{
					yield return null;
				}
				agarrante.ComenzarAgarrar(target.start.position);
				target.ForzarAgarrante(agarrante);
				interaction.StartFollowing(target);
				// interaction.w (follow weight) removed in SMA 23.1 — wait for blend via fixed coroutine
				float _wTimer = 0f;
				while (_wTimer < 0.5f)
				{
					_wTimer += UnityEngine.Time.deltaTime;
					yield return null;
					agarrante.ActualizarAgarrarPosition(interaction.worldPosition);
				}
				if ((UnityEngine.Object)(object)target.currentAgarradoPor == (UnityEngine.Object)(object)agarrante)
				{
					yield return target.EfectuarInteraccion();
				}
				yield return new WaitForSeconds(0.2f);
				agarrante.FinalizarAgarrado();
				interaction.StopFollowing();
				yield return new WaitForSeconds(0.333f);
				interaction.interaccion.Detener();
				yield return new WaitForSeconds(0.1f);
			}
			else
			{
				logger.Error("Field to execute interaction at RopaInteractableInteraccion");
			}
		}
		completion[0] = true;
	}

	public void set_eye_expression(EyeExpression expression, float duration)
	{
		OjosExpresionController ctl = Session.Guest.EyesExpressionComponent;
		ctl.Cambiar((OjosExpresionController.Tipo)expression, int.MaxValue, duration, ControllerPrioridadConfig.prioridad);
	}

	public void set_look_at_target(Transform target, float duration)
	{
		scriptedLookAtTarget.Enabled = target != null;
		if (target != null)
		{
			scriptedLookAtTarget.Transform = target;
		}
	}

	public void send_notification(string text, float duration, float fadeOut)
	{
		overlayService.InfoMessage(text, duration, fadeOut);
	}

	public object execute_interaction(Interaction i)
	{
		if (interactionManager.StartInteraction(i))
		{
			return new WaitUntil(() => !interactionManager.HasActiveInteraction);
		}
		logger.Error("Failed to start interaction {0}", i.DisplayName);
		return null;
	}

	public IList<Interaction> enumerate_transitions(bool clip_change = true, bool posture_change = true)
	{
		return interactionManager.EnumerateTransitions(clip_change, posture_change).ToArray();
	}

	public IList<Interaction> enumerate_gotos()
	{
		return interactionManager.EnumerateGotos().ToArray();
	}

	public void gio_apply_genes_from_package(string location)
	{
		if (storyManager.Current == null)
		{
			logger.Error("No active story");
			return;
		}
		VirtIO vfs = storyManager.Current.VFS;
		List<GeneInfo> update = new List<GeneInfo>();
		if (location != null)
		{
			byte[] blob = vfs.Read(location);
			if (blob != null)
			{
				Dictionary<string, Dictionary<string, float>> data = GlobalPersistenceService.Deserialize<Dictionary<string, Dictionary<string, float>>>(Encoding.UTF8.GetString(blob));
				data.Values.ForEach(delegate(Dictionary<string, float> x)
				{
					x.ForEach(delegate(KeyValuePair<string, float> KeyValuePair)
					{
						update.Add(new GeneInfo
						{
							Id = new GeneId(KeyValuePair.Key),
							Value = KeyValuePair.Value
						});
					});
				});
			}
			else
			{
				logger.Error("Unable to read file {0}", location);
			}
		}
		if (update.Count > 0)
		{
			Session.Guest.GuestInstance.UpdateAll((IList<GeneInfo>)update);
		}
	}

	public byte[] read_package_bytea(string location)
	{
		if (storyManager.Current == null)
		{
			logger.Error("No active story");
			return null;
		}
		VirtIO vfs = storyManager.Current.VFS;
		return vfs.Read(location);
	}

	public string read_png_json_bytea(byte[] bytes, string magic)
	{
		if (bytes == null)
		{
			logger.Error("read_png_json_bytea: null bytearray");
			return null;
		}
		if (bytes == null)
		{
			logger.Error("read_png_json_bytea: null magic");
			return null;
		}
		string result = ImageIO.ReadJsonPngFromMemory(bytes, Encoding.UTF8.GetBytes(magic));
		if (result == null)
		{
			logger.Error("read_png_json_bytea: failed to extract data");
		}
		return result;
	}

	public void call_with_exception_trap(Func<object> x)
	{
		try
		{
			x();
		}
		catch (Exception e)
		{
			scriptingContext.ScriptingScope.NotifyCrash(e);
		}
	}

	public void move_poi(string poiId, Vector3 pos, Quaternion rot)
	{
		PointOfInterest poi = poiManager.FindPOI(poiId);
		if (poi == null)
		{
			logger.Error("Poi {0} does not exist", poiId);
			return;
		}
		if (interactionManager.CurrentPlace.POI == poi)
		{
			logger.Warn("Updating ocuppied POI is a bad idea");
		}
		poi.Transform.position = pos;
		poi.Transform.rotation = rot;
	}

	public IEnumerable<(string, Func<object>)> find_package_assets(string ext, bool asString = false, bool pathAsId = false)
	{
		ext = ext.ToLower();
		foreach (VirtIOEntry resource in storyManager.Current.VFS.Enumerate())
		{
			if (resource.Name.ToLower().EndsWith(ext))
			{
				string id = resource.Name.Substring(0, resource.Name.Length - ext.Length);
				if (pathAsId)
				{
					id = Path.Combine(resource.Path, resource.Name);
				}
				Func<object> provider = delegate
				{
					byte[] array = resource.Accessors.Last().Read();
					return asString ? ((object)Encoding.UTF8.GetString(array)) : ((object)array);
				};
				yield return (id, provider);
			}
		}
	}

	public PythonDictionary get_player_camera_vision(bool parts = true, bool tex = false)
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected O, but got Unknown
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected O, but got Unknown
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Expected O, but got Unknown
		MaleCameraUserController ctl = ((Component)(object)Session.Player.Character).GetComponentInChildren<MaleCameraUserController>();
		Camera camera = ((Component)(object)ctl).GetComponent<CameraRendingTextureTakeAPhoto>().Camara;
		FemaleSkins skins = Session.Guest.Impl.skins as FemaleSkins;
		IRopaManager ropaManager = ((Component)(object)Session.Guest.Impl).GetComponentInChildren<IRopaManager>();
		FemaleSkins.HitSkins.Partes hitskins = skins.hitSkins.partes;
		CameraVision vision = new CameraVision(camera, ropaManager, hitskins);
		Dictionary<HumanBodyPartsEng, StimulusTrackingService.StimulusFlags> tmp = vision.Capture();
		PythonDictionary result = new PythonDictionary();
		foreach (KeyValuePair<HumanBodyPartsEng, StimulusTrackingService.StimulusFlags> x in tmp)
		{
			PythonList flags = new PythonList();
			result[(object)x.Key.ToString()] = flags;
			foreach (StimulusTrackingService.StimulusFlags f in Enum.GetValues(typeof(StimulusTrackingService.StimulusFlags)))
			{
				if (f != StimulusTrackingService.StimulusFlags.none && x.Value.HasFlag(f))
				{
					flags.Add((object)f.ToString());
				}
			}
		}
		PythonDictionary output = new PythonDictionary();
		output[(object)"parts"] = result;
		if (tex)
		{
			Texture2D tex2d = Traverse.Create((object)ctl).Field<Texture2D>("m_lastTaken").Value;
			if (tex2d != null)
			{
				output[(object)"tex"] = tex2d;
			}
		}
		return output;
	}

	public float get_required_consent(string strStimulus, string strReceiver, string strSender, bool ask = false)
	{
		object type = Enum.Parse(typeof(StimulusType), strStimulus);
		object receiver = Enum.Parse(typeof(HumanBodyPartsEng), strReceiver);
		object sender = Enum.Parse(typeof(SenderBodyPartEng), strSender);
		TipoDeEstimulo tde = TipoDeEstimulo.None;
		DireccionDeEstimulo dde = DireccionDeEstimulo.recibida;
		ParteDelCuerpoHumano parte = (ParteDelCuerpoHumano)receiver;
		ParteQuePuedeEstimular sendr = (ParteQuePuedeEstimular)sender;
		object obj = type;
		object obj2 = obj;
		if (obj2 is StimulusType)
		{
			EmocionesFemeninasValues values;
			switch ((StimulusType)obj2)
			{
			case StimulusType.touch:
				tde = TipoDeEstimulo.tactil;
				goto IL_00b5;
			case StimulusType.gaze:
				tde = TipoDeEstimulo.visual;
				goto IL_00b5;
			case StimulusType.observe:
				tde = TipoDeEstimulo.visual;
				dde = DireccionDeEstimulo.dada;
				goto IL_00b5;
			case StimulusType.penetration:
				tde = TipoDeEstimulo.coital;
				goto IL_00b5;
			case StimulusType.photo:
				tde = TipoDeEstimulo.visual;
				goto IL_00b5;
			case StimulusType.expose:
				{
					tde = ((!ask) ? TipoDeEstimulo.desvestidura : TipoDeEstimulo.peticionDesvestidura);
					goto IL_00b5;
				}
				IL_00b5:
				values = default(EmocionesFemeninasValues);
				// ConsentNecesario.ParaConJerarquia signature changed in SMA 23.1 — call via reflection
				try
				{
					var _cnMethod = AccessTools.Method(typeof(ConsentNecesario), "ParaConJerarquia");
					if (_cnMethod != null)
					{
						var _cnParams = _cnMethod.GetParameters();
						object[] _cnArgs = new object[_cnParams.Length];
						for (int _pi = 0; _pi < _cnParams.Length; _pi++)
						{
							var _pt = _cnParams[_pi].ParameterType.IsByRef ? _cnParams[_pi].ParameterType.GetElementType() : _cnParams[_pi].ParameterType;
							if (_pt == typeof(TipoDeEstimulo)) _cnArgs[_pi] = tde;
							else if (_pt == typeof(DireccionDeEstimulo)) _cnArgs[_pi] = dde;
							else if (_pt == typeof(ParteDelCuerpoHumano)) _cnArgs[_pi] = parte;
							else if (_pt == typeof(ParteQuePuedeEstimular)) _cnArgs[_pi] = sendr;
							else if (_pt == typeof(EmocionesFemeninasValues)) _cnArgs[_pi] = values;
							else if (_pt == typeof(Personalidad)) _cnArgs[_pi] = personalidad;
							else _cnArgs[_pi] = _pt.IsValueType ? Activator.CreateInstance(_pt) : null;
						}
						return (float)(_cnMethod.Invoke(null, _cnArgs) ?? 0f);
					}
				}
				catch (Exception _ex) { Logger.Global.Error(_ex, "ConsentNecesario.ParaConJerarquia reflection failed"); }
				return 0f;
			}
		}
		return 0f;
	}

	public void camera_look_at(Vector3 pos, float speed = 10f)
	{
		CurrentMainChar.ICamera cam = Singleton<CurrentMainChar>.instance.camara;
		if (cam is MainCharCamera mcc)
		{
			float t = mcc.lookAtTargetConfig.lookAtSpeed;
			mcc.lookAtTargetConfig.lookAtSpeed = speed;
			cam.Ver(pos);
			mcc.lookAtTargetConfig.lookAtSpeed = t;
		}
		else
		{
			cam.Ver(pos);
		}
	}

	public void report_crash(string text)
	{
		CrashWindow wnd = Scope.Lookup<PyStoryRuntimeService>().CrashWindow;
		if (!UIBuilder.IsVisible((VisualElement)wnd))
		{
			wnd.SetError(text);
			wnd.SetWindowVisible(v: true);
		}
	}

	public MayBeAdapter<string> ask_select_outfit()
	{
		return new MayBeAdapter<string>(Session.Modal.SelectOutfitFromGallery());
	}
}
