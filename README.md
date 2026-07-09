# sincript-cs

The C# (NET 8, NativeAOT) migration of **sincript / PerfTweaks.cmd** — a Windows 10/11
optimizer built on three principles: everything opt-in, everything backed up before it is
touched, everything reported honestly.

Ground truth for the migration is `sincript-csharp-refactor-plan.md`; every deviation is
logged in [`docs/DECISIONS.md`](docs/DECISIONS.md), and per-item parity is tracked in
[`docs/PARITY.md`](docs/PARITY.md). The original batch implementation is vendored read-only
under [`legacy/`](legacy/) and remains the working tool until each phase lands.

## Layout

```
src/Sincript/          the application (Core / UI now; Registry, Tweaks, Presets... follow in P1-P5)
docs/                  DECISIONS.md (living ledger) + PARITY.md (parity checklist)
legacy/                vendored batch reference: PerfTweaks.cmd, tests/, example preset
.github/workflows/     CI: build + NativeAOT publish + headless smoke on windows-latest
```

## Build

Compile on any OS (the csproj sets `EnableWindowsTargeting` for non-Windows dev hosts;
AOT/trim analyzers run on every build because `PublishAot=true`):

```
dotnet build src/Sincript/Sincript.csproj -c Release
```

Produce the shippable single-file exe — **on Windows** (NativeAOT cannot cross-compile
to win-x64 from Linux/macOS; CI does this on every push):

```
dotnet publish src/Sincript/Sincript.csproj -c Release -r win-x64
# → src/Sincript/bin/Release/net8.0-windows/win-x64/publish/Sincript.exe
```

## Status

| Phase | Content | State |
|---|---|---|
| P0 | Skeleton: elevation, session, logging, hardware profile (D16), full navigable menu tree | **done** |
| P1 | Registry core + backups (SafeSet, .reg writer, JSON capture) + test project | next |
| P2 | Tweak catalog + cleanup/network/power + external command runner | — |
| P3 | Presets + JSON restore (cross-era interop gate) | — |
| P4 | Apps & files | — |
| P5 | Advanced + backups/status screens | — |
| P6 | Parity audit on a test VM, README/tests rewrite | — |
