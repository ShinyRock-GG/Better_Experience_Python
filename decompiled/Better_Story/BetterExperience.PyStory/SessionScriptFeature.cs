using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Assets._ReusableScripts;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.AI;
using Assets._ReusableScripts.CuchiCuchi.Controllers.Ojos.Parpadeos;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Ropa.Interacciones;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using Assets._ReusableScripts.CuchiCuchi.Skins;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.Wrappers.Pools;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.PyStory.AI;
using BetterExperience.PyStory.Scripting;
using BetterExperience.PyStory.UI;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Characters;
using IronPython.Hosting;
using IronPython.Runtime;
using Microsoft.Scripting.Hosting;
using UnityEngine;

namespace BetterExperience.PyStory;

// ─────────────────────────────────────────────────────────────────────────────
// SessionScriptFeature — v6 original + v5.2a pycs compatibility bridge
// ─────────────────────────────────────────────────────────────────────────────

public class SessionScriptFeature : SessionService
{
    // =========================================================================
    // v6 ORIGINAL CODE (unchanged)
    // =========================================================================

    private class ScriptHost : StrandService, IDisposable
    {
        private GameSession session;

        private ScriptEngine python;

        private PythonScriptRepository scripts;

        private StoryManager storyManager;

        public ScriptPluginFeature.Plugin plugin { get; }

        private ScopeSupport scope => session.Scope;

        public ScriptHost(ScriptPluginFeature.Plugin plugin, DispatcherService dispatcher,
                          GameSession session, StoryManager storyManager)
            : base(dispatcher)
        {
            logger.Prefix = "[pymod-" + plugin.package.Id + "]";
            this.session = session;
            this.storyManager = storyManager;
            this.plugin = plugin;
            scripts = new PythonScriptRepository();
            scripts.Init(plugin.virtIO);
            Start();
        }

        private void Start()
        {
            base.scriptingScope.Start();
            python = Python.CreateEngine();
            PythonList metapath = Python.GetSysModule(python).GetVariable<PythonList>("meta_path");
            metapath.append((object)new RepositoryMetaImporter(scripts));
            ExposeRuntime();
            ImportAllModules();
        }

        public void Dispose()
        {
            base.scriptingScope.Dispose();
            if (python != null)
            {
                python.Runtime.Shutdown();
                python = null;
            }
        }

        private void ImportAllModules()
        {
            logger.Info("Importing all python modules...");
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int imported = 0;
            foreach (string scriptfile in scripts.AutoimportScripts)
            {
                string name = scriptfile;
                if (name.ToLowerInvariant().EndsWith(".py"))
                {
                    name = name.Substring(0, name.Length - 3);
                }
                if (name.ToLowerInvariant().EndsWith("__init__"))
                {
                    name = name.Substring(0, name.Length - 8);
                }
                if (name.EndsWith("\\"))
                {
                    name = name.Substring(0, name.Length - 1);
                }
                string module = name.Replace("\\", ".");
                logger.Debug("Module {0} as {1}", name, module);
                try
                {
                    python.Execute<object>("import " + module);
                    imported++;
                }
                catch (Exception ex)
                {
                    logger.Error("Module import failed {0}: {1}", module, ex.Message);
                }
            }
            stopwatch.Stop();
            logger.Info("Loaded {0} modules in {1}ms.", imported, stopwatch.ElapsedMilliseconds);
        }

        private void ExposeRuntime()
        {
            ScriptScope builtin = Python.GetBuiltinModule(python);

            // v6 lean API — for new scripts
            Pyrt pyrt = new Pyrt(logger, scope, this, session);
            builtin.SetVariable("pyrt", (object)pyrt);

            // v5.2a compatibility — for existing pycs scripts using __pycsrt
            SimpleAi simpleAi = storyManager?.Current?.Scope?.Lookup<SimpleAi>();
            var compat = new PycsrtCompat(logger, this, session, storyManager, plugin, simpleAi);
            builtin.SetVariable("__pycsrt", (object)compat);
        }
    }

    // ── v6 Pyrt (lean runtime for new scripts) ──────────────────────────────

    public class Pyrt
    {
        private StrandService scriptingContext;

        public Logger logger { get; }

        public ScopeSupport scope { get; }

        public ScopeSupport scriptScope => scriptingContext.scriptingScope;

        public GameSession session { get; private set; }

        public GameSession Session => session;

        public ScopeSupport Scope => scope;

        public ScopeSupport ScriptScope => scriptScope;

        public Pyrt(Logger logger, ScopeSupport scope, StrandService strandService, GameSession session)
        {
            this.logger = logger;
            this.scope = scope;
            scriptingContext = strandService;
            this.session = session;
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
            return new PyStrand(scriptingContext.scriptingScope);
        }

        public void call_with_exception_trap(Func<object> x)
        {
            try
            {
                x();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Crash");
            }
        }
    }

    // ── SessionScriptFeature wiring ──────────────────────────────────────────

    private ScriptPluginFeature feature;

    private List<ScriptHost> hosts = new List<ScriptHost>();

    public SessionScriptFeature(ScriptPluginFeature feature)
    {
        this.feature = feature;
    }

    public override void OnStart()
    {
        base.OnStart();
        DispatcherService dispatcher = Lookup<DispatcherService>();
        foreach (ScriptPluginFeature.Plugin p in feature.plugins)
        {
            StartPlugin(dispatcher, p);
        }
    }

    private void StartPlugin(DispatcherService dispatcher, ScriptPluginFeature.Plugin p)
    {
        logger.Info("Starting {0}", p.package.Id);
        hosts.Add(new ScriptHost(p, dispatcher, base.Session, feature.storyManager));
        ScriptPluginFeature.Plugin plugin = p;
        plugin.restart.Add(delegate
        {
            List<ScriptHost> list = hosts.Where((ScriptHost x) => x.plugin == plugin).ToList();
            if (list.Count != 0)
            {
                int index = hosts.IndexOf(list[0]);
                hosts[index].Dispose();
                hosts[index] = new ScriptHost(plugin, dispatcher, base.Session, feature.storyManager);
            }
        }, base.Scope);
    }

    // =========================================================================
    // v5.2a COMPATIBILITY BRIDGE
    // Exposes __pycsrt to Python so existing pycs scripts work unchanged.
    // =========================================================================

    /// <summary>
    /// The __pycsrt module exposed to Python.
    /// Usage in pycs/core.py:  import __pycsrt as _pyrt
    ///                          pyrt = _pyrt.api
    ///                          _pyrt.ai.Anger ...
    /// </summary>
    public class PycsrtCompat
    {
        public PyStoryRuntimeCompat api { get; }
        public SimpleAi ai { get; }

        public PycsrtCompat(Logger logger, StrandService scriptingContext,
                            GameSession session, StoryManager storyManager,
                            ScriptPluginFeature.Plugin plugin, SimpleAi simpleAi)
        {
            api = new PyStoryRuntimeCompat(logger, scriptingContext, session, storyManager, plugin);
            ai  = simpleAi;
        }
    }

    // ── PyStoryRuntimeCompat ─────────────────────────────────────────────────
    // Port of v5.2a PyStoryRuntime adapted to v6 session/scope architecture.
    // Method bodies match the actual decompiled v5.2a source.

    public class PyStoryRuntimeCompat
    {
        private readonly Logger logger;
        private readonly StrandService scriptingContext;
        private readonly ScopeSupport scriptingScope;
        private readonly GameSession session;
        private readonly StoryManager storyManager;
        private readonly ScriptPluginFeature.Plugin plugin;

        // Services resolved lazily when scene/interview scope is ready
        private POIManager poiManager;
        private InteractionManager interactionManager;
        private PoseManager poseManager;
        private DialogueManager dialogueManager;
        private OverlayService overlayService;
        private GuestHeadController.LookAt scriptedLookAtTarget;

        // Dialogue state
        private bool pronounce_complete = false;
        private bool continue_clicked;
        private int last_response_key = -1;

        public GameSession Session => session;

        public ScopeSupport Scope => scriptingScope;

        public PyStoryRuntimeCompat(Logger logger, StrandService scriptingContext,
                                    GameSession session, StoryManager storyManager,
                                    ScriptPluginFeature.Plugin plugin)
        {
            this.logger = logger;
            this.scriptingContext = scriptingContext;
            this.scriptingScope = scriptingContext.scriptingScope;
            this.session = session;
            this.storyManager = storyManager;
            this.plugin = plugin;

            if (storyManager?.Current != null)
            {
                Story story = storyManager.Current;
                story.SceneScopeCreated.Add(InitSceneScope, scriptingScope);
                if (story.SceneScope != null)
                    InitSceneScope();

                story.InterviewScopeCreated.Add(InitInterviewScope, scriptingScope);
                if (story.SceneInterviewScope != null)
                    InitInterviewScope();
            }
        }

        private void InitSceneScope()
        {
            Story story = storyManager.Current;
            poiManager      = story.SceneScope.Lookup<POIManager>();
            poseManager     = story.SceneScope.Lookup<PoseManager>();
            dialogueManager = story.SceneScope.Lookup<DialogueManager>();
            overlayService  = story.SceneScope.Lookup<OverlayService>();

            dialogueManager.OnRespond.Add(delegate(string key)
            {
                last_response_key = int.Parse(key);
            }, scriptingScope);
            dialogueManager.OnPronounceComplete.Add(delegate
            {
                pronounce_complete = true;
            }, scriptingScope);
            dialogueManager.OnContinue.Add(delegate
            {
                continue_clicked = true;
            });
        }

        private void InitInterviewScope()
        {
            Story story = storyManager.Current;
            interactionManager   = story.SceneInterviewScope.Lookup<InteractionManager>();
            scriptedLookAtTarget = session.Guest.HeadController.CreateLookAt(scriptingScope);
            scriptingContext.StrandEpilogueGens.Add(StrandEpilogue);
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

        // ── Strand delegates ─────────────────────────────────────────────────

        public PyStrand get_main_strand() => scriptingContext.MainStrand;
        public bool can_invoke_immediate(PyStrand strand) => strand.Frames.Count == 0;
        public bool invoke_next(IEnumerable e, PyStrand strand) { scriptingContext.SpawnNext(e.GetEnumerator(), strand); return true; }
        public bool invoke_last(IEnumerable e, PyStrand strand) { scriptingContext.SpawnLast(e.GetEnumerator(), strand); return true; }
        public PyStrand new_strand() => new PyStrand(scriptingScope);
        public void call_with_exception_trap(Func<object> x) { try { x(); } catch (Exception ex) { logger.Error(ex, "Crash"); } }

        // ── Dialogue ─────────────────────────────────────────────────────────

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
                        Key   = i.ToString()
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

        public int get_last_response() => last_response_key;

        // ── Overlay ──────────────────────────────────────────────────────────

        public void send_notification(string text, float duration, float fadeOut)
        {
            overlayService?.InfoMessage(text, duration, fadeOut);
        }

        // ── Guest movement ───────────────────────────────────────────────────

        public bool is_guest_busy() => interactionManager.HasActiveInteraction;

        public object get_guest_interaction_context()
        {
            return interactionManager.CreateQueryContext();
        }

        public object guest_goto_poi(string poiId, int orientation = 2)
        {
            PointOfInterest poi = poiManager.FindPOI(poiId);
            if (poi == null)
            {
                logger.Error("Poi {0} does not exist", poiId);
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
            return new WaitUntil(() => !interactionManager.HasActiveInteraction
                && interactionManager.CurrentPlace != null
                && interactionManager.CurrentPlace.POI == poi);
        }

        public object guest_teleport_poi(string poiId, int orientation = 2)
        {
            PointOfInterest poi = poiManager.FindPOI(poiId);
            if (poi == null)
            {
                logger.Error("Poi {0} does not exist", poiId);
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
            return new WaitUntil(() => !interactionManager.HasActiveInteraction
                && interactionManager.CurrentPlace != null
                && interactionManager.CurrentPlace.POI == poi);
        }

        public object apply_posture(string postureId)
        {
            string poiId = interactionManager.CurrentPlace.POI.Id;
            if (!poseManager.POIPostures.TryGetValue(poiId, out var poses))
            {
                logger.Error("(1) Posture {0} does not exist at poi {1}", postureId, poiId);
                return null;
            }
            if (!poses.ExactPostures.TryGetValue(postureId, out var posture)
                && !poses.ExactPostures.TryGetValue(poiId + "." + postureId, out posture))
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
            PoseOrientation finalOrientation = (!poseManager.StandingPosture.Is(posture))
                ? PoseOrientation.UNIVERSAL
                : PoseOrientation.FRONT;
            Interaction interaction = interactionManager.CreatePostureChangeInteraction(
                interactionManager.CreateQueryContext(ignoreOrientation: true, finalOrientation), posture);
            if (interaction == null)
            {
                logger.Error("Posture change interaction was not created. See logs above for details");
                return null;
            }
            interactionManager.StartInteraction(interaction);
            return new WaitUntil(() => !interactionManager.HasActiveInteraction);
        }

        public object play_clip(object name_or_clip, float blendingTime = -1f,
                                List<AnimatorLayer> layers = null,
                                AnimationCompletionMode completionMode = AnimationCompletionMode.Default,
                                string label = null)
        {
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
                        IAnimationClipState additiveClip = interactionManager.AnimationController
                            .GetPlayingClipByLayer(AnimatorLayer.Face);
                        if (additiveClip != null)
                        {
                            additiveClip.FadeOut();
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
                clips = new List<PoseAnimationClip> { pac };
            }
            Interaction interaction = interactionManager.CreatePlayClipInteraction(
                ctx, clips[0].Name, clips, blendingTime, layers, completionMode, label);
            if (interactionManager.StartInteraction(interaction))
            {
                return new WaitUntil(() => !interactionManager.HasActiveInteraction);
            }
            logger.Error("Unable to start animation. See logs above for details.");
            return null;
        }

        // ── Interaction enumerate/execute ────────────────────────────────────

        public IList<Interaction> enumerate_transitions(bool clip_change = true, bool posture_change = true)
            => interactionManager.EnumerateTransitions(clip_change, posture_change).ToArray();

        public IList<Interaction> enumerate_gotos()
            => interactionManager.EnumerateGotos().ToArray();

        public object execute_interaction(Interaction i)
        {
            if (interactionManager.StartInteraction(i))
            {
                return new WaitUntil(() => !interactionManager.HasActiveInteraction);
            }
            logger.Error("Failed to start interaction {0}", i.DisplayName);
            return null;
        }

        // ── Eye expression ───────────────────────────────────────────────────

        public void set_eye_expression(EyeExpression expression, float duration)
        {
            OjosExpresionController ctl = session.Guest.EyesExpressionComponent;
            ctl.Cambiar((OjosExpresionController.Tipo)expression, int.MaxValue, duration,
                        ControllerPrioridadConfig.prioridad);
        }

        // ── Look-at ──────────────────────────────────────────────────────────

        public void set_look_at_target(Transform target, float duration)
        {
            if (scriptedLookAtTarget == null) return;
            scriptedLookAtTarget.Enabled = target != null;
            if (target != null)
            {
                scriptedLookAtTarget.Transform = target;
            }
        }

        // ── Clothes ──────────────────────────────────────────────────────────

        public List<SimpleCloth> enumerate_clothes(bool ignoreHidden = true)
        {
            List<SimpleCloth> result = new List<SimpleCloth>();
            IRopaManager manager = ((UnityEngine.Component)(object)session.Guest.Impl)
                                   .GetComponentInChildren<IRopaManager>();
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
            IRopaManager manager = ((UnityEngine.Component)(object)session.Guest.Impl)
                                   .GetComponentInChildren<IRopaManager>();
            manager.OcultarPieza(id, ocultar: true, null);
        }

        // ── Interactive objects ───────────────────────────────────────────────

        public InteractiveObject make_interactive(GameObject obj)
        {
            InteractiveObject existing = obj.GetComponentInChildren<InteractiveObject>(includeInactive: true);
            if ((bool)existing)
            {
                existing.ScriptingScope = scriptingScope;
                return existing;
            }
            Transform it = UnityUtils.NewTransform("InteractiveTarget", obj.transform, scriptingScope);
            it.localPosition = Vector3.zero;
            it.localRotation = Quaternion.identity;
            it.localScale    = Vector3.one;
            InteractiveObject io = it.gameObject.AddComponent<InteractiveObject>();
            io.ScriptingScope = scriptingScope;
            return io;
        }

        public InteractiveObject install_character_interaction()
        {
            Transform activator = ((UnityEngine.Component)(object)session.Guest.Impl)
                                  .transform.FindDeepChild("DonaActivator");
            if (activator == null)
                throw new Exception("Unable to find activator");
            activator.gameObject.SetActive(value: false);
            scriptingScope.OnDispose += delegate
            {
                if ((bool)activator && (bool)activator.gameObject)
                    activator.gameObject.SetActive(value: true);
            };
            Transform head = session.Guest.Puppet.PuppetMaster.transform.FindDeepChild("CC_Base_Head");
            if (head == null)
                throw new Exception("Unable to find head");
            InteractiveObject existing = head.GetComponentInChildren<InteractiveObject>();
            if (existing != null)
            {
                existing.ScriptingScope = scriptingScope;
                return existing;
            }
            Transform t = UnityUtils.NewTransform("A", head, scriptingScope);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            InteractiveObject io = t.gameObject.AddComponent<InteractiveObject>();
            io.ScriptingScope = scriptingScope;
            SphereCollider sphere = io.GetComponent<SphereCollider>();
            if (sphere != null)
            {
                sphere.radius *= 1.5f;
            }
            return io;
        }

        // ── GameObject search ─────────────────────────────────────────────────

        public object find_go_by_name(GameObject root, params string[] name)
        {
            for (int i = 0; i < name.Length; i++)
            {
                if (root == null)
                    root = GameObject.Find(name[i]);
                else
                {
                    Transform t = root.transform.FindDeepChild(name[i]);
                    root = (t != null) ? t.gameObject : null;
                }
                if (root == null) break;
            }
            return root;
        }

        // ── Gene loading ─────────────────────────────────────────────────────

        public void gio_apply_genes_from_package(string location)
        {
            if (storyManager?.Current == null)
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
                    Dictionary<string, Dictionary<string, float>> data =
                        GlobalPersistenceService.Deserialize<Dictionary<string, Dictionary<string, float>>>(
                            Encoding.UTF8.GetString(blob));
                    data.Values.ForEach(delegate(Dictionary<string, float> x)
                    {
                        x.ForEach(delegate(KeyValuePair<string, float> kv)
                        {
                            update.Add(new GeneInfo
                            {
                                Id    = new GeneId(kv.Key),
                                Value = kv.Value
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
                session.Guest.GuestInstance.UpdateAll((IList<GeneInfo>)update);
            }
        }
    }
}
