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

// Opening websocket to somewhere other than localhost doesn't work due to
// browser security (request from HTTPS page to HTTP server is disallowed)
if (window.location.protocol !== "https:") {
  document.addEventListener("keypress", (evt) => {
    if (evt.key === "Enter" && !hostForm.parentElement) {
      showHostInput();
    }
  });

  const hostForm = document.createElement("form");
  hostForm.classList.add("host");
  const hostInput = document.createElement("input");
  hostInput.value = queryParams.get("host") ?? "";
  hostInput.placeholder = "Enter IP of game + Enter";
  hostForm.append(hostInput);
  hostForm.addEventListener("submit", (evt) => {
    evt.preventDefault();
    const host = hostInput.value.trim();
    if (host) {
      queryParams.set("host", host);
      window.location.assign(`?${queryParams.toString()}`);
    }
  });
  const showHostInput = () => {
    document.body.append(hostForm);
    hostInput.focus();
  };
}
