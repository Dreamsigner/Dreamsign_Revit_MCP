using System;
using System.IO;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Dreamsign.Contracts;
using Dreamsign.Desktop;

namespace AstraRevitBridge
{
    [Transaction(TransactionMode.Manual)]
    public sealed class ControlPanelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var ui = commandData.Application;
                var directory = LocalSettings.DefaultDirectory;
                PanelState Read()
                {
                    var document = ui.ActiveUIDocument?.Document;
                    var path = document?.PathName ?? "";
                    bool valid = document != null && !document.IsFamilyDocument && Path.IsPathFullyQualified(path)
                        && path.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) && File.Exists(path);
                    BridgeTarget target = null;
                    var store = new StateStore(directory);
                    if (File.Exists(store.StatePath)) target = store.Target;
                    bool same = valid && target != null && target.Document == document.Title &&
                        string.Equals(Path.GetFullPath(target.Path), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
                    var contact = App.LastContactUtc;
                    return new PanelState(document?.Title ?? "Aucun projet ouvert", path, target?.Document ?? "Aucun projet sélectionné",
                        valid, same, contact.HasValue ? contact.Value.ToLocalTime().ToString("dd/MM HH:mm:ss") : "Aucun échange reçu",
                        directory, LocalSettings.InstalledServer(directory));
                }
                void Select()
                {
                    var document = ui.ActiveUIDocument?.Document;
                    if (document == null) throw new ArgumentException("Ouvrez un projet Revit.");
                    new StateStore(directory).SelectProject(document.Title, document.PathName, Environment.ProcessId, document.IsFamilyDocument);
                }
                void Connect(DesktopClient client, string profile)
                {
                    var executable = LocalSettings.InstalledServer(directory) ?? throw new InvalidOperationException("Relancez l’installateur pour installer le serveur.");
                    ClientIntegration.Configure(client, ClientIntegration.ConfigurationPath(client), executable, directory, profile);
                }
                var window = new ControlPanelWindow(Read, Select, Connect);
                new WindowInteropHelper(window).Owner = ui.MainWindowHandle;
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception error) { message = error.Message; return Result.Failed; }
        }
    }
}
