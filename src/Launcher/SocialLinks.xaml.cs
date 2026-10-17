using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class SocialLinks : UserControl
{
    public SocialLinks() => InitializeComponent();
    private void OpenLink(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo((string)((Button)sender).Tag) { UseShellExecute = true }); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        { ToolTip = SetupText.Current["LaunchFailed"] + error.Message; ToolTipService.SetIsEnabled(this, true); }
    }
}
