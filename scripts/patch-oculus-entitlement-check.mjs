// @ts-check
import fs from "fs/promises";
import path from "path";

// Find these by:
// - Opening the DLL in Ghidra
// - Searching for "MarrowGame: Entitlement Failed"
// - Going to the place the string is used
// - Copying the bytes of the CALL instruction right before the TEST AL, AL and
//   `Debug.Log("MarrowGame: Entitlement Failed"); Application.Quit();`
const ENTITLEMENT_CHECK_CALL_BYTES = {
  patch3: "e8 9e f7 37 fe",
  patch4: "e8 01 9e e3 fd",
};

const MOV_AL_1 = "b0 01 90 90 90";

const hexStrToBytes = (/** @type string */ str) =>
  Buffer.from(str.split(" ").map((b) => parseInt(b, 16)));

const gameAssemblyPath = process.argv[2] || "GameAssembly.dll";

const gameAssembly = await fs.readFile(gameAssemblyPath);
for (const [patch, hexStr] of Object.entries(ENTITLEMENT_CHECK_CALL_BYTES)) {
  const bytes = hexStrToBytes(hexStr);
  const matchIndex = gameAssembly.indexOf(bytes);
  if (matchIndex === -1) continue;

  console.log(`Found entitlement check for ${patch}. Patching...`);
  const pathParts = path.parse(gameAssemblyPath);
  await fs.rename(
    gameAssemblyPath,
    path.join(pathParts.dir, pathParts.name + ".original" + pathParts.ext)
  );
  hexStrToBytes(MOV_AL_1).copy(gameAssembly, matchIndex);
  await fs.writeFile(gameAssemblyPath, gameAssembly);
  console.log("Success 😈");
  process.exit(0);
}

console.error(
  "Could not find entitlement check. Is it an Oculus version, patch 3/4 and not already patched?"
);
process.exit(1);
