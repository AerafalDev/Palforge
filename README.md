# Palforge

[![Build](https://github.com/AerafalDev/Palforge/actions/workflows/ci.yml/badge.svg)](https://github.com/AerafalDev/Palforge/actions/workflows/ci.yml)
[![Docs](https://github.com/AerafalDev/Palforge/actions/workflows/docs.yml/badge.svg)](https://aerafaldev.github.io/Palforge/)
[![NuGet](https://img.shields.io/nuget/v/Palforge.svg)](https://www.nuget.org/packages/Palforge)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A clean-room **.NET 10 modding runtime for Palworld dedicated servers**. Write plugins in plain C# — hook
native engine functions, add chat commands, and read and write live game state — all against a typed,
generated SDK, with no baked offsets. Runs in-process on the **Windows** dedicated server (`x64`).

Palforge ships as a `version.dll` proxy dropped next to the server executable. On start it boots a managed
runtime through the native .NET host ([HostFxrSharp](https://github.com/AerafalDev/HostFxrSharp)), derives the
core Unreal object layout at runtime, and loads your plugins — each into its own collectible
`AssemblyLoadContext` so they can be reloaded independently.

## How it works

```
PalServer-Win64-Shipping.exe
      │  loads
      ▼
version.dll  (Palforge.Proxy — Native-AOT shim; forwards the real version.dll exports)
      │  hostfxr
      ▼
Palforge  (managed runtime)
      ├─ derives the UObject/UClass/FProperty layout from the running process (no hardcoded offsets)
      ├─ starts the game-thread pump, logging and console
      └─ loads Palforge\Plugins\*  →  one collectible ALC per plugin
```

## Documentation

**[aerafaldev.github.io/Palforge](https://aerafaldev.github.io/Palforge/)** — guides for writing plugins,
hooking engine functions, chat commands, the generated SDK, and reading live game state.

## Packages

| Package | Version | Description |
| --- | --- | --- |
| [**Palforge**](src/Palforge/README.md) | [![NuGet](https://img.shields.io/nuget/v/Palforge.svg)](https://www.nuget.org/packages/Palforge) | The runtime plugins compile against: plugin host, typed hooks, command framework, and the generated Palworld SDK. |
| **Palforge.Templates** | [![NuGet](https://img.shields.io/nuget/v/Palforge.Templates.svg)](https://www.nuget.org/packages/Palforge.Templates) | `dotnet new` templates that scaffold a plugin, wired to reference Palforge and deploy on build. |

## Get started

Install the template package, then scaffold a plugin:

```sh
dotnet new install Palforge.Templates
dotnet new palforge-plugin -n MyPlugin
```

Point the plugin at your server so a build deploys it automatically:

```sh
dotnet new palforge-plugin -n MyPlugin \
  --DeployDirectory "C:\Program Files (x86)\Steam\steamapps\common\PalServer\Pal\Binaries\Win64"
```

`dotnet build` then copies the plugin into `…\Win64\Palforge\Plugins\MyPlugin`, ready for the server to load.

## Quick start

A plugin is a class that derives from `Plugin`. `OnStart` / `OnStop` bracket its lifetime, and `Log`,
`Unreal`, `Services` and `Scope` are available once the host has attached it:

```csharp
using Palforge.Plugins;
using Palforge.Plugins.Attributes;

namespace MyPlugin;

[Plugin("com.example.myplugin")]
public sealed class MyPlugin : Plugin
{
    protected override void OnStart() => Log.LogInformation("MyPlugin started");

    protected override void OnStop() => Log.LogInformation("MyPlugin stopped");
}
```

**Chat commands** are methods in a `ChatCommandModule`; the host discovers and binds them for you:

```csharp
using Palforge.Commands.Attributes;
using Palforge.Commands.Modules.Chat;

public sealed class FunCommands : ChatCommandModule
{
    [Command("hello")]
    [CommandDescription("Replies to the player who ran it.")]
    public void Hello() => Reply("Hello!");

    [Command("announce")]
    [CommandDescription("Sends a message to everyone on the server.")]
    public void Announce([CommandRemainder] string message) => Broadcast("{0}", message);
}
```

**Hooks** attach to a native engine function, named `Class:Function` or typed against a generated SDK class.
Mark the method `[PreHook]` to run before the original, `[PostHook]` to run after:

```csharp
using Palforge.Hooks.Attributes;

public sealed class TeleportWatch
{
    // "Class:Function" — or typed against the SDK: [PreHook<PalPlayerController>("TeleportToSafePointToServer")]
    [PreHook("PalPlayerController:TeleportToSafePointToServer")]
    public void OnTeleport() => /* runs before the engine handles the RPC */;
}
```

Need services or configuration? Add a `PluginConfiguration` next to your plugin and register them — they flow
in through the plugin's constructor via dependency injection:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Palforge.Plugins;

public sealed class MyPluginConfiguration : PluginConfiguration
{
    protected override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<MyService>();
    }
}
```

## Building from source

You need the **.NET 10 SDK** (the exact band is pinned in [`global.json`](global.json)).

```sh
git clone https://github.com/AerafalDev/Palforge.git
cd Palforge

dotnet build -c Release        # build the runtime, proxy and tests
dotnet test  -c Release        # run the test suite
```

## Contributing

Issues and pull requests are welcome — see [CONTRIBUTING](CONTRIBUTING.md). By participating you agree to the
[Code of Conduct](CODE_OF_CONDUCT.md). For security-sensitive reports, follow the [Security Policy](SECURITY.md).

## License

[MIT](LICENSE) © Aerafal

Palforge is an unofficial, fan-made project. It is not affiliated with or endorsed by Pocketpair, Inc.
"Palworld" is a trademark of Pocketpair, Inc.
