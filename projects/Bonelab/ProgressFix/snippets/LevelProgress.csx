#r "../../../../references/Bonelab/Assembly-CSharp.dll"

var progression = SLZ.SaveData.DataManager.ActiveSave.Progression;
bool completed;
SLZ.Bonelab.BonelabProgressionHelper
    .TryGetLevelCompleted(progression, "DistrictParkour", out completed);
// completed;
