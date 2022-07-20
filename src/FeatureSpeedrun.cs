using MelonLoader;
using UnityEngine;
using Valve.VR;
using HarmonyLib;
using System.Linq;
using System.Collections.Generic;

namespace SpeedrunTools
{
  class FeatureSpeedrun : Feature
  {
    private static HashSet<string> ALLOWED_MODS = new HashSet<string>();
    private static HashSet<string> ALLOWED_PLUGINS = new HashSet<string>()
    {
      "Backwards Compatibility Plugin",
    };
    private const string MENU_TEXT_NAME = "SpeedrunTools_MenuText";
    private const string LOADING_TEXT_NAME = "SpeedrunTools_LoadingText";
    private const string RGB_GREEN = "22ee11";
    private const string RGB_RED = "dd1111";
    private static bool SHOW_BONEWORKS_LOGO_OVERLAY = true;
    private static readonly string OVERLAY_KEY = $"{SteamVR_Overlay.key}.SpeedrunTools_RunStatus";

    private static int s_currentSceneIdx;
    private static ulong s_overlayHandle;
    private static float? s_relativeStartTime;
    private static float? s_loadingStartTime;
    private static Texture2D s_overlayTexture;

    private enum RunIllegitimacyReason
    {
      DISALLOWED_MODS,
      DISALLOWED_PLUGINS,
    }

    private static Dictionary<RunIllegitimacyReason, string> ComputeRunLegitimacy()
    {
      var illegitimacyReasons = new Dictionary<RunIllegitimacyReason, string>();

      var disallowedMods = MelonHandler.Mods.Where(mod => !(mod is SpeedrunTools) && !ALLOWED_MODS.Contains(mod.Info.Name));
      if (disallowedMods.Count() > 0)
      {
        var disallowedModNames = disallowedMods.Select(mod => mod.Info.Name);
        illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_MODS] =
          $"Disallowed mods are active: {string.Join(", ", disallowedModNames)}";
      }

      var disallowedPlugins = MelonHandler.Plugins.Where(plugin => !ALLOWED_PLUGINS.Contains(plugin.Info.Name));
      if (disallowedPlugins.Count() > 0)
      {
        var disallowedPluginNames = disallowedPlugins.Select(mod => mod.Info.Name);
        illegitimacyReasons[RunIllegitimacyReason.DISALLOWED_PLUGINS] =
          $"Disallowed plugins are active: {string.Join(", ", disallowedPluginNames)}";
      }

      return illegitimacyReasons;
    }

    private static string ColorText(string text, string color) =>
      $"<color=#{color}>{text}</color>";

    private static void UpdateMainMenuText(string customText)
    {
      var text = customText ?? GetMenuText();

      var menuText = GameObject.Find(MENU_TEXT_NAME);

      if (menuText == null)
      {
        menuText = new GameObject(MENU_TEXT_NAME);
        var tmp = menuText.AddComponent<TMPro.TextMeshPro>();
        tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
        tmp.fontSize = 1.6f;
        tmp.rectTransform.sizeDelta = new Vector2(2, 2);
        tmp.rectTransform.position = new Vector3(2.65f, 1.8f, 9.6f);
      }

      menuText.GetComponent<TMPro.TextMeshPro>().SetText(text);
    }

    private static string GetMenuText() =>
      $@"{(SpeedrunTools.s_isLegitRunActive ? ColorText("Speedrun mode enabled", RGB_GREEN) : ColorText("Speedrun mode disabled", RGB_RED))}
» You are{(SpeedrunTools.s_isLegitRunActive ? "" : " not")} allowed to submit runs to leaderboard
» Practice features are {(SpeedrunTools.s_isLegitRunActive ? "disabled" : "enabled")}";

    public FeatureSpeedrun()
    {
      isAllowedInLegitRuns = true;
    }

    public readonly Hotkey HotkeyToggle = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_currentSceneIdx == Utils.SCENE_MENU_IDX && (
          cl.GetBButton() && cr.GetBButton() ||
          Utils.GetKeyControl() && Input.GetKey(KeyCode.S)
        ),
      Handler = () =>
      {
        if (SpeedrunTools.s_isLegitRunActive)
        {
          SpeedrunTools.s_isLegitRunActive = false;
          UpdateMainMenuText(null);
          MelonLogger.Msg("Speedrun mode disabled");
        } else
        {
          var illegitimacyReasons = ComputeRunLegitimacy();
          if (illegitimacyReasons.Count == 0)
          {
            SpeedrunTools.s_isLegitRunActive = true;
            UpdateMainMenuText(null);
            MelonLogger.Msg("Speedrun mode enabled");
          } else
          {
            var reasonMessages = string.Join("", illegitimacyReasons.Select(reason => $"\n» {reason.Value}"));
            UpdateMainMenuText($"{ColorText("Could not enable speedrun mode", RGB_RED)} because:{reasonMessages}");
            MelonLogger.Msg($"Could not enable speedrun mode because:{reasonMessages}");
          }
        }
      }
    };

    // TODO: Make this work
    private static Texture TextToTexture(string text)
    {
      if (SHOW_BONEWORKS_LOGO_OVERLAY)
      {
        if (s_overlayTexture == null)
        {
          s_overlayTexture = Resources
            .FindObjectsOfTypeAll<Texture2D>()
            .Where(tex => tex.name == "sprite_title_boneworks")
            .First();
        }
        return s_overlayTexture;
      }

      // Build our text as a renderable mesh
      var textGameObject = new GameObject("SpeedrunTools_LoadingText")
      {
        // hideFlags = HideFlags.HideAndDontSave,
      };
      var tmp = textGameObject.AddComponent<TMPro.TextMeshPro>();
      tmp.material = new Material(Shader.Find("TextMeshPro/Distance Field"));
      tmp.alignment = TMPro.TextAlignmentOptions.TopLeft;
      tmp.fontSize = 1.8f;
      tmp.rectTransform.sizeDelta = new Vector2(2, 2);
      tmp.text = text;

      // Save existing GL/RenderTexture state
      GL.PushMatrix();
      var prevActiveRt = RenderTexture.active;

      // Point the projection matrix at our text
      GL.LoadIdentity();
      GL.LoadProjectionMatrix(Matrix4x4.Ortho(
        -tmp.rectTransform.sizeDelta.x / 2.0f,
        tmp.rectTransform.sizeDelta.x / 2.0f,
        -tmp.rectTransform.sizeDelta.x / 2.0f,
        tmp.rectTransform.sizeDelta.y / 2.0f,
        -10,
        100
      ));

      // Render the viewed text to our texture
      var texture = RenderTexture.GetTemporary(2048, 2048, 24, RenderTextureFormat.ARGB32);
      Graphics.SetRenderTarget(texture);
      GL.Clear(false, true, Color.clear);
      tmp.renderer.material.SetPass(0);
      Graphics.DrawMeshNow(tmp.mesh, Matrix4x4.identity);

      // Restore state
      GL.PopMatrix();
      RenderTexture.active = prevActiveRt;
      // GameObject.Destroy(textGameObject);

      // TESTING: Put the texture on the carpet in front of the speedrun text in main menu to see if it worked
      var carpet = GameObject.Find("carpet_2");
      if (carpet)
      {
        var renderer = carpet.GetComponent<MeshRenderer>();
        renderer.material.mainTexture = texture;
      } else
      {
        Utils.LogDebug("Carpet not found");
      }

      return texture;
    }

    private static void ShowOverlay(string text, bool colorGreen)
    {
      var texture = TextToTexture(text);

      if (OpenVR.Overlay == null) return;

      if (s_overlayHandle == OpenVR.k_ulOverlayHandleInvalid)
      {
        var createError = OpenVR.Overlay.CreateOverlay(OVERLAY_KEY, "SpeedrunTools Run Status", ref s_overlayHandle);
        if (createError != EVROverlayError.None)
        {
          var findError = OpenVR.Overlay.FindOverlay(OVERLAY_KEY, ref s_overlayHandle);
          if (findError != EVROverlayError.None)
            throw new System.Exception($"Could not find speedrun overlay: {findError}");
        }
      }
      var showError = OpenVR.Overlay.ShowOverlay(s_overlayHandle);
      if (showError == EVROverlayError.InvalidHandle || showError == EVROverlayError.UnknownOverlay)
      {
        var findError = OpenVR.Overlay.FindOverlay(OVERLAY_KEY, ref s_overlayHandle);
        if (findError != EVROverlayError.None)
          throw new System.Exception($"Could not find speedrun overlay: {findError}");
      }
      var ovrTexture = new Texture_t()
      {
        handle = texture.GetNativeTexturePtr(),
        eType = SteamVR.instance.textureType,
        eColorSpace = EColorSpace.Auto,
      };
      OpenVR.Overlay.SetOverlayTexture(s_overlayHandle, ref ovrTexture);
      OpenVR.Overlay.SetOverlayColor(
        s_overlayHandle,
        colorGreen ? 0.2f : 0.8f,
        colorGreen ? 0.8f : 0.1f,
        0.1f
      );
      OpenVR.Overlay.SetOverlayAlpha(s_overlayHandle, 1);
      OpenVR.Overlay.SetOverlayWidthInMeters(s_overlayHandle, 0.6f);
      OpenVR.Overlay.SetOverlayAutoCurveDistanceRangeInMeters(s_overlayHandle, 1, 2);
      var vecMouseScale = new HmdVector2_t()
      {
        v0 = 1,
        v1 = 1,
      };
      var uvOffset = new Vector4(0, 0, 1, 1);
      var textureBounds = new VRTextureBounds_t()
      {
        uMin = (0 + uvOffset.x) * uvOffset.z,
        vMin = (1 + uvOffset.y) * uvOffset.w,
        uMax = (1 + uvOffset.x) * uvOffset.z,
        vMax = (0 + uvOffset.y) * uvOffset.w,
      };
      OpenVR.Overlay.SetOverlayTextureBounds(s_overlayHandle, ref textureBounds);
      OpenVR.Overlay.SetOverlayMouseScale(s_overlayHandle, ref vecMouseScale);
      OpenVR.Overlay.SetOverlayInputMethod(s_overlayHandle, VROverlayInputMethod.None);
      var transform = new SteamVR_Utils.RigidTransform()
      {
        pos = new Vector3(0, -0.4f, 1),
        rot = Quaternion.identity,
        scl = new Vector3(1, 1, 1),
      };
      var ovrMatrix = transform.ToHmdMatrix34();
      OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(
        s_overlayHandle,
        OpenVR.k_unTrackedDeviceIndex_Hmd,
        ref ovrMatrix
      );
      OpenVR.Overlay.SetHighQualityOverlay(s_overlayHandle);
      OpenVR.Overlay.SetOverlayFlag(s_overlayHandle, VROverlayFlags.Curved, false);
      OpenVR.Overlay.SetOverlayFlag(s_overlayHandle, VROverlayFlags.RGSS4X, true);
    }

    private static void HideOverlay()
    {
      if (OpenVR.Overlay == null || s_overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;
      OpenVR.Overlay.HideOverlay(s_overlayHandle);
    }

    private static void OnLoadingStart()
    {
      s_loadingStartTime = Time.time;
      var startTime = s_relativeStartTime.HasValue ? s_relativeStartTime.Value : Time.time;
      var duration = System.TimeSpan.FromSeconds(Time.time - startTime);
      var modeText = SpeedrunTools.s_isLegitRunActive
        ? ColorText("enabled", RGB_GREEN)
        : ColorText("disabled", RGB_RED);
      var text = $@"Speedrun mode {modeText}
v{BuildInfo.Version}
{duration:h\:mm\:ss\.ff}";
      ShowOverlay(text, SpeedrunTools.s_isLegitRunActive);
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      if (s_loadingStartTime.HasValue && s_relativeStartTime.HasValue)
      {
        s_relativeStartTime += Time.time - s_loadingStartTime.Value;
        s_loadingStartTime = null;
      }

      var previousSceneIdx = s_currentSceneIdx;
      s_currentSceneIdx = buildIndex;

      if (previousSceneIdx == Utils.SCENE_MENU_IDX)
        s_relativeStartTime = Time.time;

      if (s_currentSceneIdx == Utils.SCENE_MENU_IDX)
        UpdateMainMenuText(null);
    }

    [HarmonyPatch(typeof(LoadingScreenPackage), nameof(LoadingScreenPackage.StartAlpha))]
    class LoadingScreenPackage_StartAlpha_Patch
    {
      [HarmonyPrefix()]
      internal static void Prefix()
      {
        OnLoadingStart();
      }
    }

    [HarmonyPatch(typeof(LoadingScreenPackage), nameof(LoadingScreenPackage.AlphaOverlays))]
    class LoadingScreenPackage_AlphaOverlays_Patch
    {
      [HarmonyPrefix()]
      internal static void Prefix()
      {
        HideOverlay();
      }
    }
  }
}
