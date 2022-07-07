using MelonLoader;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;

namespace SpeedrunTools.Replay
{
  class SerializeGameState
  {
    private static SerializeGameState s_instance;
    public static SerializeGameState Instance
    {
      get
      {
        if (s_instance == null) s_instance = new SerializeGameState();
        return s_instance;
      }
    }

    private int _currentSceneIdx;
    private Camera _cam;

    private Transform GetPlayerTransform()
    {
      if (_cam == null)
      {
        var rig = Object.FindObjectOfType<StressLevelZero.Rig.RigManager>();
        _cam = rig.ControllerRig.GetComponentInChildren<Camera>();
      }
      return _cam.transform;
    }

    private Transform GetHeadsetTransform()
    {
      // TODO
      return _cam.transform;
    }

    private Transform GetControllerTransform(string name)
    {
      // TODO
      return _cam.transform;
    }

    public void OnSceneChange(int sceneIdx)
    {
      _currentSceneIdx = sceneIdx;
      _cam = null;
    }

    public byte[] BuildFrame()
    {
      var builder = new FlatBuffers.FlatBufferBuilder(1024);
      {
        var transform = GetHeadsetTransform();
        Bwr.Headset.StartHeadset(builder);
        var pos = transform.position;
        Bwr.Headset.AddPosition(builder, Bwr.Vector3.CreateVector3(builder, pos.x, pos.y, pos.z));
        var rot = transform.rotation.eulerAngles;
        Bwr.Headset.AddRotationEuler(builder, Bwr.Vector3.CreateVector3(builder, rot.x, rot.y, rot.z));
      }
      var headset = Bwr.Headset.EndHeadset(builder);

      {
        var transform = GetControllerTransform("left");
        Bwr.Controller.StartController(builder);
        var pos = transform.position;
        Bwr.Controller.AddPosition(builder, Bwr.Vector3.CreateVector3(builder, pos.x, pos.y, pos.z));
        var rot = transform.rotation.eulerAngles;
        Bwr.Controller.AddRotationEuler(builder, Bwr.Vector3.CreateVector3(builder, rot.x, rot.y, rot.z));
      }
      var controllerLeft = Bwr.Controller.EndController(builder);

      {
        var transform = GetControllerTransform("right");
        Bwr.Controller.StartController(builder);
        var pos = transform.position;
        Bwr.Controller.AddPosition(builder, Bwr.Vector3.CreateVector3(builder, pos.x, pos.y, pos.z));
        var rot = transform.rotation.eulerAngles;
        Bwr.Controller.AddRotationEuler(builder, Bwr.Vector3.CreateVector3(builder, rot.x, rot.y, rot.z));
      }
      var controllerRight = Bwr.Controller.EndController(builder);

      {
        var transform = GetPlayerTransform();
        Bwr.Player.StartPlayer(builder);
        var pos = transform.position;
        Bwr.Player.AddPosition(builder, Bwr.Vector3.CreateVector3(builder, pos.x, pos.y, pos.z));
        Bwr.Player.AddRotation(builder, transform.rotation.eulerAngles.y);
        Bwr.Player.AddHeadset(builder, headset);
        Bwr.Player.AddControllerLeft(builder, controllerLeft);
        Bwr.Player.AddControllerRight(builder, controllerRight);
      }
      var player = Bwr.Player.EndPlayer(builder);

      var frame = Bwr.Frame.CreateFrame(builder, Time.time, player);
      builder.Finish(frame.Value);
      //Utils.LogDebug($"Recorded frame: {currentSceneIdx} {cam.position.ToString()}");
      return builder.SizedByteArray();
    }
  }
}
