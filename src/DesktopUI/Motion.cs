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
    public static void Slide(FrameworkElement view,int direction=1)
    {
        view.BeginAnimation(UIElement.OpacityProperty,null);
        var shift=view.RenderTransform as TranslateTransform??new TranslateTransform();
        view.RenderTransform=shift;shift.BeginAnimation(TranslateTransform.XProperty,null);
        if(!GetEnabled(view)||!SystemParameters.ClientAreaAnimation){view.Opacity=1;shift.X=0;return;}
        var duration=TimeSpan.FromMilliseconds(200);
        var easing=new CubicEase{EasingMode=EasingMode.EaseOut};
        view.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.15,1,duration){FillBehavior=FillBehavior.Stop});
        shift.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(20*direction,0,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop});
    }
    public static void Value(System.Windows.Controls.Primitives.RangeBase control,double next,bool animate=true)
    {
        double previous=control.Value;control.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,null);
        control.Value=next;
        if(animate&&GetEnabled(control)&&SystemParameters.ClientAreaAnimation)
            control.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,new DoubleAnimation(previous,next,TimeSpan.FromMilliseconds(180)){FillBehavior=FillBehavior.Stop});
    }}