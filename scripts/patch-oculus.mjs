// @ts-check
import fs from "fs/promises";
import path from "path";

// To find these:
// - In Player.log there should be a line like "MarrowGame: XXX Failed"
// - Open the DLL in Ghidra
// - Search for the log line
// - Go to the place the string is used
// - Open the function which is called right before the TEST AL, AL and
//   `Debug.Log(logLine); Application.Quit();`
// - Copy the starting bytes of this function (enough that it is unique)
const EC_FUNC_BYTES = {
  default:
    "40 53 48 83 ec 20 48 8b 19 48 85 db 0f 84 b3 00 00 00 48 89 7c 24 30 48 8b 42 20 0f b7 79 0a",
};

const MOV_AL_1_RET = "b0 01 c3";

const hexStrToBytes = (/** @type string */ str) =>
  Buffer.from(str.split(/\s+/).map((b) => parseInt(b, 16)));

const gameAssemblyPath = process.argv[2] || "GameAssembly.dll";

const gameAssembly = await fs.readFile(gameAssemblyPath);
for (const [patch, hexStr] of Object.entries(EC_FUNC_BYTES)) {
  const bytes = hexStrToBytes(hexStr);
  const matchIndex = gameAssembly.indexOf(bytes);
  if (matchIndex === -1) continue;

  console.log(`Found for ${patch}. Patching...`);
  const pathParts = path.parse(gameAssemblyPath);
  await fs.rename(
    gameAssemblyPath,
    path.join(pathParts.dir, pathParts.name + ".original" + pathParts.ext)
  );
  hexStrToBytes(MOV_AL_1_RET).copy(gameAssembly, matchIndex);
  await fs.writeFile(gameAssemblyPath, gameAssembly);
  console.log("Success");
  process.exit(0);
}

console.error(
  "Could not find. Is it a supported Oculus version and not already patched?"
);
process.exit(1);
