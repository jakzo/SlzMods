
using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using UnityEngine;
using SLZ.Marrow.Input;
using SLZ.Marrow.Utilities;

namespace Sst.SpeedrunTimer {
public class Server {
  private HttpListener listener;
  private ConcurrentDictionary<WebSocket, byte> sockets =
      new ConcurrentDictionary<WebSocket, byte>();
  private string address;

  public Server(string address = "http://localhost:6161/") {
    this.address = address;
    listener = new HttpListener();
    listener.Prefixes.Add(this.address);
    listener.Start();
    var prefixes = string.Join("\n", listener.Prefixes);
    MelonLogger.Msg($"Input viewer server started at:\n{prefixes}");

    Task.Run(() => AcceptWebSocketConnections());
  }

  private async Task AcceptWebSocketConnections() {
    while (true) {
      var context = await listener.GetContextAsync();
      if (context.Request.IsWebSocketRequest) {
        var wsContext = await context.AcceptWebSocketAsync(null);
        var ws = wsContext.WebSocket;
        sockets.TryAdd(ws, 0);
        var _ = Task.Run(() => HandleClient(ws));
      }
    }
  }

  private async Task HandleClient(WebSocket ws) {
    // TODO
    // var msgJson = NewtonSoft.stringify({ inputVersion: 1 });
    // await ws.SendAsync(new ArraySegment<byte>(msgJson));
    while (ws.State == WebSocketState.Open) {
      var buffer = new byte[1024];
      var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer),
                                         CancellationToken.None);

      if (result.MessageType == WebSocketMessageType.Close) {
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "",
                            CancellationToken.None);
        sockets.TryRemove(ws, out byte _);
      }
    }
  }

  public void Send(List<byte> data) {
    var segment = new ArraySegment<byte>(data.ToArray());
    foreach (var socket in sockets.Keys.ToList()) {
      if (socket.State == WebSocketState.Open) {
        Task.Run(() => socket.SendAsync(segment, WebSocketMessageType.Binary,
                                        true, CancellationToken.None));
      } else {
        sockets.TryRemove(socket, out byte _);
      }
    }
  }

  public void SendInputState() {
    // IMPORTANT: Update the inputVersion number if any size/order is changed
    var data = new List<byte>(128) { 0 };

    AddToData(data, Time.unscaledTime);

    AddToData(data, MarrowGame.xr.HMD);

    foreach (var controller in new[] { MarrowGame.xr.LeftController,
                                       MarrowGame.xr.RightController }) {
      AddToData(data, controller);

      var i = 0;
      data.AddRange(BitConverter.GetBytes(
          new bool[] {
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
          }
              .Aggregate(0, (acc, val) => acc | ((val ? 1 : 0) << i++))));

      AddToData(data, controller.Touchpad2DAxis);
      AddToData(data, controller.Joystick2DAxis);

      foreach (var value in new float[] {
                 controller.Trigger,
                 controller.Grip,
                 controller.TouchpadButton ? 1f : 0f,
                 controller.JoystickButton ? 1f : 0f,
                 controller.AButton ? 1f : 0f,
                 controller.BButton ? 1f : 0f,
                 controller.MenuButton ? 1f : 0f,
               }) {
        AddToData(data, value);
      }
    }
    Send(data);
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
}
}
