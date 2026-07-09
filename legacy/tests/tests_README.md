# Sincript tests

`PerfTweaks.cmd` is a single large, interactive, system-mutating batch script, so it
can't be meaningfully unit-tested by *running* it. Instead `Run-Tests.ps1` is a
dependency-free **static-analysis** harness (no Pester; runs on stock Windows
PowerShell 5.1) that locks in **28** invariants most prone to silent regression.

## Run

Run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\Run-Tests.ps1
```

Exit code `0` = all 28 passed, `1` = at least one failure. One `[PASS]`/`[FAIL]` line is
printed per test, with the offending detail on failure.

## What it checks

Tests 1–12 cover structural sanity and the first wave of reliability fixes. Tests 13–28
guard honest reporting, elevation behavior, backup/restore integrity, and preset safety —
the areas where a silent regression would look like success to the user.

| # | Test | Guards |
|---|------|--------|
| 1 | Every `goto X` / `call :X` resolves to a real `:X` label (comment lines ignored, `:eof` excluded) | broken menu navigation / dead jumps |
| 2 | `boot.config` has no duplicate Unity keys | fix #1 (duplicate `wait-for-native-debugger`) |
| 3 | Every key in `example.preset` is one the validator (`:PresetCheckLine`) actually recognizes — the recognized set is parsed out of the script itself, so it tracks the real validator | README / example / validator drift |
| 4 | `:CreateRegBackup` checks `errorlevel` **and** `if not exist` and has an `[ERROR]` branch before printing `[OK]` | fix #2 (claiming a backup succeeded when it didn't) |
| 5 | Both `Win32PrioritySeparation` writes in `:Performance` are gated by the same prompt variable (one mutually-exclusive choice) | fix #3 (two independent prompts could apply 42 then reset to 2 in one pass) |
| 6 | `:DoCleanupCore` does not `del` the Prefetch folder (checks for a real delete, not the explanatory `rem`) | fix #4 (placebo cleanup the README disavows) |
| 7 | `:ApplyDns` / `:DnsAuto` capture the child exit code and report via `:DnsResult` (which has `[OK]` + `[ERROR]` branches) instead of echoing an unconditional `[OK]` | DNS fix (cousin of #2 — swallowed per-adapter failures read as success) |
| 8 | `:InstallAsarInto` tracks which OpenAsar backup landed (`_bakloc`), keeps the Documents fallback (`BACKUP_DIR`), and checks the in-folder `.bak` exists before claiming it | OpenAsar backup fix (in-folder backup silently blocked by AV/Controlled Folder Access, then misreported) |
| 9 | cmd block-parse simulation: no unescaped `)` inside a `( )` block (quotes protect, `^` escapes, `(` opens only at a command position; after a close only `else` / `&` / `\|` / `)` / `>` / `<` / end-of-line are legal) | hosts restore/reset crash — a bare `)` in echo text ended the block early and "was unexpected at this time" aborted the whole batch |
| 10 | `:DoPowerCore` duplicates Ultimate onto its **canonical GUID** (`duplicatescheme` has a destination GUID) | power fix (random-GUID Ultimate clones piling up every run while High silently activated) |
| 11 | Both OpenAsar download paths gate on the child exit code (`if errorlevel 1 del`) before the existence check | download fix (a truncated `.asar` from a mid-transfer failure could be installed into Discord) |
| 12 | `:StartupWorker` writes the prior-state `.reg` backup **before** the flip and writes via literal-safe `Registry::SetValue` | startup-manager reversibility (every flip stays undoable; wildcard-looking names cannot misfire) |
| 13 | `:_sraApply` / `:_srdApply` print inline `[FAIL]` on a failed write and propagate the result into `_FAILS` across `endlocal` | Critical #1 — failed registry writes invisible to the caller; unconditional `[OK]` |
| 14 | `:Summary` consults `%_FAILS%` and has both `echo [OK]` and `echo [WARN]` branches | Critical #1 — action final line cannot distinguish success from partial failure |
| 15 | Spot-checked registry routines reset `_FAILS` and call `:Summary` (never a raw `echo [OK]`); global count sanity (`>=13` `:Summary` sites and `_FAILS` resets) | Critical #1 — stale failure count or bypassed `:Summary` on gated actions |
| 16 | `:PresetCheckLine` guards the trailing-space strip with `if defined _v` (no unguarded `%_v:~..%` on an empty value) | Critical #2 — `key=` in a `.preset` file aborts the whole script |
| 17 | Admin probe sets `_ELEV=1`; `:AdminWarn` sets `_ELEV=0`, no silent "Continuing anyway"; `:Summary` tailors `[WARN]` to `%_ELEV%` | limited-mode honesty when UAC is declined |
| 18 | `:ApplyHosts` tracks `_hbak` and aborts before copying the bundled `hosts` if no backup landed | data-loss window — overwriting system `hosts` with no undo |
| 19 | `:RestorePresetJson_ask` captures child exit code, branches on `_prrc`, has `[WARN]` and `[ERROR]` branches | preset JSON restore always printing `[OK]` |
| 20 | `:InstallAsarInto` version-sorts Discord `app-*` folders (`Sort-Object` / `[version]`), not ASCII `dir /o-n` | wrong Discord build after a version digit rollover |
| 21 | `:BackupValueLine` / `:_bvjSz` escape `"` → `\"` in REG_SZ data (not strip); both guard empty values with `if defined _rd` | corrupt `.reg` / JSON undo files; silent data loss on restore |
| 22 | `:SafeRegAdd` / `:SafeRegDelete` backup filenames use `%RANDOM%%RANDOM%.reg` | birthday collision — two values under one key overwrite each other's undo in one pass |
| 23 | `:RestorePresetJson_ask` restores REG_SZ via `Set-ItemProperty` + hive short-name → PSDrive conversion | `reg add` from PowerShell 5.1 mangles embedded quotes |
| 24 | `:Run` increments `_FAILS` only when `_RUNTRACK` is set **and** `%_ELEV%` is `0`; `:Summary` clears `_RUNTRACK` | crying wolf on benign elevated nonzero exits; tracking leaking into cleanup |
| 25 | `:Power`, `:NetworkApply`, `:NetReset`, `:BcdTimers`, `:BcdRevert`, `:Privacy`, `:GpuNvidia` set `_RUNTRACK=1` and call `:Summary` | service/boot/network actions printing `[OK]` when not elevated |
| 26 | `:SteamLight` stages the Steam path in `PT_SLDIR` and reads `$env:PT_SLDIR` in PowerShell | apostrophe in the install path (e.g. `O'Brien`) breaks shortcut creation |
| 27 | `:PresetBegin` resets `_FAILS`; built-in presets call `:Summary` (no unconditional `echo [OK] … preset`) | preset final line carrying a stale failure count or blind success |
| 28 | `:SfcDism`, `:WUReset`, `:CompactWinSxS`, `:MemCompress` gate final status on `%_ELEV%` with a `[WARN]` branch | repair actions printing `[OK]` when not elevated |

Each detector has been verified to fail on a deliberately broken copy, so a green run
is meaningful rather than vacuous.

## Batches (for context)

The numbered comments in `Run-Tests.ps1` group the later tests by when they landed:

| Batch | Tests | Theme |
|-------|-------|-------|
| (early) | 1–12 | Structure, bundled files, first reliability wave |
| Critical #1 | 13–15 | Honest registry reporting (`_FAILS`, `:Summary`, inline `[FAIL]`) |
| Critical #2 | 16 | Preset parser must not crash on `key=` |
| Batch 2 | 17–20 | Elevation honesty, hosts backup guard, preset-restore reporting, OpenAsar version pick |
| Batch 3 | 21–23 | Backup undo integrity (quotes, collisions, quote-safe preset restore) |
| Batch 4 | 24–26 | Honest `:Run` / `_RUNTRACK` reporting; SteamLight path safety |
| Batch 5 | 27–28 | Preset `:Summary` honesty; repair-action elevation gating |

## Adding a test

Add another `Invoke-Test '<name>' { ... }` block in `Run-Tests.ps1`. Inside, use
`Assert-True <condition> '<message>'`, and the `Read-Lines` / `Get-RoutineBody`
helpers to pull a specific `:label` routine body out of the script. Prefer parsing
facts out of `PerfTweaks.cmd` (as test #3 does) over hard-coding expected values, so
the test tracks the script instead of drifting from it.

`Get-RoutineBody` returns a routine's *full* body even when it spans internal
`goto`-only sub-labels (`:_sraDoWrite`, `:_netNagDone`, …): it keeps those inside
the parent and stops only at the next real routine — a non-`_` label, or a `:_`
label that is itself a `call` target (`:_mbAddFull`, `:_mbPrune`, `:_suShow`). So
slicing by `:Label` gives the whole body; no need to find the boundary by hand.
(Test #25 scans inline — that was a workaround for the older slicer and is no
longer necessary for new tests.)

Update this table when you add a test so `README_sincript.md` and the harness stay
aligned.

