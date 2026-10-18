using System.Diagnostics;

namespace Dreamsign.Setup;

public sealed class SessionSnapshot
{
    public SessionSnapshot(string[] sessions, string? error = null)
    {
        Sessions = sessions;
        Error = error;
    }
    public string[] Sessions
    {
        get;
    }
    public string? Error
    {
        get;
    }
    public bool CanContinue => Error == null && Sessions.Length == 0;
}

public static class RevitSessions
{
    public static bool CanShowWindow(string burnDisplay, string msiLevel, bool interactive)
        => interactive && (burnDisplay.Length > 0 ? burnDisplay == "4" : int.TryParse(msiLevel, out var level) && level >= 4);

    public static SessionSnapshot Scan()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("Revit");
        }
        catch (Exception) { return new SessionSnapshot(Array.Empty<string>(), "Impossible de vérifier les sessions Revit. Réessayez après avoir fermé Revit."); }
        var sessions = new List<string>();
        foreach (var process in processes)
        {
            using (process)
            {
                // Titles may be inaccessible in another Windows session; the PID still blocks removal.
                string label = "Revit · PID " + process.Id;
                try
                {
                    if (process.HasExited)
                        continue;
                    label += " · session Windows " + process.SessionId;
                    var title = process.MainWindowTitle;
                    if (!string.IsNullOrWhiteSpace(title))
                        label += " — " + title;
                }
                catch (InvalidOperationException) { continue; }
                catch (System.ComponentModel.Win32Exception) { }
                sessions.Add(label);
            }
        }
        return new SessionSnapshot(sessions.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}
