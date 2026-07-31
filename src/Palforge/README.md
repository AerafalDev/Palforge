# Palforge

[![NuGet](https://img.shields.io/nuget/v/Palforge.svg)](https://www.nuget.org/packages/Palforge)
[![Downloads](https://img.shields.io/nuget/dt/Palforge.svg)](https://www.nuget.org/packages/Palforge)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/AerafalDev/Palforge/blob/main/LICENSE)

A clean-room **.NET 10 modding runtime for Palworld dedicated servers**. Write plugins in plain C# — hook
native engine functions, add chat commands, and read and write live game state — against a typed, generated
SDK with no baked offsets. Runs in-process on the **Windows** dedicated server (`x64`).

This package is what a plugin compiles against: the `Plugin` host model, attribute-based hooks, the command
framework, and the generated Palworld SDK. The runtime itself is delivered to the server by the `version.dll`
proxy; plugins reference this package compile-only.

The fastest way to start is the template:

```sh
dotnet new install Palforge.Templates
dotnet new palforge-plugin -n MyPlugin
```

```csharp
using Palforge.Plugins;
using Palforge.Plugins.Attributes;

[Plugin("com.example.myplugin")]
public sealed class MyPlugin : Plugin
{
    protected override void OnStart() => Log.LogInformation("MyPlugin started");
}
```

## Documentation

**[Guides & API →](https://aerafaldev.github.io/Palforge/)**

---

Part of the [Palforge](https://github.com/AerafalDev/Palforge) project ·
[MIT](https://github.com/AerafalDev/Palforge/blob/main/LICENSE) © Aerafal
