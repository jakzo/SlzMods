export type Handedness = "left" | "right";
export type VirtualXRInputSource = DeepMutable<XRInputSource> & {
  handedness: Handedness;
  gamepad: DeepMutable<Gamepad>;
};
export type Transforms = Record<
  "hmd" | Handedness,
  { position: THREE.Vector3; rotation: THREE.Quaternion }
>;

export type DeepMutable<T> = { -readonly [P in keyof T]: DeepMutable<T[P]> };

export const throttle = (ms: number, fn: () => void): (() => void) => {
  let isWaiting = false;
  let isCalled = false;
  const onWaited = () => {
    const wasCalled = isCalled;
    isWaiting = isCalled = false;
    if (wasCalled) fn();
  };
  return function throttled() {
    if (isWaiting) {
      isCalled = true;
      return;
    }
    isWaiting = true;
    setTimeout(onWaited, ms);
    fn();
  };
};

export function* iterateBits(int: number) {
  while (int > 0) {
    yield (int & 1) === 1;
    int >>>= 1;
  }
}

export class DataIterator {
  byteOffset = 0;
  view: DataView;
  constructor(public data: ArrayBuffer, public byteOrder = true) {
    this.view = new DataView(data);
  }

  readUint8() {
    const value = this.view.getUint8(this.byteOffset);
    this.byteOffset += Int8Array.BYTES_PER_ELEMENT;
    return value;
  }

  readInt32() {
    const value = this.view.getInt32(this.byteOffset, this.byteOrder);
    this.byteOffset += Int32Array.BYTES_PER_ELEMENT;
    return value;
  }

  readFloat32() {
    const value = this.view.getFloat32(this.byteOffset, this.byteOrder);
    this.byteOffset += Float32Array.BYTES_PER_ELEMENT;
    return value;
  }
}

const createLogFn =
  (type: "info" | "warn" | "error") =>
  (...data: unknown[]) => {
    console[type](...data);
    const notification = document.createElement("div");
    notification.classList.add("notification");
    notification.classList.add(type);
    notification.textContent = data.map(String).join(" ");
    document.querySelector("#notifications")?.append(notification);

    setTimeout(() => {
      notification.classList.add("fade-out");
      setTimeout(() => notification.remove(), 500);
    }, 10 * 1000);
  };

export const log = {
  info: createLogFn("info"),
  warn: createLogFn("warn"),
  error: createLogFn("error"),
};

export const withBaseUrl = (relativeUrl: string) => {
  const baseUrl = window.location.toString();
  return new URL(
    relativeUrl,
    baseUrl + (baseUrl.endsWith("/") ? "" : "/")
  ).toString();
};
