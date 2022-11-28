#r "../../references/Boneworks/Assembly-CSharp.dll"

var dm = UnityEngine.Object.FindObjectOfType<Data_Manager>();
dm.data_player.offset_Sitting = 100000;
dm.DATA_SAVE();
StressLevelZero.Utilities.BoneworksSceneManager.ReloadScene();
