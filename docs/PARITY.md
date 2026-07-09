# PARITY.md — menu-item-by-menu-item parity checklist

Filled in as phases land; audited fully in P6 against the batch on a test VM (elevated and
limited modes). Legend: [ ] not ported · [~] stubbed, navigable · [x] ported + parity-checked.
Approved deltas are listed per item, referencing DECISIONS.md.

## Shell
- [x] Elevation relaunch + limited mode (D11 delta: declined UAC now reaches the limited-mode offer)
- [x] Main menu banner (D16 delta: `Machine=` field appended to the Build line)
- [x] Menu semantics: empty input re-asks, invalid re-renders, 0 = back, action → re-render
- [x] Prompt default classes (I9): menu / default-No / default-Yes
- [x] Exit screen (log + backup paths, Bye, 2 s linger)
- [x] 11. What was excluded — full verbatim content

## 1 Cleanup & repair
- [~] 1 Clean temp/logs/caches (+ Event Viewer clear) — P2 (D9 delta approved: no reparse-point recursion)
- [~] 2 DISM + SFC — P2 (D4 delta approved: live output)
- [~] 3 Reset Windows Update — P2 (D4)
- [~] 4 Re-register Store — P2
- [~] 5 Compact WinSxS — P2 (D4)

## 2/3/4 Performance · Privacy · Power — [~] P2
## 5 Network & DNS — [~] P2 (TCP, DNS×4, stack reset)
## 6 Apps & files — [~] P4 (OpenAsar, Unity, hosts×2, SteamLight, timer res×2, debloat, startup mgr; D6 delta: real Unicode names)
## 7 Advanced — [~] P5 (mitigations×2, BCD×2, NVMe, IPv6, memcompress, GPU telemetry, HAGS, IFEO priority)
## 8 Backups & status — [~] P5 (restore point, full export, status, .reg restore, manage) · JSON restore [~] P3
## 9 Recommended safe set — [~] P2
## 10 Presets — [~] P3 (light/moderate/heavy/custom/restore)

## Cross-era interop acceptance (I6) — P3/P6
- [ ] Batch-written Preset_*.json restores via C#; C#-written JSON restores via batch
- [ ] Batch per-value .reg files listed/previewed/imported by C#
- [ ] Shared Documents\PerfTweaks_Backups folder; ManageBackups categories count both eras
