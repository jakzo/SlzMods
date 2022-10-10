using UnityEngine;
using Valve.VR;

namespace Sst.Speedruns {
class Overlay {
  private const string LOADING_TEXT_NAME = "SpeedrunTools_LoadingText";
  private static readonly string OVERLAY_KEY =
      $"{SteamVR_Overlay.key}.SpeedrunTools_RunStatus";

  private GameObject s_loadingText;
  private ulong s_overlayHandle;

  public void Show(string text) {
    if (OpenVR.Overlay == null)
      return;

    var texture = TextToTexture(text);

    if (s_overlayHandle == OpenVR.k_ulOverlayHandleInvalid) {
      var createError = OpenVR.Overlay.CreateOverlay(
          OVERLAY_KEY, "SpeedrunTools Run Status", ref s_overlayHandle);
      if (createError != EVROverlayError.None) {
        var findError =
            OpenVR.Overlay.FindOverlay(OVERLAY_KEY, ref s_overlayHandle);
        if (findError != EVROverlayError.None)
          throw new System.Exception(
              $"Could not find speedrun overlay: {findError}");
      }
    }
    var showError = OpenVR.Overlay.ShowOverlay(s_overlayHandle);
    if (showError == EVROverlayError.InvalidHandle ||
        showError == EVROverlayError.UnknownOverlay) {
      var findError =
          OpenVR.Overlay.FindOverlay(OVERLAY_KEY, ref s_overlayHandle);
      if (findError != EVROverlayError.None)
        throw new System.Exception(
            $"Could not find speedrun overlay: {findError}");
    }
    var ovrTexture = new Texture_t() {
      handle = texture.GetNativeTexturePtr(),
      eType = SteamVR.instance.textureType,
      eColorSpace = EColorSpace.Auto,
    };
    OpenVR.Overlay.SetOverlayTexture(s_overlayHandle, ref ovrTexture);
    OpenVR.Overlay.SetOverlayAlpha(s_overlayHandle, 1);
    OpenVR.Overlay.SetOverlayWidthInMeters(s_overlayHandle, 0.6f);
    OpenVR.Overlay.SetOverlayAutoCurveDistanceRangeInMeters(s_overlayHandle, 1,
                                                            2);
    var vecMouseScale = new HmdVector2_t() {
      v0 = 1,
      v1 = 1,
    };
    var uvOffset = new Vector4(0, 0, 1, 1);
    var textureBounds = new VRTextureBounds_t() {
      uMin = (0 + uvOffset.x) * uvOffset.z,
      vMin = (1 + uvOffset.y) * uvOffset.w,
      uMax = (1 + uvOffset.x) * uvOffset.z,
      vMax = (0 + uvOffset.y) * uvOffset.w,
    };
    OpenVR.Overlay.SetOverlayTextureBounds(s_overlayHandle, ref textureBounds);
    OpenVR.Overlay.SetOverlayMouseScale(s_overlayHandle, ref vecMouseScale);
    OpenVR.Overlay.SetOverlayInputMethod(s_overlayHandle,
                                         VROverlayInputMethod.None);
    var transform = new SteamVR_Utils.RigidTransform() {
      pos = new Vector3(-0.1f, -0.2f, 1),
      rot = Quaternion.identity,
      scl = new Vector3(1, 1, 1),
    };
    var ovrMatrix = transform.ToHmdMatrix34();
    OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(
        s_overlayHandle, OpenVR.k_unTrackedDeviceIndex_Hmd, ref ovrMatrix);
    OpenVR.Overlay.SetHighQualityOverlay(s_overlayHandle);
    OpenVR.Overlay.SetOverlayFlag(s_overlayHandle, VROverlayFlags.Curved,
                                  false);
    OpenVR.Overlay.SetOverlayFlag(s_overlayHandle, VROverlayFlags.RGSS4X, true);
  }

  public void Hide() {
    if (OpenVR.Overlay == null ||
        s_overlayHandle == OpenVR.k_ulOverlayHandleInvalid)
      return;
    OpenVR.Overlay.HideOverlay(s_overlayHandle);
  }

  private Texture TextToTexture(string text) {
    TMPro.TextMeshPro tmp;
    if (s_loadingText == null) {
      s_loadingText = new GameObject("SpeedrunTools_LoadingText");
      // TODO: Is there a way to have this render correctly while not making it
      // appear in the scene?
      s_loadingText.transform.position = new Vector3(100000, 100000, 100000);
      Object.DontDestroyOnLoad(s_loadingText);
      tmp = s_loadingText.AddComponent<TMPro.TextMeshPro>();
      tmp.material = new Material(Shader.Find("TextMeshPro/Distance Field"));
      tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
      tmp.fontSize = 2;
      tmp.rectTransform.sizeDelta = new Vector2(3, 3);
    } else {
      tmp = s_loadingText.GetComponent<TMPro.TextMeshPro>();
    }

    // Create the mesh of our text
    tmp.text = text;
    tmp.ForceMeshUpdate();

    // Save existing GL/RenderTexture state
    GL.PushMatrix();
    var prevActiveRt = RenderTexture.active;

    // Point the projection matrix at our text
    GL.LoadIdentity();
    GL.LoadProjectionMatrix(
        Matrix4x4.Ortho(-tmp.rectTransform.sizeDelta.x / 2.0f,
                        tmp.rectTransform.sizeDelta.x / 2.0f,
                        -tmp.rectTransform.sizeDelta.x / 2.0f,
                        tmp.rectTransform.sizeDelta.y / 2.0f, -10, 100));

    // Render the viewed text to our texture
    var texture =
        RenderTexture.GetTemporary(2048, 2048, 24, RenderTextureFormat.ARGB32);
    Graphics.SetRenderTarget(texture);
    GL.Clear(false, true, Color.clear);
    tmp.renderer.material.SetPass(0);
    Graphics.DrawMeshNow(tmp.mesh, Camera.main.cameraToWorldMatrix);

    // Restore state
    GL.PopMatrix();
    RenderTexture.active = prevActiveRt;

    return texture;
  }
}
}
