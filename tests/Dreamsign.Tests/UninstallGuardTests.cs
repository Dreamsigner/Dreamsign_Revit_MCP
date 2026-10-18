using System.Windows;
using System.Windows.Threading;
using Dreamsign.Setup;
using Xunit;

namespace Dreamsign.Tests;

public sealed class UninstallGuardTests
{
    [Theory]
    [InlineData("4", "2", true, true)]
    [InlineData("2", "5", true, false)]
    [InlineData("3", "5", true, false)]
    [InlineData("", "5", true, true)]
    [InlineData("", "2", true, false)]
    [InlineData("4", "5", false, false)]
    public void SilentOrNonInteractiveMaintenanceNeverOpensAWindow(string burn, string msi, bool interactive, bool expected)
        => Assert.Equal(expected, RevitSessions.CanShowWindow(burn, msi, interactive));

    [Fact]
    public void WindowTracksSessionsAndRechecksBeforeAllowingUninstall()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var snapshot = new SessionSnapshot(new[] { "Revit · PID 123 · session Windows 1", "Revit · PID 456 · session Windows 2" });
                var window = new RevitSessionsWindow(() => snapshot, TimeSpan.FromMilliseconds(15));
                window.Left = -10000;
                window.Top = -10000;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.ShowInTaskbar = false;
                window.ShowActivated = false;
                try
                {
                    window.Show();
                    window.UpdateLayout();
                    Assert.False(window.ContinueButton.IsEnabled);
                    Assert.Equal(2, window.SessionList.Items.Count);
                    var preview = Environment.GetEnvironmentVariable("DREAMSIGN_GUARD_PREVIEW");
                    if (!string.IsNullOrEmpty(preview))
                    {
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using var stream = File.Create(preview);
                        encoder.Save(stream);
                    }
                    // Pump the dispatcher: session changes must be picked up by the actual timer.
                    snapshot = new SessionSnapshot(Array.Empty<string>());
                    var frame = new DispatcherFrame();
                    var stop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                    stop.Tick += (_, _) => { stop.Stop(); frame.Continue = false; };
                    stop.Start();
                    Dispatcher.PushFrame(frame);
                    Assert.True(window.ContinueButton.IsEnabled);
                    snapshot = new SessionSnapshot(new[] { "Revit reopened" });
                    window.ContinueButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                    Assert.True(window.IsVisible);
                    Assert.False(window.ContinueButton.IsEnabled);
                    snapshot = new SessionSnapshot(Array.Empty<string>(), "Process enumeration unavailable");
                    window.RefreshSessions();
                    Assert.False(window.ContinueButton.IsEnabled);
                }
                finally { window.Close(); window.Dispatcher.InvokeShutdown(); }
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Guard window test timed out");
        Assert.Null(failure);
    }
}
