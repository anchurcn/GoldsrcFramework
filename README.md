# GoldsrcFramework

GoldsrcFramework is a code library that rewrites HLSDK using C# . It supports developing GoldSrc mods with C#, providing a better development experience and higher development efficiency for GoldSrc mod developers.

## WIP



## Features

- Support developing GoldSrc mods with C#
- Use modern .NET, providing better language features and performance
- Hot-reload support (edit and continue) for faster iteration
- Better readability and maintainability, using modern C# syntax structure and clear code style
- More basic libraries, including common data structures, algorithms, network programming, commonly used serialization tools, and other functions
- More compatible with Visual Studio, allowing the use of Visual Studio's powerful debugger and development environment

## Documentation


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

(WIP) dotnet new goldsrcmod and dotnet run

## Building from Source

### Prerequisites
Git
VS2026 + .NET 10.0 SDK + VS2026 C++ workloads with default components

### Demo debug and run steps:

1. Setup variables in Directory.Build.props.user to your Half Life installation path
1. Create 'gsfdemo' mod folder (currently must be the name)
1. setup liblist.gam
```
// Valve Game Info file
//  These are key/value pairs.  Certain mods will use different settings.
//
game "Half-Life GoldsrcFramework"
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
1. Copy all files in /valve/dlls and /valve/cl_dlls to /gsfdemo
1. rename client.dll to libclient.dll, hl.dll to libserver.dll
1. The demo can be run now
1. Press F5 in Visual Studio

Half Life 1 SDK LICENSE
======================

Half Life 1 SDK Copyright© Valve Corp.  

[LICENSE](LICENSE.txt)