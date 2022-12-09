#r "../../../../references/Bonelab/Assembly-CSharp.dll"
#r "../../../../references/Bonelab/UnityEngine.CoreModule.dll"

UnityEngine.Events.UnityEvent evt;
evt.AddListener(new Action(() => { Log("myevent"); }));
