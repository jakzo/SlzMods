using MelonLoader;

namespace SpeedrunTools
{
  class PluginResetSave : Plugin
  {
    private static int s_currentSceneIdx;

    public readonly Hotkey HotkeyReset = new Hotkey()
    {
      Predicate = (cl, cr) =>
        s_currentSceneIdx == Utils.SCENE_MENU_IDX &&
        cl.GetAButton() && cl.GetBButton() &&
        cr.GetAButton() && cr.GetBButton(),
      Handler = () =>
      {
        MelonLogger.Msg("Resetting save");
        var dataManager = UnityEngine.Object.FindObjectOfType<Data_Manager>();
        var oldData = dataManager.data_player;
        dataManager.DATA_DEFAULT_ALL();

        // Copy previous preferences
        var newData = dataManager.data_player;
        newData.additionalLighting = oldData.additionalLighting;
        newData.aliasing = oldData.aliasing;
        newData.ambientOcclusion = oldData.ambientOcclusion;
        newData.audio_GlobalVolume = oldData.audio_GlobalVolume;
        newData.audio_Music = oldData.audio_Music;
        newData.audio_SFX = oldData.audio_SFX;
        newData.beltRightSide = oldData.beltRightSide;
        newData.bloom = oldData.bloom;
        newData.fisheye = oldData.fisheye;
        newData.fisheyeLocation = oldData.fisheyeLocation;
        newData.isAdaptiveOn = oldData.isAdaptiveOn;
        newData.isInverted = oldData.isInverted;
        newData.isRightHanded = oldData.isRightHanded;
        newData.joySensitivityNew = oldData.joySensitivityNew;
        newData.language = oldData.language;
        newData.loco_Curve = oldData.loco_Curve;
        newData.loco_DegreesPerSnap = oldData.loco_DegreesPerSnap;
        newData.loco_Direction = oldData.loco_Direction;
        newData.loco_SnapDegreesPerFrame = oldData.loco_SnapDegreesPerFrame;
        newData.mod_Haptic = oldData.mod_Haptic;
        newData.motionBlur = oldData.motionBlur;
        newData.mouseSensitivityNew = oldData.mouseSensitivityNew;
        newData.offset_Floor = oldData.offset_Floor;
        newData.offset_Sitting = oldData.offset_Sitting;
        newData.physicsUpdateRate = oldData.physicsUpdateRate;
        newData.player_Height = oldData.player_Height;
        newData.player_Name = oldData.player_Name;
        newData.playIntroCheck = oldData.playIntroCheck;
        newData.playMode = oldData.playMode; // safe?
        newData.profile_Name = oldData.profile_Name;
        newData.quality = oldData.quality;
        newData.resX = oldData.resX;
        newData.resY = oldData.resY;
        newData.shadowQuality = oldData.shadowQuality;
        newData.SnapEnabled = oldData.SnapEnabled;
        newData.standing = oldData.standing;
        newData.subtitlesOn = oldData.subtitlesOn;
        newData.TextureResolution = oldData.TextureResolution;
        newData.VirtualCrouching = oldData.VirtualCrouching;

        dataManager.RELOADSCENE();
      }
    };

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
      s_currentSceneIdx = buildIndex;
    }
  }
}
