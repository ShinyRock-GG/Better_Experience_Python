# Third-party notices — BetterExperience release packages

## Full tier only (pydlr/ — Python runtime for Better_Story)

- **IronPython 3.4** (IronPython.dll, IronPython.Modules.dll) — Apache License 2.0.
  Copyright (c) .NET Foundation and Contributors. https://ironpython.net
- **Dynamic Language Runtime** (Microsoft.Dynamic.dll, Microsoft.Scripting.dll) —
  Apache License 2.0. Copyright (c) .NET Foundation and Contributors.
  https://github.com/IronLanguages/dlr
- **.NET compatibility shims** (System.Buffers, System.Memory,
  System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe) — MIT License.
  Copyright (c) .NET Foundation and Contributors.

Full license texts: `LICENSE-Apache-2.0.txt` and `LICENSE-MIT-dotnet.txt` in this folder
(make-release.ps1 fetches them on first run and refuses to package without them).

## Standard tier prerequisite (NOT bundled)

- **Monkey** — separate mod by its own author; redistribution terms not ours to grant.
  Better_Scene.dll hard-requires it. Released packages DECLARE it as a prerequisite;
  do not bundle Monkey.dll without the author's permission.
