// @ts-check
import http from "http";
import fs from "fs/promises";
import path from "path";
import { spawn } from "child_process";

const BONELAB_EXE = "bonelab.exe";

const isBonelabDir = async (/** @type string */ dir) =>
  fs.stat(path.join(dir, BONELAB_EXE)).then(
    () => true,
    () => false
  );

const bonelabDir = process.cwd();
if (!isBonelabDir(bonelabDir))
  throw new Error("Current directory is not Bonelab");

const quitBonelab = () =>
  /** @type Promise<void> */ (
    new Promise((resolve, reject) => {
      const child = spawn("taskkill", ["/IM", BONELAB_EXE, "/F"]);
      child.on("error", reject);
      child.on("close", (code) => {
        if (code === 0) resolve();
        else reject(`Failed to quit Bonelab with code: ${code}`);
      });
    })
  );

const copyFiles = async (
  /** @type Record<String, string> */ files,
  /** @type string */ dir
) => {
  for (const [relativePath, base64Contents] of Object.entries(files)) {
    const filePath = path.join(dir, relativePath);
    const contents = Buffer.from(base64Contents, "base64");
    await fs.mkdir(path.dirname(filePath), { recursive: true });
    await fs.writeFile(filePath, contents);
  }
};

const startBonelab = () =>
  /** @type Promise<void> */ (
    new Promise((resolve, reject) => {
      const child = spawn(path.join(bonelabDir, BONELAB_EXE), {
        detached: true,
      });
      child.on("error", reject);
      child.on("close", (code) =>
        reject(new Error(`Bonelab exited with code: ${code}`))
      );
      setTimeout(() => {
        child.removeAllListeners();
        resolve();
      }, 1000);
    })
  );

const bonelabTestMod = async ({
  /** @type boolean */ clearMods,
  /** @type Record<string, string> Key = file path relative to bonelab dir, value = base64 contents */ files,
}) => {
  await quitBonelab();

  if (clearMods) {
    await fs.rm(path.join(bonelabDir, "Mods"), {
      recursive: true,
      force: true,
    });
    await fs.rm(path.join(bonelabDir, "Plugins"), {
      recursive: true,
      force: true,
    });
  }

  await copyFiles(files, bonelabDir);

  await startBonelab();
};

const routes = {
  "/bonelab/test-mod": bonelabTestMod,
};

const startServer = (
  /** @type number */ port,
  /** @type string | undefined */ host
) =>
  new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => {
      const respond = (/** @type number */ status, data) => {
        res.statusCode = status;
        res.end(JSON.stringify(data));
      };

      try {
        const url = new URL(
          req.url ?? "",
          `http://${req.headers.host ?? "localhost"}`
        );
        const route = routes[url.pathname];
        if (!route) {
          respond(404, { error: "Route not found" });
          return;
        }
        if (req.method !== "POST") {
          respond(405, { error: "Method must be POST" });
          return;
        }

        let bodyStr = "";
        req.on("data", (data) => (bodyStr += data));
        req.on("end", () => {
          const parseBody = () => {
            try {
              return JSON.parse(bodyStr);
            } catch (err) {
              respond(400, {
                error: "Failed to parse body",
                message: String(err),
              });
            }
          };

          const body = parseBody();
          try {
            route(body);
          } catch (err) {
            respond(500, {
              error: "Server error",
              message: String(err),
              stack: err?.stack,
            });
          }
        });
      } catch (err) {
        respond(500, {
          error: "Server error",
          message: String(err),
          stack: err?.stack,
        });
      }
    });
    server.listen(port, host, () => resolve(server)).on("error", reject);
  });

await startServer(1234);
