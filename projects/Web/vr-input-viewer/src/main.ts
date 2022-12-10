import "./style.css";

import { InputViewer } from "./InputViewer";

const inputViewer = new InputViewer({
  container: document.querySelector<HTMLDivElement>("#app")!,
});
inputViewer.start();
