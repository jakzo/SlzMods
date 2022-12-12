import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls";
import Stats from "three/examples/jsm/libs/stats.module";
import { VRButton } from "three/examples/jsm/webxr/VRButton";
import { XRControllerModelFactory } from "three/examples/jsm/webxr/XRControllerModelFactory";

import { createEnvironment } from "./Environment";
import { createHeadset } from "./Headset";

export interface InputViewerOpts {
  container: HTMLElement;
  showStats?: boolean;
}

export class InputViewer {
  stats?: Stats;
  scene: THREE.Scene;
  camera: THREE.PerspectiveCamera;
  renderer: THREE.WebGLRenderer;
  resizeObserver: ResizeObserver;
  timePrev?: number;
  controllers: THREE.Group[];
  headset: THREE.Object3D;

  isStopped = true;

  constructor(opts: InputViewerOpts) {
    this.scene = new THREE.Scene();

    this.camera = new THREE.PerspectiveCamera(75, 1, 0.1, 1000);
    this.camera.position.set(0, 1.6, 3);
    this.camera.lookAt(0, 1, 0);

    this.renderer = new THREE.WebGLRenderer({ antialias: true });
    this.renderer.xr.enabled = true;
    this.renderer.setPixelRatio(window.devicePixelRatio);
    const setSize = (): void => {
      const width = opts.container.clientWidth;
      const height = opts.container.clientHeight;
      this.camera.aspect = width / height;
      this.camera.updateProjectionMatrix();
      this.renderer.setSize(width, height);
    };
    setSize();
    this.resizeObserver = new ResizeObserver(setSize);
    this.resizeObserver.observe(opts.container);
    opts.container.append(this.renderer.domElement);
    opts.container.appendChild(VRButton.createButton(this.renderer));

    const controllerModelFactory = new XRControllerModelFactory();
    for (const i of [0, 1]) {
      const controller = this.renderer.xr.getControllerGrip(i);
      controller.add(controllerModelFactory.createControllerModel(controller));
      this.scene.add(controller);
    }

    this.headset = createHeadset();
    this.scene.add(this.headset);

    this.stats = opts.showStats ? Stats() : undefined;
    if (this.stats) opts.container.appendChild(this.stats.dom);

    this.scene.add(createEnvironment());

    const orbitControls = new OrbitControls(
      this.camera,
      this.renderer.domElement
    );
    orbitControls.target = new THREE.Vector3(0, 1, 0);
    orbitControls.update();

    this.controllers = [0, 1].map((i) => {
      const controller = this.renderer.xr.getControllerGrip(i);
      controller.addEventListener("connected", () => {
        this.scene.add(controller);
      });
      controller.addEventListener("disconnected", () => {
        this.scene.remove(controller);
      });
      return controller;
    });
  }

  start() {
    if (!this.isStopped) return;
    this.renderer.setAnimationLoop(this.animate);
    this.timePrev = undefined;
    this.isStopped = false;
  }

  stop() {
    if (this.isStopped) return;
    this.renderer.setAnimationLoop(null);
    this.timePrev = undefined;
    this.isStopped = true;
  }

  animate = (time: number): void => {
    this.stats?.begin();

    if (this.timePrev === undefined) this.timePrev = time;

    // const timeDelta = (time - this.timePrev) / 1000;
    // this.renderer.xr.isPresenting

    this.renderer.render(this.scene, this.camera);

    this.timePrev = time;
    this.stats?.end();
  };
}
