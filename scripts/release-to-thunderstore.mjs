import { execSync } from "child_process";
import fs from "fs/promises";
import path from "path";

const fileExists = (filePath) =>
  fs.stat(filePath).then(
    () => true,
    () => false
  );

const [, , game, project, newVersion] = process.argv;
if (!game || !project || !newVersion) {
  console.error(
    "Usage: node ./scripts/release-to-thunderstore.mjs GAME PROJECT NEW_VERSION"
  );
  process.exit(1);
}

const projectDir = path.join("projects", game, project);
const readmeContents = await fs.readFile(
  path.join(projectDir, "README.md"),
  "utf8"
);
const changelogContents = await fs.readFile(
  path.join(projectDir, "CHANGELOG.md"),
  "utf8"
);
const thunderstoreDir = path.join(projectDir, "thunderstore");
const configPath = path.join(thunderstoreDir, "thunderstore.toml");
const config = await fs.readFile(configPath, "utf8");

const distDir = path.join(thunderstoreDir, "dist");
await fs.rm(distDir, { recursive: true, force: true });
const modsDir = path.join(distDir, "Mods");
const releasePath = path.join(projectDir, "bin", "Release");
const filePath = path.join(releasePath, `${project}.dll`);
if (await fileExists(filePath)) {
  await fs.mkdir(modsDir, { recursive: true });
  await fs.copyFile(filePath, path.join(modsDir, `${project}.dll`));
} else {
  for (const entry of await fs.readdir(releasePath, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const buildDir = path.join(releasePath, entry.name);
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
}
await fs.writeFile(
  path.join(thunderstoreDir, "generated-README.md"),
  `${readmeContents}\n# Changelog\n\n${changelogContents}`
);
await fs.writeFile(
  configPath,
  config.replace(
    /(^s*versionNumber\s*=\s*")[^"\n]*("\s*$)/gm,
    `$1${newVersion}$2`
  )
);

execSync("tcli build", { stdio: "inherit", cwd: thunderstoreDir });
execSync("tcli publish", { stdio: "inherit", cwd: thunderstoreDir });
