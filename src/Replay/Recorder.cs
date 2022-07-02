using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SpeedrunTools.Replay
{
  class Recorder
  {
    public bool IsRecording = false;

    private float _minFrameDuration;
    private int _maxFrameLength;
    private int _numFrames = 0;
    private System.DateTime _startTime;
    private FlatBuffers.FlatBufferBuilder _builder;
    private List<FlatBuffers.Offset<Bwr.Level>> _levels = new List<FlatBuffers.Offset<Bwr.Level>>();
    private List<FlatBuffers.Offset<Bwr.Frame>> _frames = new List<FlatBuffers.Offset<Bwr.Frame>>();
    private float? _loadStartTime;
    private float _relativeStartTime;
    private float _lastFrameTime;
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

    public void Start(float minFrameDuration, float maxDuration)
    {
      _minFrameDuration = minFrameDuration;
      _maxFrameLength = (int)(maxDuration / _minFrameDuration);
      _startTime = System.DateTime.Now;
      _loadStartTime = Time.time;
      _relativeStartTime = Time.time;
      IsRecording = true;

      _builder = new FlatBuffers.FlatBufferBuilder(1024);
    }

    public void Stop()
    {
      IsRecording = false;
      var duration = _lastFrameTime - _relativeStartTime;
      var file = Bwr.File.CreateFile(
        _builder,
        Bwr.Metadata.CreateMetadata(_builder, _startTime.ToBinary(), duration),
        Bwr.File.CreateLevelsVector(_builder, _levels.ToArray())
      );
      _builder.Finish(file.Value);
    }

    public byte[] GetReplayBytes()
    {
      if (_builder == null || IsRecording)
        throw new System.Exception("Replay cannot be saved until recording is finished");
      return _builder.SizedByteArray();
    }

    public Replay GetReplay()
    {
      if (_builder == null || IsRecording)
        throw new System.Exception("Replay cannot be saved until recording is finished");
      return new Replay(Bwr.File.GetRootAsFile(_builder.DataBuffer));
    }

    public void SaveToFile(string filePath)
    {
      System.IO.File.WriteAllBytes(filePath, GetReplayBytes());
    }

    public void OnUpdate(int currentSceneIdx)
    {
      if (!IsRecording) return;
      if (_numFrames >= _maxFrameLength)
      {
        Utils.LogDebug("Max recording length reached");
        IsRecording = false;
        return;
      }

      // Stop recording during loading screens
      if (SceneLoader.loading)
      {
        if (!_loadStartTime.HasValue)
        {
          var frames = Bwr.Level.CreateFramesVector(_builder, _frames.ToArray());
          _levels.Add(Bwr.Level.CreateLevel(_builder, _levelStartTime, currentSceneIdx, frames));

          _loadStartTime = Time.time;
          _cam = null; // seems like camera is replaced on scene change
        }
        return;
      }
      if (_loadStartTime.HasValue)
      {
        _levelStartTime = Time.time;
        _loadStartTime = null;
      }

      // Only record frame if time since last frame is greater than MinFrameDuration
      if (_frames.Count > 0 && _lastFrameTime + _minFrameDuration > Time.time) return;

      // Record frame
      {
        var transform = GetHeadsetTransform();
        Bwr.Headset.StartHeadset(_builder);
        var pos = transform.position;
        Bwr.Headset.AddPosition(_builder, Bwr.Vector3.CreateVector3(_builder, pos.x, pos.y, pos.z));
        var rot = transform.rotation.eulerAngles;
        Bwr.Headset.AddRotationEuler(_builder, Bwr.Vector3.CreateVector3(_builder, rot.x, rot.y, rot.z));
      }
      var headset = Bwr.Headset.EndHeadset(_builder);

      {
        var transform = GetControllerTransform("left");
        Bwr.Controller.StartController(_builder);
        var pos = transform.position;
        Bwr.Controller.AddPosition(_builder, Bwr.Vector3.CreateVector3(_builder, pos.x, pos.y, pos.z));
        var rot = transform.rotation.eulerAngles;
        Bwr.Controller.AddRotationEuler(_builder, Bwr.Vector3.CreateVector3(_builder, rot.x, rot.y, rot.z));
      }
      var controllerLeft = Bwr.Controller.EndController(_builder);

      {
        var transform = GetControllerTransform("right");
        Bwr.Controller.StartController(_builder);
        var pos = transform.position;
        Bwr.Controller.AddPosition(_builder, Bwr.Vector3.CreateVector3(_builder, pos.x, pos.y, pos.z));
        var rot = transform.rotation.eulerAngles;
        Bwr.Controller.AddRotationEuler(_builder, Bwr.Vector3.CreateVector3(_builder, rot.x, rot.y, rot.z));
      }
      var controllerRight = Bwr.Controller.EndController(_builder);

      {
        var transform = GetPlayerTransform();
        Bwr.Player.StartPlayer(_builder);
        var pos = transform.position;
        Bwr.Player.AddPosition(_builder, Bwr.Vector3.CreateVector3(_builder, pos.x, pos.y, pos.z));
        Bwr.Player.AddRotation(_builder, transform.rotation.eulerAngles.y);
        Bwr.Player.AddHeadset(_builder, headset);
        Bwr.Player.AddControllerLeft(_builder, controllerLeft);
        Bwr.Player.AddControllerRight(_builder, controllerRight);
      }
      var player = Bwr.Player.EndPlayer(_builder);

      _frames.Add(Bwr.Frame.CreateFrame(_builder, Time.time, player));
      _lastFrameTime = Time.time;
      //Utils.LogDebug($"Recorded frame: {currentSceneIdx} {cam.position.ToString()}");
    }
  }
}
