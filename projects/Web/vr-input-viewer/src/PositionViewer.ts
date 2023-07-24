import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls";
import Stats from "three/examples/jsm/libs/stats.module";
import { XRControllerModelFactory } from "three/examples/jsm/webxr/XRControllerModelFactory";

import { createEnvironment } from "./Environment";
import { createHeadset } from "./Headset";
import { ControllerHud } from "./Hud";
import {
  Handedness,
  Transforms,
  VirtualXRInputSource,
  throttle,
} from "./utils";

export interface PositionViewerOpts {
  container: HTMLElement;
  transforms: Transforms;
  showStats?: boolean;
}

export class PositionViewer {
  stats?: Stats;
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
  // hudScene: THREE.Scene;
  // hudCamera: THREE.OrthographicCamera;
  huds: ControllerHud[] = [];
  renderer: THREE.WebGLRenderer;
  resizeObserver: ResizeObserver;
  controllerModelFactory: XRControllerModelFactory =
    new XRControllerModelFactory();
  controllers: Record<
    "left" | "right",
    { gripSpace: THREE.Group; container: THREE.Group }
  >;
  headset: THREE.Object3D;
  transforms: [THREE.Object3D, Transforms[keyof Transforms]][];

  isStopped = true;

  constructor(public opts: PositionViewerOpts) {
    this.scene = new THREE.Scene();

    this.camera = new THREE.PerspectiveCamera(75, 1, 0.1, 1000);
    this.camera.position.set(0, 1.2, -0.5);
    this.camera.lookAt(0, 1.2, 0);

    // this.hudScene = new THREE.Scene();

    // this.hudCamera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 1000);
    // this.hudCamera.lookAt(0, 1, 0);

    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.setPixelRatio(window.devicePixelRatio);
    const setSize = throttle(100, () => {
      const width = opts.container.clientWidth;
      const height = opts.container.clientHeight;
      this.camera.aspect = width / height;
      this.camera.updateProjectionMatrix();
      // this.hudCamera.left = width / -2;
      // this.hudCamera.right = width / 2;
      // this.hudCamera.top = height / 2;
      // this.hudCamera.bottom = height / -2;
      // this.hudCamera.updateProjectionMatrix();
      this.renderer.setSize(width, height);
    });
    setSize();
    this.resizeObserver = new ResizeObserver(setSize);
    this.resizeObserver.observe(opts.container);
    opts.container.append(this.renderer.domElement);

    this.controllers = {
      left: this.createController(),
      right: this.createController(),
    };

    this.headset = createHeadset();
    this.scene.add(this.headset);

    this.transforms = [
      [this.headset, opts.transforms.hmd],
      [this.controllers.left.container, opts.transforms.left],
      [this.controllers.right.container, opts.transforms.right],
    ];

    this.stats = opts.showStats ? Stats() : undefined;
    if (this.stats) opts.container.appendChild(this.stats.dom);

    this.scene.add(createEnvironment());

    const orbitControls = new OrbitControls(
      this.camera,
      this.renderer.domElement
    );
    orbitControls.target = new THREE.Vector3(0, 1.2, 0);
    orbitControls.update();
  }

  private createController() {
    const container = new THREE.Group();
    const gripSpace = new THREE.Group();
    gripSpace.add(this.controllerModelFactory.createControllerModel(gripSpace));
    container.add(gripSpace);
    gripSpace.position.z += 0.02;
    this.scene.add(container);
    return { gripSpace, container };
  }

  onControllerConnected(xrInputSource: VirtualXRInputSource) {
    const { gripSpace } = this.controllers[xrInputSource.handedness];
    gripSpace.dispatchEvent({ type: "connected", data: xrInputSource });
  }

  onControllerDisconnected(handedness: Handedness) {
    const { gripSpace } = this.controllers[handedness];
    gripSpace.dispatchEvent({ type: "disconnected" });
  }

  start() {
    if (!this.isStopped) return;
    this.renderer.setAnimationLoop(this.animate);
    this.isStopped = false;
  }

  stop() {
    if (this.isStopped) return;
    this.renderer.setAnimationLoop(null);
    this.isStopped = true;
  }

  animate = (_time: number): void => {
    this.stats?.begin();

    for (const [obj, { position, rotation }] of this.transforms) {
      obj.position.x = -position.x;
      obj.position.y = position.y;
      obj.position.z = position.z;

      obj.setRotationFromQuaternion(rotation);
      obj.rotateX(Math.PI * 0.75);
      obj.rotateZ(Math.PI);

      // obj.rotation.x = rotation.x * ((2 * Math.PI) / 360);
      // obj.rotation.y = rotation.y * ((2 * Math.PI) / 360);
      // obj.rotation.z = rotation.z * ((2 * Math.PI) / 360);
      // obj.rotation.x = Math.PI / 2;
      // obj.rotation.y = 0;
      // obj.rotation.z = Math.PI;
      // obj.rotateZ((-rotation.z / 360) * 2 * Math.PI);
      // obj.rotateY((rotation.y / 360) * 2 * Math.PI);
      // obj.rotateX((rotation.x / 360) * 2 * Math.PI);

      // obj.quaternion.x = rotation.x;
      // obj.quaternion.y = rotation.z;
      // obj.quaternion.z = rotation.y;
      // obj.quaternion.w = rotation.w;

      // obj.quaternion.multiply(
      //   new THREE.Quaternion().setFromAxisAngle(
      //     new THREE.Vector3(1, 0, 0),
      //     Math.PI / 2
      //   )
      // );
      // obj.quaternion.multiply(
      //   new THREE.Quaternion().setFromAxisAngle(
      //     new THREE.Vector3(0, 0, 1),
      //     Math.PI
      //   )
      // );

      // const euler = new THREE.Euler().setFromQuaternion(obj.quaternion, "YZX");
      // [euler.y, euler.z] = [euler.z, euler.y];
      // obj.quaternion.setFromEuler(euler);

      // obj.rotateX(Math.PI);

      // obj.rotateX(Math.PI * 0.5);
      // obj.rotateY(Math.PI * 1.0);
      // obj.rotateZ(Math.PI * 1.0);
      // [obj.rotation.y, obj.rotation.z] = [obj.rotation.z, obj.rotation.y];
      obj.updateMatrix();
    }

    for (const hud of this.huds) hud.update();

    this.renderer.render(this.scene, this.camera);

    this.stats?.end();
  };
}
