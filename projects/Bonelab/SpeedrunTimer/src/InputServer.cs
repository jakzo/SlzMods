using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;

#if ML6
using Il2CppSLZ.Marrow.Input;
using Il2CppSLZ.Marrow.Utilities;
#else
using SLZ.Marrow.Input;
using SLZ.Marrow.Utilities;
#endif

namespace Sst.SpeedrunTimer {
public class InputServer {
  private const float DEGREES_TO_RADIANS = 2f * (float)Math.PI / 360f;

  private WebsocketServer websocketServer;

  public InputServer(int port = 6161, string ip = null) {
    websocketServer = new WebsocketServer() {
      OnConnect = client => client.Send("{\"inputVersion\":1}"),
      // OnMessage = (msg, client) => MelonLogger.Msg($"Websocket: {msg}"),
    };
    websocketServer.Start(port, ip);
    var addresses = (ip != null ? new[] { ip } : Network.GetAllAddresses())
                        .Select(address => $"ws://{address}:{port}");
    var addressText = string.Join("\n", addresses);
    MelonLogger.Msg($"Input viewer server started at:\n{addressText}");
  }

  public void SendInputState() {
    if (MarrowGame.xr?.HMD == null)
      return;

    // IMPORTANT: Update the inputVersion number if any size/order is changed
    byte messageType = 1;
    var data = new List<byte>(128) { messageType };

    AddToData(data, Time.unscaledTime);

    AddToData(data, MarrowGame.xr.HMD);

    foreach (var controller in new[] { MarrowGame.xr.LeftController,
                                       MarrowGame.xr.RightController }) {
      if (controller != null) {
        AddToData(data, controller);
      } else {
        AddToData(data, new Vector3());
        AddToData(data, new Quaternion());
      }

      var bools = new bool[] {
        controller.IsConnected,
        controller.TriggerButton,
        controller.TriggerTouched,
        controller.GripButton,
        false,
        controller.TouchpadButton,
        controller.TouchpadTouch,
        controller.JoystickButton,
        controller.JoystickTouch,
        controller.AButton,
        controller.ATouch,
        controller.BButton,
        controller.BTouch,
        controller.MenuButton,
        false,
      };
      var boolBitField = 0;
      var i = 0;
      foreach (var value in bools) {
        if (value)
          boolBitField |= 1 << i;
        i++;
      }
      data.AddRange(BitConverter.GetBytes(boolBitField));

      AddToData(data, controller.Touchpad2DAxis);
      AddToData(data, controller.Joystick2DAxis);

      AddToData(data, controller.Trigger);
      AddToData(data, controller.Grip);
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
