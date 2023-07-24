import "./Hud.css";
import { VirtualXRInputSource } from "./utils";

export interface ControllerHudOpts {
  xrInputSource: VirtualXRInputSource;
}

export interface ControllerHud extends ControllerHudOpts {}
export class ControllerHud {
  canvas: SVGSVGElement;
  buttonA = createSvg("circle", ["button", "a"]);
  buttonB = createSvg("circle", ["button", "b"]);
  thumbstick = createSvg("circle", ["stick"]);
  triggerPrimary = createPath(
    ["trigger", "primary"],
    [
      ["M", 0, 0],
      ["L", 0, 30],
      ["A", 30, 30, 0, 0, 0, ...angleToCoord(30, 50)],
      ["A", 75, 75, 0, 0, 1, 5, 0],
      ["Z"],
    ]
  );
  triggerSecondary = createPath(
    ["trigger", "secondary"],
    [
      ["M", 0, 0],
      ["L", 10, 0],
      ["A", 15, 15, 0, 0, 1, 10, 20],
      ["L", 0, 20],
      ["Z"],
    ]
  );

  buttons: [SVGElement, number][] = [
    [this.triggerPrimary, 0],
    [this.triggerSecondary, 1],
    [this.thumbstick, 3],
    [this.buttonA, 4],
    [this.buttonB, 5],
  ];

  constructor(opts: ControllerHudOpts) {
    this.xrInputSource = opts.xrInputSource;

    this.canvas = createSvg("svg", [
      "controller-hud",
      this.xrInputSource.handedness,
    ]);
    this.canvas.setAttribute("viewBox", "0 0 100 100");

    const thumbstickContainer = createSvg("g", ["thumbstick"]);
    thumbstickContainer.append(createSvg("circle", ["background"]));
    thumbstickContainer.append(this.thumbstick);

    this.canvas.append(
      this.buttonA,
      this.buttonB,
      thumbstickContainer,
      this.triggerPrimary,
      this.triggerSecondary
    );

    requestAnimationFrame(this.update);
  }

  remove() {
    this.canvas.remove();
  }

  update = () => {
    if (!this.canvas.parentNode) return;
    requestAnimationFrame(this.update);
    for (const [hudElement, buttonIndex] of this.buttons) {
      const state = this.xrInputSource.gamepad?.buttons[buttonIndex];

      if (state?.touched) hudElement.classList.add("touched");
      else hudElement.classList.remove("touched");

      if (state?.pressed) hudElement.classList.add("pressed");
      else hudElement.classList.remove("pressed");
    }

    const { axes } = this.xrInputSource.gamepad;
    const isLeft = this.xrInputSource.handedness === "left";
    this.thumbstick.style.translate = `${axes[2] * -13 * (isLeft ? -1 : 1)}px ${
      axes[3] * -13
    }px`;
  };
}

const createSvg = <K extends keyof SVGElementTagNameMap>(
  tagName: K,
  classNames: string[]
): SVGElementTagNameMap[K] => {
  const element = document.createElementNS(
    "http://www.w3.org/2000/svg",
    tagName
  );
  for (const className of classNames) element.classList.add(className);
  return element;
};

const createPath = (classNames: string[], data: [string, ...number[]][]) => {
  const path = createSvg("path", classNames);
  path.setAttribute(
    "d",
    data.map(([command, ...values]) => command + values.join(",")).join(" ")
  );
  return path;
};

const angleToCoord = (
  radius: number,
  angleDegrees: number
): [number, number] => [
  Math.sin((Math.PI / 180) * angleDegrees) * radius,
  Math.cos((Math.PI / 180) * angleDegrees) * radius,
];
