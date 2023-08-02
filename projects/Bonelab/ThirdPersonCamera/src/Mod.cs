using MelonLoader;
using HarmonyLib;
using UnityEngine;
using SLZ.Bonelab;
using SLZ.SaveData;

namespace Sst.ThirdPersonCamera {
public class Mod : MelonMod {
  private const float DEG2RAD = 0.017453292519943295f;

  private static int CAMERA_COLLISION_LAYER_MASK =
      LayerMask.GetMask(new string[] {
        "Default", "Static",
        "NoSelfCollide", // has things like doors but also some npc body parts
      });

  private SmoothFollower _thirdPersonCamera;
  private float _cameraRadius;
  private MelonPreferences_Entry<float> _prefFollowDistance;
  private MelonPreferences_Entry<float> _prefSmoothTime;
  private MelonPreferences_Entry<float> _prefCameraRatio;

  public override void OnInitializeMelon() {
    Dbg.Init(BuildInfo.NAME);

    var category = MelonPreferences.CreateCategory(BuildInfo.NAME);
    _prefFollowDistance = category.CreateEntry(
        "follow_distance", 2f, "Distance the camera is behind the player");
    _prefSmoothTime = category.CreateEntry(
        "smooth_time", 10f,
        "Time it takes for the camera to catch up to player movements");
    _prefCameraRatio = category.CreateEntry(
        "camera_ratio", 0.5f,
        "Amount to split screen between first and third person cameras (0 = first person only, 1 = third person only)");

    Utilities.LevelHooks.OnLevelStart += level => CreateThirdPersonCamera();
    Utilities.LevelHooks.OnLoad += level => ResetState();
    _prefCameraRatio.OnEntryValueChanged.Subscribe((a, b) => SetCameraRatios());
  }

  public override void OnUpdate() {
    if (!_thirdPersonCamera)
      return;

    var rigScreen =
        Utilities.LevelHooks.RigManager?.uiRig.controlPlayer.rigScreen ??
        GetRigScreen();
    var target = rigScreen.TargetTransform;
    if (target) {
      _thirdPersonCamera.targetTransform.rotation = target.rotation;
      _thirdPersonCamera.targetTransform.position =
          target.position +
          target.rotation * Vector3.back * _prefFollowDistance.Value;

      if (Physics.SphereCast(
              target.position, _cameraRadius,
              _thirdPersonCamera.targetTransform.position - target.position,
              out var hit,
              Vector3.Distance(target.position,
                               _thirdPersonCamera.targetTransform.position),
              CAMERA_COLLISION_LAYER_MASK)) {
        _thirdPersonCamera.targetTransform.position =
            hit.point + hit.normal * _cameraRadius;
      }
    }

    _thirdPersonCamera.MoveCameraUpdate();
  }

  private void SetCameraRatios() {
    var cam = GetRigScreen().cam;
    if (cam)
      cam.rect = new Rect(0f, 0f, _prefCameraRatio.Value, 1f);
    if (_thirdPersonCamera) {
      var camera = _thirdPersonCamera.GetComponent<Camera>();
      camera.rect =
          new Rect(_prefCameraRatio.Value, 0f, 1f - _prefCameraRatio.Value, 1f);

      var halfHeight =
          Mathf.Tan(0.5f * camera.fieldOfView * DEG2RAD) * camera.nearClipPlane;
      var halfWidth = halfHeight * camera.aspect;
      _cameraRadius = Mathf.Max(halfHeight, halfWidth);
    }
  }

  private void ResetState() { _thirdPersonCamera = null; }

  private void CreateThirdPersonCamera() {
    if (Utilities.LevelHooks.IsLoading || _thirdPersonCamera)
      return;

    Dbg.Log("CreateThirdPersonCamera");
    var spectatorCamera = GetRigScreen().cam.gameObject;
    var thirdPersonCamera = GameObject.Instantiate(spectatorCamera);
    thirdPersonCamera.name = "Third Person Camera";
    thirdPersonCamera.transform.parent = spectatorCamera.transform.parent;

    _thirdPersonCamera = thirdPersonCamera.GetComponent<SmoothFollower>();
    _thirdPersonCamera.TranslationSmoothTime = 10f;
    _thirdPersonCamera.RotationalSmoothTime = 10f;

    var thirdPersonTarget = new GameObject("Third Person Target").transform;
    thirdPersonTarget.SetParent(_thirdPersonCamera.targetTransform.parent,
                                false);
    _thirdPersonCamera.targetTransform = thirdPersonTarget;

    spectatorCamera.GetComponent<Camera>().rect =
        new Rect(0f, 0f, _prefCameraRatio.Value, 1f);
    thirdPersonCamera.GetComponent<Camera>().rect =
        new Rect(_prefCameraRatio.Value, 0f, 1f - _prefCameraRatio.Value, 1f);

    SetCameraRatios();

#if DEBUG // TAS
    var spectatorFollower = spectatorCamera.GetComponent<SmoothFollower>();
    spectatorFollower.RotationalSmoothTime *= 4f;
    spectatorFollower.TranslationSmoothTime *= 2f;
#endif
  }

  private RigScreenOptions GetRigScreen() =>
      Utilities.LevelHooks.RigManager.GetComponent<RigScreenOptions>();
}
}
