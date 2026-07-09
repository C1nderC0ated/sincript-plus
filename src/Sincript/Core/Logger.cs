using System.Text;

namespace Sincript.Core;

/// <summary>
/// The :Log routine. Same line grammar as the batch log — "[stamp] message", where callers
/// supply the EXEC:/OK:/FAIL:/REGADD/ABORT: tags — and the same discipline (I13): outcomes
/// only, never raw child output. Timestamp format is fixed/sortable instead of the batch's
/// locale-dependent %date% %time% (the grammar, not the stamp locale, was ever the contract).
/// D6 (approved): UTF-8, so non-ASCII names are logged as themselves.
/// </summary>
internal sealed class Logger
{
    public string LogFilePath { get; }

    private bool _dead; // first hard IO failure turns the logger into a no-op, like a batch >> to an unwritable path

    public Logger(string logFilePath) => LogFilePath = logFilePath;

    public void Log(string message)
    {
        if (_dead) return;
        try
        {
            // Open-append-close per line: the batch ">>" semantics — every line durable
            // immediately, nothing buffered across a crash. Volume is tiny; simplicity wins.
            File.AppendAllText(
                LogFilePath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            _dead = true; // never let logging take the tool down
        }
    }
}
