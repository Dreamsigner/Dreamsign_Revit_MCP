using System.Threading;
using WixToolset.Dtf.WindowsInstaller;

namespace Dreamsign.Setup;

public static class CustomActions
{
    [CustomAction]
    public static ActionResult WaitForRevitExit(Session session)
    {
        try
        {
            var snapshot = RevitSessions.Scan();
            if (snapshot.CanContinue)
                return ActionResult.Success;
            if (!RevitSessions.CanShowWindow(session["DREAMSIGN_BUNDLE_DISPLAY"], session["UILevel"], Environment.UserInteractive))
            {
                using (var message = new Record(1))
                {
                    message[0] = "Enregistrez vos projets et fermez toutes les sessions Revit avant de désinstaller Dreamsign MCP. Aucune session ne sera fermée automatiquement.";
                    session.Message(InstallMessage.Error, message);
                }
                session.Log("Dreamsign MCP: uninstall blocked; Revit sessions={0}; scanError={1}", snapshot.Sessions.Length, snapshot.Error != null);
                return ActionResult.Failure;
            }
            bool allowed = false;
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    allowed = new RevitSessionsWindow(RevitSessions.Scan).ShowDialog() == true;
                }
                catch (Exception error) { failure = error; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
                throw failure;
            if (!allowed)
                return ActionResult.UserExit;
            // Recheck after the dialog exits; never rely on the last timer tick.
            return RevitSessions.Scan().CanContinue ? ActionResult.Success : ActionResult.Failure;
        }
        catch (Exception error)
        {
            session.Log("Dreamsign MCP: session check failed ({0}); uninstall blocked.", error.GetType().Name);
            return ActionResult.Failure;
        }
    }
}
