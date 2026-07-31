# Contributing to Palforge

Thanks for taking the time to contribute! Palforge is a clean-room .NET modding runtime for Palworld dedicated
servers. Issues, bug reports and pull requests are all welcome.

By participating you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

## Getting started

You need the **.NET 10 SDK**. The exact band is pinned in [`global.json`](global.json), so the right SDK is
selected automatically.

```sh
git clone https://github.com/AerafalDev/Palforge.git
cd Palforge

dotnet build -c Release        # build the runtime, proxy and tests
dotnet test  -c Release        # run the test suite
```

The repository has three parts:

- **`src/Palforge`** — the managed runtime (plugin host, hooks, commands, memory/layout, the generated SDK).
  This is the package plugins compile against.
- **`src/Palforge.Proxy`** — the Native-AOT `version.dll` shim that boots the runtime and forwards the real
  `version.dll` exports.
- **`templates/`** — the `dotnet new` plugin template and its package.

## Runs inside the game

Palforge runs **in-process inside the Windows dedicated server**, so the constraints are unusual:

- **Windows / x64 only.** The runtime targets `net10.0-windows` and calls Win32 through source-generated
  P/Invokes (CsWin32). Platform code and native signatures must stay `x64`-correct.
- **The game thread is sacred.** Reads are generally safe from anywhere, but **calling engine functions,
  mutating objects, or freeing native memory off the game thread crashes the server.** Route mutation through
  the game-thread pump; never allocate/free Unreal objects from a background thread.
- **No baked offsets.** Object layout is *derived* from the running process at startup. When you touch the
  layout or SDK layers, prefer derivation and validation over hardcoded numbers, and keep the provenance of
  every offset explicit.

## Coding conventions

Style is enforced by [`.editorconfig`](.editorconfig); please don't fight it. The points that matter most here:

- **C# style** — file-scoped namespaces, 4-space indentation, Allman braces, `var` when the type is apparent,
  `_camelCase` private fields.
- **Member order** — constants and fields, then properties, then constructors, then methods.
- **No primary constructors** — declare constructors explicitly.
- **XML documentation** — public types and members carry `///` docs. Explain the *engine* behaviour being
  wrapped, not just the C# signature.
- **Interop hygiene** — all native access goes through source-generated `[LibraryImport]` / CsWin32 P/Invokes
  (no reflection-marshalled delegates), and native handles are owned by `SafeHandle`s.
- **Warnings are errors** — the build runs with `TreatWarningsAsErrors`, so a green build means zero warnings.

Files are **UTF-8 (no BOM) with CRLF** line endings, normalized by `.gitattributes`.

## Tests

New behaviour ships with a test. The suite lives in `tests/Palforge.Tests` (xUnit v3, with
[CsCheck](https://github.com/AnthonyLloyd/CsCheck) for property-based tests). Prefer tests that are
deterministic and don't require a running game — for example, layout-derivation logic against a captured
snapshot, argument parsing, command binding, or memory-region math.

Run `dotnet test -c Release` before opening a pull request. When a change affects behaviour that only shows up
in-game, say so in the PR and describe how you verified it against a live server.

## Working on a plugin

To exercise the plugin surface end to end, scaffold one from the template and point it at a local server:

```sh
dotnet new install ./templates/nupkg/Palforge.Templates.1.0.0.nupkg --force
dotnet new palforge-plugin -n Scratch --DeployDirectory "…\PalServer\Pal\Binaries\Win64"
dotnet build ./Scratch     # deploys to …\Win64\Palforge\Plugins\Scratch
```

## Pull requests

- Branch off `main`; keep each change small and self-contained.
- Write clear, present-tense commit messages — one logical change per commit.
- Make sure `dotnet build -c Release` and `dotnet test -c Release` are green.
- Describe *what* changed and *why*. When you touch interop or layout, cite the engine class/function or the
  evidence the offset was derived from.

## Reporting bugs

Open an issue with the server version, the Palforge version, what you expected and what happened, and a minimal
repro if you can share one. For security-sensitive reports, follow the [Security Policy](SECURITY.md) instead of
opening a public issue.
