// @ts-check
import fs from "fs/promises";
import { spawn, spawnSync } from "child_process";
import path from "path";
import { fileURLToPath } from "url";

const rootDir = fileURLToPath(new URL("..", import.meta.url));

const fileExists = (/** @type string */ filePath) =>
  fs.stat(filePath).then(
    () => true,
    () => false
  );

const buildUnix = async () => {
  /** @type {{ name: string, path: string }[]} */
  const projectsMono = [];
  /** @type {{ name: string, path: string }[]} */
  const projectsDotnet = [];

  const projectsDir = path.join(rootDir, "projects");
  for (const entry of await fs.readdir(projectsDir, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const projectType = entry.name;
    const projectTypeDir = path.join(projectsDir, projectType);
    for (const entry of await fs.readdir(projectTypeDir, {
      withFileTypes: true,
    })) {
      if (!entry.isDirectory()) continue;
      const projectName = entry.name;
      const projectDir = path.join(projectTypeDir, projectName);

      for (const { projects, csproj } of [
        { projects: projectsMono, csproj: `${projectName}.csproj` },
        { projects: projectsDotnet, csproj: "Project.csproj" },
      ]) {
        const csprojPath = path.join(projectDir, csproj);
        if (await fileExists(csprojPath)) {
          projects.push({
            name: `${projectType}${projectName}`,
            path: path.relative(rootDir, csprojPath),
          });
        }
      }
    }
  }

  // No dependencies in mods
  // const result = spawnSync("nuget", ["restore", "SlzMods.sln"], {
  //   stdio: "inherit",
  //   cwd: rootDir,
  // });
  // if (result.error || result.status !== 0) {
  //   process.exit(1);
  // }

  let failed = false;

  for (const { name, projects, command, args } of [
    { name: "mono", projects: projectsMono, command: "msbuild", args: ["-m"] },
    {
      name: "dotnet",
      projects: projectsDotnet,
      command: "dotnet",
      args: ["build"],
    },
  ]) {
    // One for all projects seems to work
    const projectGuid = "EAE1410F-B5CF-47D6-8764-2FCAEE822C9A";
    const slnPath = path.join(rootDir, `generated-${name}-projects.sln`);
    await fs.writeFile(
      slnPath,
      `
Microsoft Visual Studio Solution File, Format Version 12.00
${projects
  .map(
    (project) =>
      `Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "${project.name}", "${project.path}", "{${projectGuid}}"\nEndProject`
  )
  .join("\n")}
Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug|Any CPU = Debug|Any CPU
    Release|Any CPU = Release|Any CPU
  EndGlobalSection
  GlobalSection(ProjectConfigurationPlatforms) = postSolution
    {${projectGuid}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
    {${projectGuid}}.Debug|Any CPU.Build.0 = Debug|Any CPU
    {${projectGuid}}.Release|Any CPU.ActiveCfg = Debug|Any CPU
    {${projectGuid}}.Release|Any CPU.Build.0 = Debug|Any CPU
  EndGlobalSection
EndGlobal
  `
    );

    console.log("Running build for:", name);
    const result = spawnSync(command, [...args, slnPath], {
      stdio: "inherit",
      cwd: rootDir,
    });
    if (result.error || result.status !== 0) failed = true;
  }

  if (failed) {
    console.log("Build failed");
    process.exit(1);
  }

  console.log("Build succeeded");
};

const game = process.argv[2]?.toLowerCase();
const startAfterBuild = !!process.argv[3];

const directoryBuildContents = await fs.readFile(
  path.join(rootDir, "projects", "Bonelab", "Directory.Build.props"),
  "utf-8"
);
const versions = {
  patch: directoryBuildContents.match(/<DefaultPatch>([^<]+)</)?.[1] ?? "3",
  melonLoader:
    directoryBuildContents.match(/<DefaultMelonLoader>([^<]+)</)?.[1] ?? "5",
};

const bonelabDir = `Bonelab_P${versions.patch}_ML${versions.melonLoader}`;
const bonelabExe = "BONELAB_Oculus_Windows64.exe";

const boneworksExe = "BONEWORKS.exe";

switch (process.platform) {
  case "win32": {
    if (startAfterBuild) {
      spawnSync("taskkill", ["/IM", bonelabExe, "/F"], {
        stdio: "inherit",
      });
    }

    const result = spawnSync("dotnet", ["build"], {
      stdio: "inherit",
      cwd: rootDir,
    });
    if (result.error || result.status !== 0) process.exit(result.status || 1);

    if (startAfterBuild) {
      console.log(`Starting ${game}...`);
      const gamePath =
        game === "bonelab"
          ? `C:\\Program Files\\Oculus\\Software\\Software\\${bonelabDir}`
          : "TODO";
      if (gamePath) {
        spawn(path.join(gamePath, bonelabExe), {
          stdio: "inherit",
          detached: true,
          cwd: gamePath,
        }).unref();
      }
    }

    break;
  }

  case "linux":
  case "darwin": {
    const vmName = "Windows 11";
    if (startAfterBuild) {
      const gameExe = game === "bonelab" ? bonelabExe : boneworksExe;
      spawnSync("prlctl", ["resume", vmName], { stdio: "inherit" });
      spawnSync("prlctl", ["exec", vmName, "taskkill", "/IM", gameExe, "/F"], {
        stdio: "inherit",
      });
    }

    await buildUnix();

    if (startAfterBuild) {
      console.log(`Starting ${game}...`);
      // TODO: Get from build output
      const gamePath =
        game === "bonelab"
          ? `${process.env.HOME}/Downloads/${bonelabDir}/${bonelabExe}`
          : `/Volumes/[C] ${vmName}.hidden/Program Files (x86)/Steam/steamapps/common/BONEWORKS/BONEWORKS/${boneworksExe}`;
      spawnSync("open", [gamePath], {
        stdio: "inherit",
        cwd: path.dirname(gamePath),
      });
    }

    break;
  }

  default: {
    throw new Error(`Unknown OS: ${process.platform}`);
  }
}
