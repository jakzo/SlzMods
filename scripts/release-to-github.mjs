// @ts-check
import fs from "fs/promises";
import path from "path";

const fileExists = (filePath) =>
  fs.stat(filePath).then(
    () => true,
    () => false
  );

const [, , game, project] = process.argv;
if (!game || !project) {
  console.error("Usage: node ./scripts/release-to-github.mjs GAME PROJECT");
  process.exit(1);
}

const projectDir = path.join("projects", game, project);
const distDir = path.join(projectDir, "dist", "build");
await fs.rm(distDir, { recursive: true, force: true });

const releaseDir = path.join(projectDir, "bin", "Release");
for (const entry of await fs.readdir(releaseDir, { withFileTypes: true })) {
  if (!entry.isDirectory()) continue;
  const buildDir = path.join(releaseDir, entry.name);
  const outputDir = path.join(distDir, entry.name, "Mods");
  for (const entry of await fs.readdir(buildDir, { withFileTypes: true })) {
    if (!entry.isFile() || !entry.name.endsWith(".dll")) continue;
    await fs.mkdir(outputDir, { recursive: true });
    await fs.copyFile(
      path.join(buildDir, entry.name),
      path.join(outputDir, entry.name)
    );
  }
}

await fs.copyFile(
  path.join(projectDir, "README.md"),
  path.join(distDir, "README.md")
);
await fs.copyFile(
  path.join(projectDir, "CHANGELOG.md"),
  path.join(distDir, "CHANGELOG.md")
);
if (await fileExists(path.join(projectDir, "assets"))) {
  await fs.cp(path.join(projectDir, "assets"), path.join(distDir, "assets"), {
    recursive: true,
    force: true,
  });
}
