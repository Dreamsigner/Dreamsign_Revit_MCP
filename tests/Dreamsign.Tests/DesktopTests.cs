using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dreamsign.Contracts;
using Dreamsign.Desktop;
using Xunit;

namespace Dreamsign.Tests;

public sealed class DesktopTests
{
    [Fact]
    public void PanelSelectsOnlyOnClickAndConfiguresChosenClient()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                bool selected=false, connected=false;
                var window = new ControlPanelWindow(
                    ()=>new("Projet exemple", @"C:\Projects\Exemple.rvt",selected ? "Projet exemple" : "Aucun projet sélectionné",true,selected,
                        "Aucun échange reçu", @"C:\Users\Utilisateur\AppData\Local\DreamsignRevitMCP", @"C:\Tools\Dreamsign.Revit.Mcp.exe"),
                    ()=>selected=true,
                    (client,profile)=>{ Assert.Equal(DesktopClient.Cursor,client); Assert.Equal("modeling",profile); connected=true; });
                window.Left=-10000; window.Top=-10000; window.WindowStartupLocation=WindowStartupLocation.Manual;
                window.ShowInTaskbar=false; window.ShowActivated=false;
                try
                {
                    window.Show(); window.UpdateLayout();
                    Assert.False(selected); Assert.False(connected);
                    var preview=Environment.GetEnvironmentVariable("DREAMSIGN_UI_PREVIEW");
                    if(preview is not null)
                    {
                        var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream=File.Create(preview); encoder.Save(stream);
                    }
                    var button=(Button)window.FindName("UseProjectButton");
                    Assert.True(button.IsEnabled); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.True(selected);
                    Assert.Contains("Ce projet est la cible",((TextBlock)window.FindName("TargetText")).Text);
                    ((ComboBox)window.FindName("ClientChoice")).SelectedIndex=2;
                    var connect=FindButton(window,"Configurer la connexion");
                    connect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.True(connected);
                }
                finally { window.Close(); window.Dispatcher.InvokeShutdown(); }
            }
            catch(Exception error){failure=error;}
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)),"WPF test timed out");
        Assert.Null(failure);
    }
    private static Button FindButton(DependencyObject root,string label)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            if(child is Button button && button.Content?.ToString()==label)return button;
            try{return FindButton(child,label);}catch(InvalidOperationException){}
        }
        throw new InvalidOperationException("Button not found");
    }
}

