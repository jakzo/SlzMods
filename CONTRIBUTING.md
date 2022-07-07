Contributions are welcome! Feel free to open a PR for small fixes or open an issue if you want to discuss something or make a bigger change.

## Setup

- Install [Mono](https://www.mono-project.com/download/stable/)
- Use [VSCode](https://code.visualstudio.com/download)
- Install the [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) (it should prompt you after opening the project in VSCode)

## Modifying replay Flatbuffer spec

Make sure the Flatbuffers CLI is installed. Easiest way is using a package manager like [Chocolatey](https://chocolatey.org/) or [Homebrew](https://brew.sh/).

```sh
# Windows (using Chocolatey)
choco install flatbuffers

# Mac
brew install flatbuffers

# Linux (Ubuntu)
sudo apt install flatbuffers-compiler
```

After making changes to `Bwr.fbs` run this to regenerate the `Bwr/*.cs` files:

```sh
cd src/Replay && flatc --csharp Bwr.fbs
```

## Build

```sh
msbuild /property:Configuration=Release
```

## Branches

The `main` branch contains the latest stable changes (ie. the code that is built and uploaded to Thunderstore). I do my own development on the `develop` branch and every push to this branch creates a dev build (you can see them under releases).

In most cases you want to fork from and merge PRs to the `main` branch. However if you're curious you can look at what's happening in the `develop` branch.
