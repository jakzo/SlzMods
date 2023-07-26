import "./style.css";

import { InputViewer } from "./InputViewer";

const queryParams = new URLSearchParams(window.location.search);
const inputViewer = new InputViewer({
  address:
    queryParams.get("address") ??
    `ws://${queryParams.get("host") ?? "localhost"}:${
      queryParams.get("port") ?? "6161"
    }`,
  container: document.querySelector<HTMLDivElement>("#app")!,
});
inputViewer.start();
