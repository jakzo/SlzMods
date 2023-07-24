import * as THREE from "three";
import { ControllerHud } from "./Hud";
import { PositionViewer } from "./PositionViewer";
import {
  DataIterator,
  Handedness,
  Transforms,
  VirtualXRInputSource,
  iterateBits,
} from "./utils";

const REQUIRED_INPUT_VERSION = 1;

export interface InputViewerOpts {
  address: string;
  container: HTMLElement;
  hideHud?: boolean;
  hidePositions?: boolean;
  showStats?: boolean;
}

export class InputViewer {
  transforms: Transforms = {
    hmd: { position: new THREE.Vector3(), rotation: new THREE.Quaternion() },
    left: { position: new THREE.Vector3(), rotation: new THREE.Quaternion() },
    right: { position: new THREE.Vector3(), rotation: new THREE.Quaternion() },
  };
  controllers: Partial<Record<Handedness, VirtualXRInputSource>> = {};
  huds?: Partial<Record<Handedness, ControllerHud>>;
  positionViewer?: PositionViewer;

  constructor(public opts: InputViewerOpts) {
    if (!opts.hideHud) this.huds = {};
    if (!opts.hidePositions)
      this.positionViewer = new PositionViewer({
        ...opts,
        transforms: this.transforms,
      });
  }

  private createController(handedness: Handedness) {
    const xrInputSource: VirtualXRInputSource = {
      handedness,
      gamepad: {
        index: -1,
        id: "",
        timestamp: 0,
        connected: true,
        hapticActuators: [],
        vibrationActuator: null,
        mapping: "xr-standard",
        axes: [0, 0, 0, 0], // [TouchpadX, TouchpadY, ThumbstickX, ThumbstickY]
        buttons: [
          { pressed: false, touched: false, value: 0 }, // Trigger
          { pressed: false, touched: false, value: 0 }, // Grip
          { pressed: false, touched: false, value: 0 }, // Touchpad
          { pressed: false, touched: false, value: 0 }, // Thumbstick
          { pressed: false, touched: false, value: 0 }, // A
          { pressed: false, touched: false, value: 0 }, // B
          { pressed: false, touched: false, value: 0 }, // Menu
        ],
      },
      profiles: ["oculus-touch-v3"],
      targetRayMode: "tracked-pointer",
      targetRaySpace: new EventTarget(),
    };
    return xrInputSource;
  }

  private createHud(xrInputSource: VirtualXRInputSource) {
    const hud = new ControllerHud({ xrInputSource });
    this.opts.container.append(hud.canvas);
    return hud;
  }

  start() {
    let inputVersion: number | undefined = undefined;
    const ws = new WebSocket(this.opts.address);
    ws.binaryType = "arraybuffer";
    ws.addEventListener("open", () => {
      console.log("Connected to game");
    });
    ws.addEventListener(
      "message",
      (evt: MessageEvent<ArrayBuffer | string>) => {
        if (typeof evt.data === "string") {
          try {
            const msg = JSON.parse(evt.data);
            if (msg.inputVersion) {
              inputVersion = msg.inputVersion;
              console.log("Got input version:", inputVersion);
              if (inputVersion !== REQUIRED_INPUT_VERSION) {
                console.error(
                  "Wrong input version from server:",
                  inputVersion,
                  "but required:",
                  REQUIRED_INPUT_VERSION
                );
              }
            }
          } catch {}
          return;
        }

        if (inputVersion !== REQUIRED_INPUT_VERSION) return;
        const data = new DataIterator(evt.data);
        const type = data.readUint8();
        if (type !== 1) return;

        if (this.positionViewer?.isStopped) {
          this.positionViewer.start();
          this.onControllerConnected("left");
          this.onControllerConnected("right");
        }

        data.readFloat32(); // timestamp
        this.readHmdState(data);
        this.readControllerState(data, "left");
        this.readControllerState(data, "right");
      }
    );
  }

  stop() {
    this.positionViewer?.stop();
  }

  onControllerConnected(handedness: Handedness) {
    const xrInputSource = this.createController(handedness);
    this.controllers[handedness] = xrInputSource;
    if (this.huds) this.huds[handedness] = this.createHud(xrInputSource);
    this.positionViewer?.onControllerConnected(xrInputSource);
  }

  onControllerDisconnected(handedness: Handedness) {
    const controller = this.controllers[handedness];
    if (controller) controller.gamepad.connected = false;
    delete this.controllers[handedness];

    if (this.huds) {
      this.huds[handedness]?.remove();
      delete this.huds[handedness];
    }

    this.positionViewer?.onControllerDisconnected(handedness);
  }

  private readHmdState(data: DataIterator) {
    const { position, rotation } = this.transforms.hmd;
    position.set(data.readFloat32(), data.readFloat32(), data.readFloat32());
    const rx = data.readFloat32();
    const ry = data.readFloat32();
    const rz = data.readFloat32();
    const rw = data.readFloat32();
    rotation.set(rx, -ry, -rz, rw);
  }

  private readControllerState(data: DataIterator, handedness: Handedness) {
    const { position, rotation } = this.transforms[handedness];
    position.set(data.readFloat32(), data.readFloat32(), data.readFloat32());
    const rx = data.readFloat32();
    const ry = data.readFloat32();
    const rz = data.readFloat32();
    const rw = data.readFloat32();
    rotation.set(rx, -ry, -rz, rw);

    const flagIterator = iterateBits(data.readInt32());
    const isConnected = flagIterator.next().value ?? false;
    let controller = this.controllers[handedness];
    if (!isConnected) {
      if (controller) this.onControllerDisconnected(handedness);
    } else {
      if (!controller) {
        this.onControllerConnected(handedness);
        controller = this.controllers[handedness]!;
      }
      for (const i of controller.gamepad.axes.keys()) {
        controller.gamepad.axes[i] = data.readFloat32();
      }
      for (const [i, button] of controller.gamepad.buttons.entries()) {
        button.pressed = flagIterator.next().value ?? false;
        button.touched = flagIterator.next().value ?? false;
        button.value = i < 2 ? data.readFloat32() : button.pressed ? 1 : 0;
      }
    }
    controller?.gamepad;
  }
}
