# Security Policy

## Supported versions

Palforge is developed on the `main` branch. Security fixes are applied to `main` and shipped in the next
release; only the **latest published version** is supported.

| Version | Supported |
| --- | --- |
| Latest release | ✅ |
| Older releases | ❌ |

## Reporting a vulnerability

**Please do not report security issues in public GitHub issues or pull requests.**

Palforge runs **in-process inside the Palworld dedicated server**. It ships as a `version.dll` proxy that boots
a .NET runtime through `hostfxr`, hooks native engine functions, reads and writes the game's process memory,
and loads third-party plugin assemblies into collectible `AssemblyLoadContext`s. The security surface that
matters most is therefore:

- **Native library / host resolution** — a path that could load an unexpected `version.dll`, `hostfxr`, or
  runtime, or mishandling of the proxy's export forwarding.
- **Plugin loading** — how plugin assemblies are discovered and loaded from `Palforge\Plugins`. A plugin is
  fully trusted code running in the server process; the concern is the loader itself (unexpected load paths,
  metadata parsing, ALC lifetime), not the trust granted to a plugin the operator installed.
- **Memory access** — out-of-bounds or type-confused reads/writes in the memory, layout-derivation, or SDK
  marshalling layers that a crafted game state could trigger.
- **Hook binding** — resolving and patching engine functions by name/signature in a way that could corrupt
  state or be redirected.

Report privately through either channel:

- **GitHub Security Advisories** — open the repository's **Security → Report a vulnerability** tab to start a
  private advisory (preferred).
- **Email** — <aerafal.github@gmail.com>.

Please include:

- a description of the issue and its impact,
- a minimal repro (server version, Palforge version, and the smallest plugin/inputs that trigger it),
- the version (or commit) you observed it on.

## What to expect

- We aim to acknowledge a report within a few days.
- We'll confirm the issue, keep you updated as we work on a fix, and credit you in the release notes unless you
  prefer to stay anonymous.
- Once a fix is released, the advisory is published.

Thank you for helping keep Palforge and the servers that run it safe.
