namespace Sincript.Core;

/// <summary>
/// The batch script's globals (SCRIPT_DIR, BACKUP_DIR, LOGFILE, _ELEV, WIN_BUILD, GPU, ...)
/// as one object constructed once in Program and passed explicitly. No statics, no DI —
/// plan §3: plain constructed objects, matching the tool's self-contained character.
/// </summary>
internal sealed class AppSession
{
    /// <summary>_ELEV. True = full toolset; false = the documented limited (per-user) mode.</summary>
    public bool Elevated { get; }

    /// <summary>%~dp0 analog: the folder the exe lives in. Bundled inputs (hosts, boot.config,
    /// app.asar, SetTimerResolution.exe, sincript_presets\) are looked up beside it.</summary>
    public string ScriptDir { get; }

    /// <summary>Documents\PerfTweaks_Backups (I6: shared with the batch era, both generations
    /// read each other's backups).</summary>
    public string BackupDir { get; }

    public Logger Logger { get; }

    public SystemInfo System { get; }

    private AppSession(bool elevated, string scriptDir, string backupDir, Logger logger, SystemInfo system)
    {
        Elevated = elevated;
        ScriptDir = scriptDir;
        BackupDir = backupDir;
        Logger = logger;
        System = system;
    }

    public static AppSession Create(bool elevated)
    {
        string scriptDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

        // Batch read HKCU ...\Shell Folders\Personal to catch OneDrive-redirected Documents;
        // Environment.GetFolderPath goes through SHGetKnownFolderPath, which resolves the same
        // redirection (plan §8.3 — verify on a redirected profile during the P6 parity audit;
        // risk-registered fallback is a 10-line registry read).
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrEmpty(docs))
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            docs = Path.Combine(string.IsNullOrEmpty(profile) ? "." : profile, "Documents");
        }

        string backupDir = Path.Combine(docs, "PerfTweaks_Backups");
        try { Directory.CreateDirectory(backupDir); }
        catch { /* Logger degrades to no-op; actions will report honestly when backups fail (I3/I8) */ }

        // D12 (bucket A): PerfTweaks_ prefix + .log suffix preserved (ManageBackups counts
        // PerfTweaks_*.log), uniq token upgraded from %RANDOM% to sortable timestamp + GUID slice.
        string uniq = Guid.NewGuid().ToString("N")[..8];
        string logPath = Path.Combine(
            backupDir,
            $"PerfTweaks_{DateTime.Now:yyyyMMdd_HHmmss}_{uniq}.log");

        return new AppSession(elevated, scriptDir, backupDir, new Logger(logPath), SystemInfo.Detect());
    }
}
