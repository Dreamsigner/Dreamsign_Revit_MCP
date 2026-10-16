using System.Text.Json.Nodes;
using System.Windows;
using Dreamsign.Contracts;

namespace Dreamsign.Desktop;

public sealed record PanelState(string ActiveProject, string ActivePath, string TargetProject, bool CanSelect, bool IsTarget,
    string LastContact, string DataDirectory, string? ServerExecutable);

public partial class ControlPanelWindow : Window
{
    private readonly Func<PanelState> read;
    private readonly Action select;
    private readonly Action<DesktopClient, string> connect;
    private PanelState state = null!;
    public ControlPanelWindow(Func<PanelState> read, Action select, Action<DesktopClient, string> connect)
    {
        InitializeComponent(); this.read = read; this.select = select; this.connect = connect;
        Refresh();
    }
    private string Profile => ProfileChoice.SelectedIndex switch { 0 => "inspection", 1 => "workflow", _ => "modeling" };
    private void ClosePanel(object sender, RoutedEventArgs args) => Close();
    private void Refresh()
    {
        state = read();
        BridgeStatus.Text = "Bridge chargé";
        VersionText.Text = "Dreamsign MCP · " + ToolCatalog.Version;
        ActiveProjectText.Text = state.ActiveProject;
        ActiveProjectText.ToolTip = state.ActiveProject;
        ActivePathText.Text = state.ActivePath; ActivePathText.ToolTip = state.ActivePath;
        UseProjectButton.IsEnabled = state.CanSelect;
        UseProjectButton.ToolTip = state.CanSelect ? "Autoriser le MCP à utiliser ce projet." : "Ouvrez un projet et enregistrez-le au format RVT.";
        TargetText.Text = state.IsTarget ? "✓ Ce projet est la cible du MCP." : "Cible actuelle : " + state.TargetProject;
        TargetBadgeText.Text = state.IsTarget ? "Projet autorisé" : "À sélectionner";
        TargetBadge.Background = (System.Windows.Media.Brush)FindResource(state.IsTarget ? "AccentTint" : "Canvas");
        TargetBadgeText.Foreground = (System.Windows.Media.Brush)FindResource(state.IsTarget ? "Accent" : "MutedInk");
        ConnectButton.IsEnabled = state.IsTarget && state.ServerExecutable is not null;
        ConnectButton.ToolTip = state.ServerExecutable is null ? "Le serveur est introuvable. Relancez l’installateur."
            : !state.IsTarget ? "Choisissez d’abord le projet de travail." : "Sauvegarder et configurer cet assistant.";
        System.Windows.Controls.ToolTipService.SetShowOnDisabled(ConnectButton, true);
        ContactText.Text = "Dernier contact : " + state.LastContact;
        StorageText.Text = "Données de cet utilisateur : " + state.DataDirectory;
    }
    private void Run(Action action)
    {
        try { action(); Refresh(); }
        catch (Exception error) when (error is not OutOfMemoryException) { FeedbackText.Text = "Action interrompue : " + error.Message; }
    }
    private void SelectProject(object sender, RoutedEventArgs args) => Run(() =>
    {
        select(); FeedbackText.Text = "Projet sélectionné. Activer un autre fichier ne changera pas cette autorisation.";
    });
    private void ConnectClient(object sender, RoutedEventArgs args) => Run(() =>
    {
        connect((DesktopClient)ClientChoice.SelectedIndex, Profile);
        FeedbackText.Text = "Configuration enregistrée avec sauvegarde. Cliquez sur « Terminer », puis rechargez la connexion MCP dans votre assistant.";
    });
    private void Diagnose(object sender, RoutedEventArgs args) => Run(() =>
    {
        Refresh();
        FeedbackText.Text = !state.CanSelect ? "Le projet actif doit être un fichier RVT enregistré."
            : state.ServerExecutable is null ? "Le serveur installé est introuvable. Relancez l’installateur."
            : !state.IsTarget ? "Le bridge fonctionne. Cliquez sur « Utiliser ce projet » pour autoriser ce fichier."
            : "Le bridge est chargé, le serveur est installé et ce projet est ciblé. Le dernier contact indique un échange passé, pas une connexion permanente.";
    });
    private void CopyConfiguration(object sender, RoutedEventArgs args) => Run(() =>
    {
        if (state.ServerExecutable is null) throw new InvalidOperationException("Installez d’abord le serveur.");
        var configuration = new JsonObject { ["mcpServers"] = new JsonObject { [ClientIntegration.EntryName] = new JsonObject
        {
            ["command"] = state.ServerExecutable,
            ["args"] = new JsonArray(ClientIntegration.Arguments(state.DataDirectory, Profile).Select(a => (JsonNode)JsonValue.Create(a)!).ToArray())
        } } };
        Clipboard.SetText(configuration.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        FeedbackText.Text = "Configuration copiée. Ajoutez-la à votre client compatible MCP.";
    });
}
