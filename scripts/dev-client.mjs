import http from "http";
import dgram from "dgram";
import net from "net";

const SERVER_IP = "192.168.0.37";

const wake = (/** @type string */ macAddress) =>
  new Promise((resolve, reject) => {
    const address = "255.255.255.255";
    const protocol = net.isIPv6(address) ? "udp6" : "udp4";
    const port = 9;

    const magicPacket = Buffer.concat([
      Buffer.from("FF".repeat(6), "hex"),
      ...Array(16).fill(
        Buffer.from(macAddress.replace(/[^0-9a-f]/gi, ""), "hex")
      ),
    ]);

    const socket = dgram.createSocket(protocol);
    socket.send(magicPacket, 0, magicPacket.length, port, address, (error) => {
      try {
        socket.close();
      } catch {}

      if (error) reject(error);
      else resolve();
    });

    socket.on("error", (error) => {
      reject(error);
      socket.close();
    });

    socket.once("listening", () => {
      socket.setBroadcast(true);
    });
  });

const sendRequest = (/** @type string */ pathname, body) =>
  new Promise((resolve, reject) => {
    const req = http.request(new URL(pathname, `http://${SERVER_IP}`), {
      method: "POST",
    });

    req.on("connect", () => {});
  });

const sendMods = async () => {
  await sendRequest("/bonelab/test-mod", {});
};

const bonelabTestMod = async () => {
  console.log("Waking");
  await wake("04:7C:16:AA:B2:C0");

  console.log("Sending mods");
  sendMods();

  console.log("Done");
};

await bonelabTestMod();
