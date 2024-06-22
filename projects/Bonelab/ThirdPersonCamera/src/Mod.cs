using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using Sst.Utilities;

#if ML6
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.VRMK;
using Il2CppSLZ.Rig;
#else
using SLZ.Bonelab;
using SLZ.VRMK;
using SLZ.Rig;
#endif

namespace Sst.ThirdPersonCamera;

public class Mod : MelonMod {
  private const float DEG2RAD = 0.017453292519943295f;

  private static int CAMERA_COLLISION_LAYER_MASK =
      LayerMask.GetMask(new string[] {
        "Default", "Static",
        "NoSelfCollide", // has things like doors but also some npc body parts
      });

  public static Mod Instance;

  private SmoothFollower _thirdPersonCamera;
  private float _cameraRadius;
  private Vector3 _followVec;
  private MelonPreferences_Entry<float> _prefFollowDistance;
  private MelonPreferences_Entry<float> _prefSmoothTime;
  private MelonPreferences_Entry<float> _prefCameraRatio;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);
    Instance = this;

    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefFollowDistance = category.CreateEntry(
        "follow_distance", 2f, "Distance the camera is behind the player");
    _prefSmoothTime = category.CreateEntry(
        "smooth_time", 10f,
        "Time it takes for the camera to catch up to player movements");
    _prefCameraRatio = category.CreateEntry(
        "camera_ratio", 1f,
        "Amount to split screen between first and third person cameras (0 = first person only, 1 = third person only)");

    LevelHooks.OnLevelStart += level => Create();
    LevelHooks.OnLoad += level => ResetState();
    _prefCameraRatio.OnEntryValueChanged.Subscribe((a, b) => SetCameraRatios());
    _prefFollowDistance.OnEntryValueChanged.Subscribe((a, b) => SetFollowVec());
    SetFollowVec();
  }

  public override void OnUpdate() {
#if DEBUG
    if (LevelHooks.RigManager?.ControllerRig.rightController
            .GetThumbStickDown() ??
        false) {
      Toggle();
    }
#endif

    if (!_thirdPersonCamera)
      return;

    var rigScreen = UIRig.Instance?.controlPlayer.rigScreen ?? GetRigScreen();
    if (rigScreen?.TargetTransform) {
      var tpos = rigScreen.TargetTransform.position;
      var trot = rigScreen.TargetTransform.rotation;
      var targetTransformPos = tpos + trot * _followVec;

      if (Physics.SphereCast(tpos, _cameraRadius, targetTransformPos - tpos,
                             out var hit,
                             Vector3.Distance(tpos, targetTransformPos),
                             CAMERA_COLLISION_LAYER_MASK)) {
        targetTransformPos = hit.point + hit.normal * _cameraRadius;
      }

      _thirdPersonCamera.targetTransform.SetPositionAndRotation(
          targetTransformPos, trot);
    }

    _thirdPersonCamera.MoveCameraUpdate();
  }

  public void Toggle() {
    if (_thirdPersonCamera) {
      Destroy();
    } else {
      Create();
    }
  }

  public void Destroy() {
    if (!_thirdPersonCamera)
      return;

    Dbg.Log("Destroy");
    GameObject.Destroy(_thirdPersonCamera.gameObject);
    ResetState();
    SetCameraRatios();
    GetSpectatorCamera().SetActive(true);
  }

  public void Create() {
    if (LevelHooks.IsLoading || _thirdPersonCamera)
      return;

    Dbg.Log("Create");
    var spectatorCamera = GetSpectatorCamera();
    var thirdPersonCamera = GameObject.Instantiate(spectatorCamera);
    thirdPersonCamera.name = "Third Person Camera";
    thirdPersonCamera.transform.parent = spectatorCamera.transform.parent;

    _thirdPersonCamera = thirdPersonCamera.GetComponent<SmoothFollower>();
    _thirdPersonCamera.TranslationSmoothTime = _prefSmoothTime.Value;
    _thirdPersonCamera.RotationalSmoothTime = _prefSmoothTime.Value;

    var thirdPersonTarget = new GameObject("Third Person Target").transform;
    thirdPersonTarget.SetParent(_thirdPersonCamera.targetTransform.parent,
                                false);
    _thirdPersonCamera.targetTransform = thirdPersonTarget;

    spectatorCamera.GetComponent<Camera>().rect =
        new Rect(0f, 0f, _prefCameraRatio.Value, 1f);
    thirdPersonCamera.GetComponent<Camera>().rect =
        new Rect(_prefCameraRatio.Value, 0f, 1f - _prefCameraRatio.Value, 1f);

    SetCameraRatios();
    spectatorCamera.SetActive(false);

    GetRigContainer().GetComponent<PlayerAvatarArt>()?.EnableHair();

#if DEBUG
    // TAS
    // var spectatorFollower =
    // spectatorCamera.GetComponent<SmoothFollower>();
    // spectatorFollower.RotationalSmoothTime *= 4f;
    // spectatorFollower.TranslationSmoothTime *= 2f;

    // Screen.SetResolution(1920 * 2, 1080, false);
#endif
  }

  private void ResetState() { _thirdPersonCamera = null; }

  private void SetCameraRatios() {
    var cam = GetRigScreen().cam;
    if (cam)
      cam.rect = new Rect(
          0f, 0f, 1f - (_thirdPersonCamera ? _prefCameraRatio.Value : 0f), 1f);
    if (_thirdPersonCamera) {
      var camera = _thirdPersonCamera.GetComponent<Camera>();
      camera.rect =
          new Rect(1f - _prefCameraRatio.Value, 0f, _prefCameraRatio.Value, 1f);

      var halfHeight =
          Mathf.Tan(0.5f * camera.fieldOfView * DEG2RAD) * camera.nearClipPlane;
      var halfWidth = halfHeight * camera.aspect;
      _cameraRadius = Mathf.Max(halfHeight, halfWidth);
    }
  }

  private Component GetRigContainer() {
#if PATCH4
    return LevelHooks.RigManager.ControllerRig;
#else
    return LevelHooks.RigManager;
#endif
  }

  private RigScreenOptions
  GetRigScreen() => GetRigContainer().GetComponent<RigScreenOptions>();

  private GameObject GetSpectatorCamera() => GetRigScreen().cam.gameObject;

  private void SetFollowVec() {
    _followVec = Vector3.back * _prefFollowDistance.Value;
  }

  [HarmonyPatch(typeof(PlayerAvatarArt), nameof(PlayerAvatarArt.DisableHair))]
  class PlayerAvatarArt_DisableHair_Patch {
    [HarmonyPrefix()]
    internal static bool Prefix() {
      Dbg.Log("PlayerAvatarArt_DisableHair_Patch");
      return !Instance._thirdPersonCamera;
    }
  }
}
