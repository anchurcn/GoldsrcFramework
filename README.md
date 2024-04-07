# GoldsrcFramework

GoldsrcFramework is a code library that rewrites HLSDK using C# (.NET 8). It supports developing GoldSrc mods with C#, providing a better development experience and higher development efficiency for GoldSrc mod developers.

## Features

- Support developing GoldSrc mods with C#
- Use .NET 8, providing better language features and performance
- Hot-reload support (edit and continue) for faster iteration
- Better readability and maintainability, using modern C# syntax structure and clear code style
- More basic libraries, including common data structures, algorithms, network programming, commonly used serialization tools, and other functions
- More compatible with Visual Studio, allowing the use of Visual Studio's powerful debugger and development environment

## Future Planning

GoldsrcFramework's future planning includes:
- Improving code generation tools to generate C# definitions from HLSDK
- Providing better documentation and tutorials to help developers get started and learn faster
- Improving the code library's scalability and customizability, making it easier for developers to modify and extend according to their needs
- Integrating more toolchains (such as stride3d)

## Usage

### Demo build steps:

1. Open the `dotnet_hosting.props` file and modify the value of the `AppHostDir` macro to the directory where your local .NET Core AppHost package is located.
2. Right-click on the `GoldsrcFramework` project in the Visual Studio Solution Explorer and click on "Build".

### Demo debug and run steps:

1. Follow the instructions in the Demo build steps.
2. Set the path to the game application (`hl.exe`).