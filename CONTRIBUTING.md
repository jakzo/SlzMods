## Dev instructions

### Setup

- Install [Mono](https://www.mono-project.com/download/stable/)
- Use [VSCode](https://code.visualstudio.com/download)
- Install the [C# extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) (it should prompt you after opening the project in VSCode)

### Modifying Replay Flatbuffer Spec

Make sure the Flatbuffers CLI is installed. Easiest way is using a package manager like [Chocolatey](https://chocolatey.org/) or [Homebrew](https://brew.sh/).

```sh
choco install flatbuffers
brew install flatbuffers
sudo apt install flatbuffers-compiler
```

After making changes to `Bwr.fbs` run this to regenerate the `Bwr/*.cs` files:

```sh
cd src/Replay && flatc --csharp Bwr.fbs
```

### Build

```sh
msbuild /property:Configuration=Release
```
