import * as THREE from "three";
import { Transforms } from "../utils";

export class HeightTrackers {
  container = new THREE.Object3D();
  trackers: Record<keyof Transforms, THREE.Mesh>;
  heightLog = new LinkedList<[number, number]>();
  heightMarker: THREE.Mesh;

  constructor(
    public transforms: Transforms,
    public medianHeightWindow: number
  ) {
    const material = new THREE.MeshBasicMaterial({
      color: 0xffffff,
      transparent: true,
      opacity: 0.5,
      side: THREE.FrontSide,
    });

    this.trackers = {
      hmd: this.createTracker(material),
      left: this.createTracker(material),
      right: this.createTracker(material),
    };

    this.heightMarker = new THREE.Mesh(
      new THREE.SphereGeometry(0.03),
      new THREE.MeshBasicMaterial({ color: "#ff3333" })
    );
    this.container.add(this.heightMarker);
  }

  createTracker(material: THREE.Material): THREE.Mesh {
    const geometry = new THREE.CylinderGeometry(0.01, 0.01, 1, 32);
    const mesh = new THREE.Mesh(geometry, material);
    this.container.add(mesh);
    return mesh;
  }

  update() {
    for (const key of Object.keys(this.transforms) as (keyof Transforms)[]) {
      const position = this.transforms[key].position;
      const tracker = this.trackers[key];

      tracker.position.x = -position.x;
      tracker.position.y = position.y / 2;
      tracker.position.z = position.z;
      tracker.scale.y = position.y;
    }

    const now = Date.now();
    const windowStart = now - this.medianHeightWindow * 1000;
    while (
      this.heightLog.first &&
      this.heightLog.first.value[0] < windowStart
    ) {
      this.heightLog.shift();
    }
    const hmdPos = this.transforms.hmd.position;
    this.heightLog.push([now, hmdPos.y]);
    this.heightMarker.position.set(
      -hmdPos.x,
      this.heightLog.middle
        ? this.heightLog.size % 2 === 1
          ? this.heightLog.middle.value[1]
          : (this.heightLog.middle.value[1] +
              this.heightLog.middle.next!.value[1]) /
            2
        : 0,
      hmdPos.z
    );
  }
}

class LinkedList<T> {
  first?: LinkedListNode<T>;
  last?: LinkedListNode<T>;
  middle?: LinkedListNode<T>;
  size = 0;

  push(value: T) {
    const prevLast = this.last;
    this.last = new LinkedListNode(value);
    this.size++;
    if (prevLast) {
      this.last.prev = prevLast;
      prevLast.next = this.last;
      if (this.size % 2 === 1) this.middle = this.middle!.next;
    } else {
      this.first = this.middle = this.last;
    }
  }

  shift(): T | undefined {
    if (!this.first) return undefined;
    const value = this.first.value;
    this.first = this.first.next;
    this.size--;
    if (this.first) {
      this.first.prev = undefined;
      if (this.size % 2 === 1) this.middle = this.middle!.next;
    } else {
      this.middle = this.last = undefined;
    }
    return value;
  }
}

class LinkedListNode<T> {
  next?: LinkedListNode<T>;
  prev?: LinkedListNode<T>;
  constructor(public value: T) {}
}
