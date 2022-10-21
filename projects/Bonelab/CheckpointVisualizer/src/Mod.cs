using System.Linq;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Bonelab;
using SLZ.Marrow.Warehouse;
using SLZ.Marrow.SceneStreaming;

namespace Sst {
public class Mod : MelonMod {
  private static Color COLOR_GREEN = new Color(0.2f, 0.8f, 0.2f, 0.25f);
  private static Color COLOR_RED = new Color(0.8f, 0.2f, 0.2f, 0.25f);

  public static Mod Instance;
  public LevelCrate NextLevel;

  private GameControl_KartRace _gameControl;
  private TriggerLasers[] _checkpointTriggers;
  private UnityEngine.Collider[] _checkpointColliders;
  private GameObject[] _renderedColliders;
  private LoadingScene _activeLoadingScene;
  private Shader _shader;
  private float _setupAfter = 0;

  public Mod() { Instance = this; }

  public override void OnInitializeMelon() { Dbg.Init(BuildInfo.NAME); }

  // ---
  public override void OnSceneWasInitialized(int buildindex, string sceneName) {
    if (!sceneName.ToUpper().Contains("BOOTSTRAP"))
      return;
    AssetWarehouse.OnReady(new System.Action(() => {
      var crate = AssetWarehouse.Instance.GetCrates().ToArray().First(
          c => c.Title == Utilities.Levels.TITLE_MONOGON_MOTORWAY);
      var bootstrapper =
          GameObject.FindObjectOfType<SceneBootstrapper_Bonelab>();
      var crateRef = new LevelCrateReference(crate.Barcode.ID);
      bootstrapper.VoidG114CrateRef = crateRef;
      bootstrapper.MenuHollowCrateRef = crateRef;
    }));
  }
  // ---

  public override void OnUpdate() {
    if (_activeLoadingScene != null &&
        !_activeLoadingScene.gameObject.scene.isLoaded) {
      _activeLoadingScene = null;
      if (SceneStreamer._session._level.Crate.Title ==
          Utilities.Levels.TITLE_MONOGON_MOTORWAY) {
        _setupAfter = Time.time + 2;
        Dbg.Log($"setup scheduled for {_setupAfter}");
      }
    }

    if (_setupAfter != 0 && _setupAfter < Time.time) {
      _setupAfter = 0;
      OnLevelStart();
    }
  }

  public void OnLevelStart() {
    Dbg.Log("OnLevelStart");
    // TODO: How do we get transparency to work using the color alpha?
    _shader = Utilities.Unity.FindShader("SLZ/Highlighter");
    _gameControl = GameObject.FindObjectOfType<GameControl_KartRace>();
    _checkpointTriggers =
        _gameControl.trackCheckPoint
            .Select((_, i) => GameObject.Find($"trigger_{(char)(65 + i)}")
                                  .GetComponent<TriggerLasers>())
            .ToArray();
    _checkpointColliders =
        _checkpointTriggers
            .Select(trigger => trigger.gameObject.GetComponent<BoxCollider>())
            .ToArray();
    _renderedColliders =
        _checkpointColliders.Select(collider => RenderTrigger(collider, false))
            .ToArray();

    foreach (var (trigger, i) in _checkpointTriggers.Select((t, i) => (t, i))) {
      trigger.OnTriggerEnterEvent.AddListener(
          new System.Action<UnityEngine.Collider>((collider) =>
                                                      RerenderTrigger(i)));
    }

    foreach (var name in new[] { "trigger_newLap", "trigger_start" }) {
      var trigger =
          Utilities.Unity
              .FindDescendantTransform(_gameControl.gameObject.transform, name)
              .gameObject;
      trigger.GetComponent<TriggerLasers>().OnTriggerEnterEvent.AddListener(
          new System.Action<UnityEngine.Collider>(collider =>
                                                      RerenderAllTriggers()));
      RenderTrigger(trigger.GetComponent<BoxCollider>(), false);
    }
    Dbg.Log("Rendered triggers");
  }

  private void RerenderAllTriggers() {
    Dbg.Log("RerenderAllTriggers");
    for (var i = 0; i < _checkpointTriggers.Length; i++)
      RerenderTrigger(i);
  }

  private void RerenderTrigger(int i) {
    Dbg.Log($"RerenderTrigger: {i}");
    if (_renderedColliders[i] != null)
      GameObject.Destroy(_renderedColliders[i]);
    _renderedColliders[i] =
        RenderTrigger(_checkpointColliders[i], _gameControl.trackCheckPoint[i]);
  }

  private GameObject RenderTrigger(Collider collider, bool isTriggered) =>
      Utilities.Collider.Visualize(collider.gameObject, collider,
                                   isTriggered? COLOR_GREEN: COLOR_RED,
                                   _shader);

  [HarmonyPatch(typeof(LoadingScene), nameof(LoadingScene.Start))]
  class LoadingScene_Start_Patch {
    [HarmonyPrefix()]
    internal static void Prefix(LoadingScene __instance) {
      Dbg.Log("LoadingScene_Start_Patch");
      Instance._activeLoadingScene = __instance;
    }
  }
}
}

/*

Shader list:
Hidden/Internal-MotionVectors
Sprites/Default
UI/Default
Hidden/Universal Render Pipeline/Blit Alpha Premultiply
Hidden/BlitCopy
Hidden/BlitCopyWithDepth
Hidden/Internal-GUITextureClip
Hidden/Internal-GUITexture
Hidden/Internal-GUIRoundedRect
Hidden/Internal-GUIRoundedRectWithColorPerBorder
Sprites/Mask
Hidden/InternalErrorShader
Hidden/Internal-Loading
Hidden/Internal-Loading
GUI/Text Shader
Standard
Legacy Shaders/VertexLit
Hidden/Universal Render Pipeline/Blit
Hidden/Universal Render Pipeline/CopyDepth
Hidden/Universal Render Pipeline/LutBuilderHdr
Hidden/Universal Render Pipeline/LutBuilderLdr
Hidden/Universal Render Pipeline/Sampling
Hidden/Universal Render Pipeline/ScreenSpaceShadows
Hidden/Universal Render Pipeline/UberPost
Hidden/Universal Render Pipeline/HBAO
Hidden/Universal Render Pipeline/Debug/DebugReplacement
Hidden/Universal Render Pipeline/StencilDeferred
Hidden/Universal Render Pipeline/FallbackError
Hidden/Universal Render Pipeline/MaterialError
Hidden/Universal/CoreBlit
Hidden/Universal/CoreBlitColorAndDepth
Hidden/kMotion/CameraMotionVectors
Hidden/kMotion/ObjectMotionVectors
Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion
Hidden/Universal Render Pipeline/Stop NaN
Hidden/Universal Render Pipeline/SubpixelMorphologicalAntialiasing
Hidden/Universal Render Pipeline/GaussianDepthOfField
Hidden/Universal Render Pipeline/BokehDepthOfField
Hidden/Universal Render Pipeline/CameraMotionBlur
Hidden/Universal Render Pipeline/PaniniProjection
Hidden/Universal Render Pipeline/Bloom
Hidden/Universal Render Pipeline/LensFlareDataDriven
Hidden/Universal Render Pipeline/Scaling Setup
Hidden/Universal Render Pipeline/Edge Adaptive Spatial Upsampling
Hidden/Universal Render Pipeline/FinalPost
Hidden/Universal Render Pipeline/XR/XROcclusionMesh
Hidden/Universal Render Pipeline/XR/XRMirrorView
Hidden/MK/Glow/MKGlowSM40
SLZ/Icon Billboard
Hidden/MK/Glow/MKGlowSM35
TextMeshPro/Sprite
Hidden/MK/Glow/MKGlowSM25
Hidden/MK/Glow/MKGlowSM20
Hidden/MK/Glow/SelectiveRender
TextMeshPro/Mobile/Distance Field
Hidden/MK/Glow/MKGlowSM30
Hidden/Universal Render Pipeline/CopyDepthToColor
Universal Render Pipeline/Unlit
Universal Render Pipeline/Lit (PBR Workflow)
Hidden/Universal Render Pipeline/FallbackError
TextMeshPro/Distance Field
TextMeshPro/Mobile/Distance Field
Sprites/Default
DefaultUI
UI/Default Font
UI/Unlit/Text
UI/Default
UI/Unlit/Transparent
SLZ/LitMAS/LitMAS Opaque
Lux URP/Human/Skin
Custom/HighlightAlt
SLZ/Fuzz Shell
SLZ/Additive HDR
SLZ/Highlighter
Universal Render Pipeline/Particles/Lit (Particles)
SLZ/AdditiveWithDistortion
SLZ/LitMAS/LitMAS Triplanar
SLZ/Gacha
Lux URP/Human/Hair Blend
SLZ/Fuzz Sheen
SLZ/LitMAS/LitMAS Vertex Color AO
SLZ/LitMAS/LitMAS Posespace
Universal Render Pipeline/Particles/Unlit (Particles)
SLZ/Hologram Depth fade
Unlit/URP Unlit Color
SLZ/TextureRampEmissive
Shadergraph Fixed/EyeAdvanced_LOD0_URP
SLZ/Mod2x
Universal Render Pipeline/Particles/Simple Lit
SLZ/ParticleStar
SLZ/SpiderChart
SLZ/Simple Geo Skybox
SLZ/OpaqueMotion
StressLevelZero/Lines/Colored Blended
SLZ/Fuzz Sheen Terrain First-Pass
Lux URP/Human/Hair
Baked Shadergraphs/TMP_SDF-URP Lit
Standard
Legacy Shaders/VertexLit
SLZ/ConstelationLines
Hidden/TerrainEngine/Details/UniversalPipeline/BillboardWavingDoublePass
Hidden/TerrainEngine/CameraFacingBillboardTree
SLZ/Holographic Wall Simple
Hidden/TerrainEngine/Details/UniversalPipeline/Vertexlit
Hidden/kMotion/ObjectMotionVectors
Hidden/Universal Render Pipeline/Blit
Hidden/Universal Render Pipeline/XR/XRMirrorView
Hidden/Universal Render Pipeline/XR/XROcclusionMesh
Hidden/Universal Render Pipeline/FinalPost
Hidden/Universal Render Pipeline/GaussianDepthOfField
Hidden/Universal Render Pipeline/BokehDepthOfField
Hidden/Universal Render Pipeline/LutBuilderHdr
Hidden/Universal Render Pipeline/Scaling Setup
Hidden/Universal Render Pipeline/LutBuilderLdr
Hidden/Universal Render Pipeline/Stop NaN
Hidden/Universal Render Pipeline/PaniniProjection
Hidden/Universal Render Pipeline/UberPost
Hidden/Universal Render Pipeline/Bloom
Hidden/Universal Render Pipeline/LensFlareDataDriven
Hidden/Universal Render Pipeline/CameraMotionBlur
Hidden/Universal Render Pipeline/Edge Adaptive Spatial Upsampling
Hidden/Universal Render Pipeline/SubpixelMorphologicalAntialiasing
Hidden/Universal Render Pipeline/ScreenSpaceShadows
Hidden/Universal Render Pipeline/StencilDeferred
Hidden/Universal Render Pipeline/CopyDepthToColor
Hidden/Universal Render Pipeline/Debug/DebugReplacement
Hidden/Universal Render Pipeline/Sampling
Hidden/Universal/CoreBlitColorAndDepth
Hidden/kMotion/CameraMotionVectors
Hidden/Universal Render Pipeline/CopyDepth
Hidden/Universal Render Pipeline/MaterialError
Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion
Hidden/TerrainEngine/Details/UniversalPipeline/WavingDoublePass
Hidden/Universal/CoreBlit
Hidden/Universal Render Pipeline/HBAO
Hidden/Shader Graph/FallbackError
Hidden/Shader Graph/FallbackError
SLZ/Vignette
Hidden/Nature/Terrain/Utilities
Hidden/TerrainEngine/HeightBlitCopy
Hidden/TerrainEngine/PaintHeight
Hidden/TerrainEngine/CrossBlendNeighbors
Hidden/Nature/Tree Creator Albedo Rendertex
Hidden/Nature/Tree Creator Normal Rendertex
Hidden/Nature/Tree Soft Occlusion Bark Rendertex
Hidden/TerrainEngine/GenerateNormalmap
Hidden/TerrainEngine/Splatmap/Standard-BaseGen
Hidden/TerrainEngine/TerrainLayerUtils
Hidden/Nature/Tree Soft Occlusion Leaves Rendertex
Skybox/Cubemap
Hidden/TerrainEngine/BillboardTree
SLZ/Particle/Motion Vector Billboard Correct Shadows
SLZ/Lit MAS Transparent
SLZ/Anime

*/
