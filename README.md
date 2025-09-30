# GoldsrcFramework

GoldsrcFramework is a code library that rewrites HLSDK using C# (.NET 8). It supports developing GoldSrc mods with C#, providing a better development experience and higher development efficiency for GoldSrc mod developers.

## WIP
Currently only runable on xash, and goldsrc crashes.

## Features

- Support developing GoldSrc mods with C#
- Use .NET 8, providing better language features and performance
- Hot-reload support (edit and continue) for faster iteration
- Better readability and maintainability, using modern C# syntax structure and clear code style
- More basic libraries, including common data structures, algorithms, network programming, commonly used serialization tools, and other functions
- More compatible with Visual Studio, allowing the use of Visual Studio's powerful debugger and development environment

## Documentation

📚 **Comprehensive documentation is now available!**

- **[Architecture Overview](docs/Architecture.md)** - Detailed technical architecture documentation
- **[Architecture Diagrams](docs/Architecture-Diagrams.md)** - Visual diagrams with Mermaid
- **[Quick Reference](docs/Quick-Reference.md)** - API reference and code examples
- **[Documentation Index](docs/README.md)** - Complete documentation guide

### Quick Links

- **New to the project?** Start with [Architecture Overview](docs/Architecture.md)
- **Want to see diagrams?** Check out [Architecture Diagrams](docs/Architecture-Diagrams.md)
- **Ready to code?** Use [Quick Reference](docs/Quick-Reference.md)

## Future Planning

GoldsrcFramework's future planning includes:
- Improving code generation tools to generate C# definitions from HLSDK
- Providing better documentation and tutorials to help developers get started and learn faster
- Improving the code library's scalability and customizability, making it easier for developers to modify and extend according to their needs
- Integrating more toolchains (such as stride3d)

## Usage

### Prerequisites
VS2022 + .NET 9.0 SDK + .NET 8.0 SDK + VS2022 C++ workloads with default components
### Demo build steps:

Just click re-build for the GoldsrcFramework.Demo project.
Must re-build GoldsrcFramework.Demo project while anything changes.

### Demo debug and run steps:

1. Follow the instructions in the Demo build steps.
1. Create 'gsfdemo' mod folder (currently must be the name)
1. setup liblist.gam
```
// Valve Game Info file
//  These are key/value pairs.  Certain mods will use different settings.
//
game "Half-Life GSF"
startmap "c0a0"
trainmap "t0a0"
mpentity "info_player_deathmatch"
gamedll "cl_dlls\client.dll"
gamedll_linux "dlls/hl.so"
gamedll_osx "dlls/hl.dylib"
secure "1"
type "singleplayer_only"
animated_title "1"
hd_background "1"
```
1. Copy all files in dlls to cl_dlls
1. rename client.dll to libclient.dll, hl.dll to libserver.dll
1. Copy all files in Demo project OutDir(usually bin/Debug/net8.0/) to gsfdemo/cl_dlls
1. The demo can be run now
1. For vs debug run, config the demo launcher project to launch the game