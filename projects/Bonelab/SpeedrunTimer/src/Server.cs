using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using MelonLoader;
using UnityEngine;
using SLZ.Marrow.Input;
using SLZ.Marrow.Utilities;

namespace Sst.SpeedrunTimer {
public class Server {
  private const float DEGREES_TO_RADIANS = 2f * (float)Math.PI / 360f;

  private WebsocketServer websocketServer;

  public Server(int port = 6161, string ip = "127.0.0.1") {
    websocketServer = new WebsocketServer() {
      OnConnect = client => _ =
          websocketServer.Send("{\"inputVersion\":1}", client),
      // OnMessage = (msg, client) => MelonLogger.Msg($"Websocket: {msg}"),
    };
    Task.Run(() => websocketServer.Start(port, ip));
    MelonLogger.Msg($"Input viewer server started at: ws://{ip}:{port}");
  }

  public void SendInputState() {
    if (MarrowGame.xr?.HMD == null)
      return;

    // IMPORTANT: Update the inputVersion number if any size/order is changed
    byte messageType = 1;
    var data = new List<byte>(128) { messageType };

    AddToData(data, Time.unscaledTime);

    // var rigManager = Mod.Instance.RigManager;
    SLZ.Rig.RigManager rigManager = null;
    if (rigManager) {
      AddToData(data, rigManager.ControllerRig.m_head);
    } else {
      AddToData(data, MarrowGame.xr.HMD);
    }

    foreach (var (controller, rigController)
                 in new[] { (MarrowGame.xr.LeftController,
                             rigManager?.ControllerRig.leftController),
                            (MarrowGame.xr.RightController,
                             rigManager?.ControllerRig.rightController) }) {
      if (rigController) {
        AddToData(data, rigController.transform);
      } else {
        AddToData(data, controller);
      }

      var i = 0;
      data.AddRange(BitConverter.GetBytes(
          new bool[] {
            controller.IsConnected,
            rigController?._primaryInteractionButton ??
                controller.TriggerButton,
            controller.TriggerTouched,
            controller.GripButton,
            false,
            rigController?._touchPad ?? controller.TouchpadButton,
            rigController?._touchPadTouch ?? controller.TouchpadTouch,
            rigController?._thumbstick ?? controller.JoystickButton,
            rigController?._thumbstickTouch ?? controller.JoystickTouch,
            rigController?._aButton ?? controller.AButton,
            controller.ATouch,
            rigController?._bButton ?? controller.BButton,
            controller.BTouch,
            controller.MenuButton,
            false,
          }
              .Aggregate(0, (acc, val) => acc | ((val ? 1 : 0) << i++))));

      AddToData(data,
                rigController?._touchPadAxis ?? controller.Touchpad2DAxis);
      AddToData(data,
                rigController?._thumbstickAxis ?? controller.Joystick2DAxis);

      AddToData(data, rigController?._primaryAxis ?? controller.Trigger);
      AddToData(data, rigController?.GetSecondaryInteractionButtonAxis() ??
                          controller.Grip);
    }
    websocketServer.Send(data.ToArray());
  }

  private void AddToData(List<byte> data, int value) {
    data.AddRange(BitConverter.GetBytes(value));
  }
  private void AddToData(List<byte> data, float value) {
    data.AddRange(BitConverter.GetBytes(value));
  }
  private void AddToData(List<byte> data, Vector2 value) {
    AddToData(data, value.x);
    AddToData(data, value.y);
  }
  private void AddToData(List<byte> data, Vector3 value) {
    AddToData(data, value.x);
    AddToData(data, value.y);
    AddToData(data, value.z);
  }
  private void AddToData(List<byte> data, Quaternion value) {
    AddToData(data, value.x);
    AddToData(data, value.y);
    AddToData(data, value.z);
    AddToData(data, value.w);
  }
  private void AddToData(List<byte> data, XRDevice value) {
    AddToData(data, value.Position);
    AddToData(data, value.Rotation);
  }
  private void AddToData(List<byte> data, Transform value) {
    AddToData(data, value.localPosition);
    AddToData(data, value.localRotation);
  }
}
}
