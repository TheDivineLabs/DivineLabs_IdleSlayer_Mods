using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace DivineLabs.DesktopUI;
public static class Motion
{
    public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(Motion),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetEnabled(DependencyObject o)=>(bool)o.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject o,bool v)=>o.SetValue(EnabledProperty,v);
    public static void Enter(FrameworkElement view)
    {
        view.BeginAnimation(UIElement.OpacityProperty,null);
        if(!GetEnabled(view)||!SystemParameters.ClientAreaAnimation){view.Opacity=1;return;}
        view.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.45,1,TimeSpan.FromMilliseconds(180)){FillBehavior=FillBehavior.Stop});
    }
}