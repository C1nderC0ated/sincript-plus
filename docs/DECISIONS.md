# DECISIONS.md — living decision ledger

Companion to `sincript-csharp-refactor-plan.md` §7. Every deviation from the batch reference
or from the plan gets an entry here; silent judgment calls are not allowed (plan §13).

## Sign-off record — 2026-07-10 (author)

The plan (§7) shipped six Bucket-B items requiring explicit sign-off. All six were
**APPROVED by the author on 2026-07-10**, plus one scoped feature un-deferral (D16).

| # | Decision | Bucket | Status | Notes |
|---|---|---|---|---|
| D1 | asInvoker manifest + self-relaunch (limited mode survives) | — | Per plan | Implemented in P0 (`app.manifest`, `Elevation.cs`) |
| D2 | Back up **all** registry value kinds restorably (UTF-16LE `.reg`, richer JSON `oldtype`s) | B | **APPROVED 2026-07-10** | Lands in P1 (`RegFileWriter`, `PriorValue`) + P3 (JSON writer/restorer branches). Batch-era restorers skip the new `oldtype`s gracefully (plan §6.4) |
| D3 | Extend the idempotent skip from DWORD-only to exact-match on **all** kinds | B | **APPROVED 2026-07-10** | Lands in P1 (`RegistryService.AlreadyAtTarget`). Fixes the latent SZ backup-burial gap; fulfills the batch comment's stated intent |
| D4 | Stream live stdout for repair-class long-runners (DISM, SFC, WinSxS, WU reset, netsh reset); keep suppression elsewhere; log stays outcome-only (I13) | B | **APPROVED 2026-07-10** | Lands in P2 (`ExternalCommand` gains a `StreamOutput` mode) |
| D5 | SteamLight shortcut: hidden PS child now, `IShellLinkW` ComWrappers later | — | Per plan | P4 |
| D6 | `Console.OutputEncoding = UTF8`; real Unicode in display and log (Cyrillic startup names, non-ASCII paths) | B | **APPROVED 2026-07-10** | Implemented in P0 (`Program.cs`, `Logger.cs`). The "Names are shown ASCII-only" caption is removed when the startup manager ports in P4 |
| D7 | Surviving PowerShell children (Appx, MMAgent, restore point, DNS-set phase-1), all `CreateNoWindow` | — | Per plan | P2/P4/P5 |
| D8 | powercfg / bcdedit / schtasks / netsh / sc / reg export-import stay subprocesses | — | Per plan | P2/P5 |
| D9 | Temp cleanup does **not** recurse into reparse points / junctions | B | **APPROVED 2026-07-10** | Lands in P2 (`CleanupActions`); a safety narrowing of `del /f /s /q` semantics |
| D10 | SteamLight launcher remains a user-editable `.bat`, content verbatim | — | Per plan | P4 |
| D11 | Declined-UAC parent offers limited mode instead of vanishing (catch `Win32Exception` 1223) | B | **APPROVED 2026-07-10** | Implemented in P0 (`Program.cs` / `Elevation.TryRelaunchElevated`) |
| D12 | Uniq tokens: `%RANDOM%(%RANDOM%)` → `yyyyMMdd_HHmmss` + 8-hex GUID slice; prefixes unchanged | — | Per plan | Implemented in P0 for the log; P1 for backups |
| D13 | Menu input stays line-based; the three I9 prompt-default classes are the only primitives | — | Per plan | Implemented in P0 (`Prompts.cs`) |
| D14 | Console cosmetics preserved (magenta theme, SIN logo, best-effort 100×36) | — | Per plan | Implemented in P0 (`ConsoleUi.cs`) |
| D15 | Data-driven tweak catalog | — | Per plan | P2 |

## D16 — Laptop-aware tweak advisories *(new; author-requested un-deferral, APPROVED 2026-07-10)*

**What.** A small hardware-detection feature that explicitly marks tweaks that are typically
harmful to apply on laptops, forward-compatible with the plan-§11 full detection engine.

**Detection (implemented in P0, `Core/Hardware.cs`).** `PowerDeterminePlatformRoleEx`
(powrprof.dll) maps `Mobile`/`Slate` → Laptop, the desktop/workstation/server roles → Desktop;
`Unspecified` falls back to battery presence via `GetSystemPowerStatus` (no-system-battery →
Desktop). Pure P/Invoke, AOT-safe, never throws; an undetectable machine is `Unknown` and no
advisory ever fires. No WMI (AOT posture, plan §12).

**Model (implemented in P0).**
- `HardwareProfile` record — the single object consumers read. The future engine *replaces
  `HardwareDetector` and widens this record* (CPU, RAM, GPU detail, storage, AC/battery state)
  without touching a single consumer. That is the forward-compatibility contract.
- `TweakAdvisory` `[Flags]` enum — `HarmfulOnLaptop`, plus `HarmfulOnDesktop` (the batch's own
  LargeSystemCache wording: "can help some laptops, can hurt desktops"). New engine conditions
  (`HarmfulOnLowRam`, …) slot in as new flags.
- `Advisories.WarnIfApplicable(session, flags)` — the one rendering choke point.

**Behavioral rules (binding).** Advisories are **warning-only**: shown after an action's banner
and before its confirm prompt; they never block, never change a prompt default, never alter
what a preset applies. The opt-in philosophy stays intact — this is information, not gatekeeping
(plan §0 rule 1, I9 untouched).

**P2 wiring list** (where `TweakAdvisory` attaches when the catalog lands):
- Power core (high-performance/Ultimate plan + all sleep timeouts to never) → `HarmfulOnLaptop`
- Hibernate-off optional knob → `HarmfulOnLaptop` (removes hibernate/fast-startup battery protection)
- BCD timer set (`disabledynamictick yes`) → `HarmfulOnLaptop` (dynamic tick is a battery feature)
- Timer-resolution autostart → `HarmfulOnLaptop` (a held high-resolution timer draws power)
- LargeSystemCache opt-in → `HarmfulOnDesktop`
- Heavy preset → surfaces the laptop advisory once in its banner (it contains power core + BCD)

**Approved visible deltas** (recorded for `PARITY.md`): the main-menu status line and the start
log line gain a `Machine=laptop|desktop|unknown` field; flagged actions print an
`  [ADVISORY] …` line on matching machines. No other output changes.

## D17 — `PlatformTarget=x64` scoped to Windows builds *(P1; author-approved 2026-07-09)*

**What.** `src/Sincript/Sincript.csproj` guards the plan-§2 PE marking with
`Condition="$([MSBuild]::IsOSPlatform('Windows'))"`. On Windows — every dev box that ships
anything, and CI — the property is unchanged and the IL stays 64-bit-required. On a non-Windows
host it falls back to AnyCPU.

**Why.** `PlatformTarget=x64` stamps the IL assembly as x86-64-*required*, and an arm64 .NET
process refuses to load such an assembly (`FileLoadException: assembly architecture is not
compatible with the current process architecture`). Unconditional, it makes `dotnet test`
impossible on an Apple Silicon dev host: every test that touches a SUT type fails to load it.
That would have forced P1's test suite to be CI-only from the day it was written.

**Why this is safety-neutral.** The §2 contract is about the *running process* being 64-bit so
`WOW6432Node` paths and the default registry view match the batch's 64-bit cmd. Three facts keep
that intact:

1. The shipped artifact is a NativeAOT `dotnet publish -r win-x64`; a native binary's
   architecture comes from the RID, never from `PlatformTarget`. The published exe is
   byte-for-byte unaffected by this condition.
2. The publish gate runs on `windows-latest`, where the condition is true anyway.
3. `Program.cs` already asserts `Environment.Is64BitProcess` and exits 1 otherwise. That runtime
   check — not the PE header — is what actually enforces the contract; the csproj comment calls
   the marking "belt and suspenders."

**Precedent.** Identical in shape to the adjacent `EnableWindowsTargeting` accommodation, which
already relaxes a Windows-only build constraint on non-Windows dev hosts on the stated grounds
that "the native AOT *publish* still requires a Windows machine/CI."

**Residual risk, and how it is closed.** A Release IL build produced *on* macOS/Linux is AnyCPU
and would load in a 32-bit host. Nothing ships from such a build, and `Program.cs` rejects a
32-bit process at startup regardless. Rather than leave this to the P6 audit, `ci.yml` now reads
the published exe's COFF header and fails the job unless `Machine == 0x8664` (AMD64), so the
contract is enforced on the artifact that actually ships, on every commit.

## P0 implementation footnotes (micro-deviations, all safety-neutral)

- **F1.** `Prompts.MenuChoice` returns "0" (back/exit) on stdin EOF instead of cmd's
  `set /p` fast-spin — makes headless CI smoke runs terminate; interactive behavior identical.
- **F2.** Log timestamps are fixed `yyyy-MM-dd HH:mm:ss` instead of locale `%date% %time%`;
  the I13 contract is the line *grammar*, and the batch stamp was never locale-stable anyway.
- **F3.** `ConsoleUi.Pause` falls through without blocking when stdin is redirected — the
  batch `pause < NUL` behaved the same way.

## P1 implementation footnotes (refinements under D2/D3, author-approved 2026-07-09)

- **F4.** `PriorValue` captures the **raw `RegQueryValueEx` byte image plus the raw kind DWORD**,
  not a `RegistryValueKind`-typed value. `Microsoft.Win32.Registry` cannot represent an
  unterminated `REG_SZ`, an embedded NUL, a trailing empty `REG_MULTI_SZ` element, or any kind it
  does not model — and D2 says *all* kinds back up restorably. A backup that normalizes its input
  is not a backup. Side benefit: serialization becomes a pure `bytes -> text` function, testable
  on any dev host, with the Win32 surface reduced to one P/Invoke.
- **F5.** Kind decides the *text* form, never the *fidelity*. `REG_SZ` whose bytes are not a
  cleanly terminated UTF-16 string falls back to `hex(1):`, and a `REG_DWORD` that is not exactly
  four bytes falls back to `hex(4):`, rather than being laundered into a form that would restore
  differently. Unnamed kinds serialize by their raw DWORD (`hex(2a):`).
- **F6.** The `.reg` wrapping contract is **`reg import` compatibility, not byte-parity with
  `reg export`**. We match its shape (80 columns, trailing `\`, two-space continuations) for
  diff-friendliness, but importers accept a continuation after any comma, and no test asserts
  byte-identity with `reg export`'s wrapping.
- **F7.** `RegistryValueOptions.DoNotExpandEnvironmentNames` is mandatory anywhere a
  `REG_EXPAND_SZ` is read for backup. Without it a backup of `%SystemRoot%` records
  `C:\Windows`, and restoring it onto another machine writes the wrong path. Reading raw bytes
  (F4) sidesteps this entirely; the footnote stands as a guard for any managed read added later.
- **F8.** *(pending, not yet implemented)* `RegistryService.AlreadyAtTarget` (D3) compares kind
  first, then data with **kind-aware** semantics: a differing trailing NUL on `SZ`/`EXPAND_SZ` and
  the decoded string sequence for `MULTI_SZ` compare equal; `DWORD`/`QWORD`/`BINARY`/`NONE` are
  byte-exact. Strict byte equality would make a stored `REG_SZ` lacking its terminator never
  match its target, so every apply pass would rewrite it and snapshot the already-tweaked value as
  its "prior" state — reintroducing the exact SZ backup-burial bug D3 exists to fix.
- **F9.** *(pending)* I6 requires C# to list/preview/import **batch-era** `.reg` files, which
  `echo` wrote in the console code page (ANSI). New files are UTF-16LE. The reader must sniff the
  BOM and accept both.

**Verification.** F4-F7 are no longer claims about our own model of the `.reg` format.
`RegFileRoundTripTests` serializes a `PriorValue`, hands the file to `reg import`, reads the value
back through `RegQueryValueEx`, and requires the bytes to come back identical — across all fifteen
kinds, including the empty `REG_SZ`, empty `REG_MULTI_SZ`, `REG_NONE`, the 200-byte binary that
forces line wrapping, and the `%SystemRoot%` that must survive unexpanded. Windows is the judge,
so the test only runs there; CI is its first and only execution.
