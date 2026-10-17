using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

// Export arranged WPF visuals for editable design-tool import, without taking a screenshot apart.
internal static class SvgSnapshot
{
    private static readonly XNamespace Ns="http://www.w3.org/2000/svg";
    private static int clipId;
    private static string N(double n)=>n.ToString("0.###",CultureInfo.InvariantCulture);
    public static void Save(FrameworkElement root,string path)
    {
        var svg=new XElement(Ns+"svg",new XAttribute("width",N(root.ActualWidth)),new XAttribute("height",N(root.ActualHeight)),new XAttribute("viewBox",$"0 0 {N(root.ActualWidth)} {N(root.ActualHeight)}"));
        Visit(root,root,svg);new XDocument(svg).Save(path);
    }
    private static void Visit(Visual visual,Visual root,XElement parent)
    {
        if(visual is UIElement element&&element.Visibility!=Visibility.Visible)return;
        double opacity=VisualTreeHelper.GetOpacity(visual);if(opacity<=0)return;
        var group=new XElement(Ns+"g",new XAttribute("opacity",N(opacity)));
        if(visual!=root){
            var t=visual.TransformToAncestor(root);var o=t.Transform(new Point());var x=t.Transform(new Point(1,0));var y=t.Transform(new Point(0,1));
            group.SetAttributeValue("transform",$"matrix({N(x.X-o.X)} {N(x.Y-o.Y)} {N(y.X-o.X)} {N(y.Y-o.Y)} {N(o.X)} {N(o.Y)})");
        }
        var clip=VisualTreeHelper.GetClip(visual);
        if(clip!=null){string id="clip"+(++clipId);group.Add(new XElement(Ns+"defs",new XElement(Ns+"clipPath",new XAttribute("id",id),new XElement(Ns+"path",new XAttribute("d",clip.GetFlattenedPathGeometry().ToString(CultureInfo.InvariantCulture))))));group.SetAttributeValue("clip-path","url(#"+id+")");}
        var drawing=VisualTreeHelper.GetDrawing(visual);if(drawing!=null)Draw(drawing,group);parent.Add(group);
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(visual);i++)if(VisualTreeHelper.GetChild(visual,i) is Visual child)Visit(child,visual,group);
    }
    private static string Paint(Brush? brush)=>brush is SolidColorBrush solid?$"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}":brush is GradientBrush gradient&&gradient.GradientStops.Count>0?$"#{gradient.GradientStops[0].Color.R:X2}{gradient.GradientStops[0].Color.G:X2}{gradient.GradientStops[0].Color.B:X2}":"none";
    private static void Draw(Drawing drawing,XElement parent)
    {
        switch(drawing){
            case DrawingGroup drawings:
                var group=new XElement(Ns+"g",new XAttribute("opacity",N(drawings.Opacity)));
                if(drawings.Transform!=null&&!drawings.Transform.Value.IsIdentity){var m=drawings.Transform.Value;group.SetAttributeValue("transform",$"matrix({N(m.M11)} {N(m.M12)} {N(m.M21)} {N(m.M22)} {N(m.OffsetX)} {N(m.OffsetY)})");}
                parent.Add(group);foreach(var child in drawings.Children)Draw(child,group);break;
            case GeometryDrawing geometry:
                var node=new XElement(Ns+"path",new XAttribute("d",geometry.Geometry.GetFlattenedPathGeometry().ToString(CultureInfo.InvariantCulture)),new XAttribute("fill",Paint(geometry.Brush)));
                if(geometry.Brush is SolidColorBrush color)node.SetAttributeValue("fill-opacity",N(color.Color.A/255d*color.Opacity));
                if(geometry.Pen!=null){node.SetAttributeValue("stroke",Paint(geometry.Pen.Brush));node.SetAttributeValue("stroke-width",N(geometry.Pen.Thickness));}
                parent.Add(node);break;
            case GlyphRunDrawing glyphs when glyphs.GlyphRun.Characters!=null:
                var run=glyphs.GlyphRun;double position=run.BaselineOrigin.X;
                var text=new XElement(Ns+"text",new XAttribute("font-family","Segoe UI"),new XAttribute("font-size",N(run.FontRenderingEmSize)),new XAttribute("fill",Paint(glyphs.ForegroundBrush)),new XAttribute("y",N(run.BaselineOrigin.Y)));
                if(run.GlyphTypeface.Weight.ToOpenTypeWeight()>=600)text.SetAttributeValue("font-weight",run.GlyphTypeface.Weight.ToOpenTypeWeight());
                var positions=new List<string>();
                for(int i=0;i<run.Characters.Count;i++){positions.Add(N(position));if(i<run.AdvanceWidths.Count)position+=run.AdvanceWidths[i];}
                text.SetAttributeValue("x",string.Join(" ",positions));text.Value=new string(run.Characters.ToArray());parent.Add(text);break;
            case ImageDrawing { ImageSource: DrawingImage icon } vector:
                var bounds=icon.Drawing.Bounds;var rectangle=vector.Rect;
                var iconGroup=new XElement(Ns+"g",new XAttribute("transform",$"translate({N(rectangle.X)} {N(rectangle.Y)}) scale({N(rectangle.Width/bounds.Width)} {N(rectangle.Height/bounds.Height)}) translate({N(-bounds.X)} {N(-bounds.Y)})"));
                parent.Add(iconGroup);Draw(icon.Drawing,iconGroup);break;
            case ImageDrawing image when image.ImageSource is BitmapSource bitmap:
                bool photo=bitmap.PixelWidth>256&&bitmap.PixelHeight>256;
                BitmapEncoder encoder=photo?new JpegBitmapEncoder{QualityLevel=90}:new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=new MemoryStream()){
                    encoder.Save(stream);var r=image.Rect;parent.Add(new XElement(Ns+"image",new XAttribute("x",N(r.X)),new XAttribute("y",N(r.Y)),new XAttribute("width",N(r.Width)),new XAttribute("height",N(r.Height)),new XAttribute("href",(photo?"data:image/jpeg;base64,":"data:image/png;base64,")+Convert.ToBase64String(stream.ToArray()))));
                }break;
        }
    }
}
