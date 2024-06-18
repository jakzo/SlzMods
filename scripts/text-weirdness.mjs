// @ts-check
import fs from "fs/promises";

const gameAssemblyPath = process.argv[2] || "GameAssembly.dll";

const gameAssembly = await fs.readFile(gameAssemblyPath);
const arbitraryBoundary = gameAssembly.indexOf(
  Buffer.from("Germanic", "ascii")
);

for (let i = -1000; i < 1000; i++) {
  const size = 18;
  const index = arbitraryBoundary + i * size;
  const chunk = gameAssembly.subarray(index, index + size);
  const numNonChars = chunk.reduce(
    (n, b) => n + (b < 32 || b > 126 ? 1 : 0),
    0
  );
  if (numNonChars >= 2) continue;
  const text = chunk.toString("ascii");
  console.log(text);
}
