using System.Windows;
using System.Windows.Threading;

namespace Dreamsign.Setup;

public partial class RevitSessionsWindow : Window
{
    private readonly Func<SessionSnapshot> scan;
    private readonly DispatcherTimer timer;
    private string lastList = "";
    public RevitSessionsWindow(Func<SessionSnapshot> scan, TimeSpan? interval = null)
    {
        this.scan = scan;
        InitializeComponent();
        timer = new DispatcherTimer { Interval = interval ?? TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => RefreshSessions();
        Loaded += (_, _) => { RefreshSessions(); timer.Start(); };
        Closed += (_, _) => timer.Stop();
    }
    public void RefreshSessions()
    {
        var snapshot = scan();
        var list = string.Join("\n", snapshot.Sessions);
        if (list != lastList) { SessionList.ItemsSource = snapshot.Sessions; lastList = list; }
        ContinueButton.IsEnabled = snapshot.CanContinue;
        StatusText.Text = snapshot.Error ?? (snapshot.CanContinue
            ? "Toutes les sessions Revit sont fermées. Vous pouvez continuer."
            : snapshot.Sessions.Length + " session(s) Revit active(s). Vérification automatique chaque seconde.");
    }
    private void ContinueClick(object sender, RoutedEventArgs e)
    {
        RefreshSessions();
        if (ContinueButton.IsEnabled) DialogResult = true;
    }
    private void CancelClick(object sender, RoutedEventArgs e) => Close();
}
