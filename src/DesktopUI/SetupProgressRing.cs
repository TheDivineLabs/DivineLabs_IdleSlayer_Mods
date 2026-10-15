using System.Windows;
using System.Windows.Media;
namespace DivineLabs.DesktopUI;
public sealed class SetupProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty=DependencyProperty.Register(nameof(Value),typeof(double),typeof(SetupProgressRing),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    public double Value{get=>(double)GetValue(ValueProperty);set=>SetValue(ValueProperty,value);}
    private static readonly Pen Track=new(new SolidColorBrush(Color.FromRgb(226,231,240)),7);
    private static readonly Pen Accent=new(new SolidColorBrush(Color.FromRgb(102,88,232)),7){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
    protected override void OnRender(DrawingContext context)
    {
        var center=new Point(ActualWidth/2,ActualHeight/2);double radius=Math.Max(0,Math.Min(ActualWidth,ActualHeight)/2-5);
        context.DrawEllipse(null,Track,center,radius,radius);
        double progress=Math.Clamp(Value,0,100);
        if(progress<=0)return;
        if(progress>=100){context.DrawEllipse(null,Accent,center,radius,radius);return;}
        double angle=progress/100*Math.PI*2;
        var geometry=new StreamGeometry();
        using(var drawing=geometry.Open()){
            drawing.BeginFigure(new Point(center.X,center.Y-radius),false,false);
            drawing.ArcTo(new Point(center.X+Math.Sin(angle)*radius,center.Y-Math.Cos(angle)*radius),new Size(radius,radius),0,progress>50,SweepDirection.Clockwise,true,false);
        }
        geometry.Freeze();context.DrawGeometry(null,Accent,geometry);
    }
}
