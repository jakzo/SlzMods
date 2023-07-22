import "./style.css";

import { InputViewer } from "./InputViewer";

const inputViewer = new InputViewer({
  address: "ws://localhost:6161",
  container: document.querySelector<HTMLDivElement>("#app")!,
});
inputViewer.start();
