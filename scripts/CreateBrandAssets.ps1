param([string]$Root=(Split-Path $PSScriptRoot -Parent),[string]$PreviewDirectory="")
Add-Type -AssemblyName PresentationCore,WindowsBase
[xml]$brandSource=Get-Content -LiteralPath (Join-Path $Root 'assets\DivineLabs.svg') -Raw
$brandPath=($brandSource.DocumentElement.ChildNodes|Where-Object LocalName -eq 'path'|Select-Object -First 1).GetAttribute('d')
$input=Join-Path $Root 'assets\IdleSlayer.original.ico'
$decoder=[Windows.Media.Imaging.IconBitmapDecoder]::new([Uri]::new($input),[Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
$source=$decoder.Frames|Sort-Object PixelWidth -Descending|Select-Object -First 1
$frames=[Collections.Generic.List[byte[]]]::new()
foreach($size in @(16,24,32,48,64,128,256)){
 $drawing=[Windows.Media.DrawingVisual]::new();$dc=$drawing.RenderOpen()
 [Windows.Media.RenderOptions]::SetBitmapScalingMode($drawing,[Windows.Media.BitmapScalingMode]::NearestNeighbor)
 $dc.DrawImage($source,[Windows.Rect]::new(0,0,$size,$size))
 # Keep the game icon recognisable, with a larger, pixel-aligned DivineLabs badge.
 # Tiny shell icons need slightly more badge area than the large application artwork.
 $badgeSize=[Math]::Ceiling($size*$(if($size-le32){.56}else{.50}))
 $badgeLeft=$size-$badgeSize;$badgeTop=$size-$badgeSize
 $borderWidth=$(if($size-le32){1.0}else{$size/128.0})
 $badgeRect=[Windows.Rect]::new($badgeLeft+$borderWidth/2,$badgeTop+$borderWidth/2,$badgeSize-$borderWidth,$badgeSize-$borderWidth)
 $fill=[Windows.Media.LinearGradientBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#7B55FF'),[Windows.Media.ColorConverter]::ConvertFromString('#5347DB'),45)
 $dc.DrawRoundedRectangle($fill,[Windows.Media.Pen]::new([Windows.Media.Brushes]::White,$borderWidth),$badgeRect,$badgeSize*.23,$badgeSize*.23)
 $geometry=[Windows.Media.Geometry]::Parse($brandPath)
 $bounds=$geometry.Bounds
 $glyphScale=($badgeSize*.84)/$bounds.Height
 $glyphLeft=$badgeLeft+($badgeSize-$bounds.Width*$glyphScale)/2
 $glyphTop=$badgeTop+($badgeSize-$bounds.Height*$glyphScale)/2
 $dc.PushTransform([Windows.Media.TranslateTransform]::new($glyphLeft,$glyphTop))
 $dc.PushTransform([Windows.Media.ScaleTransform]::new($glyphScale,$glyphScale))
 $dc.PushTransform([Windows.Media.TranslateTransform]::new(-$bounds.X,-$bounds.Y))
 $dc.DrawGeometry([Windows.Media.Brushes]::White,$null,$geometry)
 $dc.Pop();$dc.Pop();$dc.Pop();$dc.Close()
 $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($drawing)
 $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $stream=[IO.MemoryStream]::new();$encoder.Save($stream);$frames.Add($stream.ToArray());$stream.Dispose()
 if($PreviewDirectory){
  [IO.Directory]::CreateDirectory($PreviewDirectory)|Out-Null
  [IO.File]::WriteAllBytes((Join-Path $PreviewDirectory ("icon-"+$size+".png")),$frames[$frames.Count-1])
 }
 if($size-eq 256){[IO.File]::WriteAllBytes((Join-Path $Root 'assets\DivineLabs.png'),$frames[$frames.Count-1])}
}
$stream=[IO.MemoryStream]::new();$writer=[IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
$offset=6+16*$frames.Count;$sizes=@(16,24,32,48,64,128,256)
for($i=0;$i-lt $frames.Count;$i++){$value=if($sizes[$i]-eq 256){0}else{$sizes[$i]};$writer.Write([byte]$value);$writer.Write([byte]$value);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset);$offset+=$frames[$i].Length}
foreach($frame in $frames){$writer.Write($frame)}
[IO.File]::WriteAllBytes((Join-Path $Root 'assets\DivineLabs.ico'),$stream.ToArray());$writer.Dispose();$stream.Dispose()
Write-Output ('Original source: '+$source.PixelWidth+'px; application icon: Idle Slayer + DivineLabs badge, 7 sizes.')
# Both UI runtimes use the same path as the source SVG.
[xml]$svg=Get-Content -LiteralPath (Join-Path $Root 'assets\DivineLabs.svg') -Raw
$path=$svg.DocumentElement.ChildNodes|Where-Object LocalName -eq 'path'|Select-Object -First 1
$flat=[Windows.Media.Geometry]::Parse($path.GetAttribute('d')).GetFlattenedPathGeometry(.25,[Windows.Media.ToleranceType]::Absolute)
$lines=[Collections.Generic.List[string]]::new()
$lines.Add('using UnityEngine;')
$lines.Add('namespace DivineLabs.IdleSlayer;')
$lines.Add('// Generated from assets/DivineLabs.svg by scripts/CreateBrandAssets.ps1.')
$lines.Add('internal static class BrandGeometry { internal static readonly Vector2[][] Ribbons = {')
foreach($figure in $flat.Figures){
 $points=[Collections.Generic.List[Windows.Point]]::new();$points.Add($figure.StartPoint)
 foreach($segment in $figure.Segments){if($segment-is[Windows.Media.PolyLineSegment]){foreach($point in $segment.Points){$points.Add($point)}}elseif($segment-is[Windows.Media.LineSegment]){$points.Add($segment.Point)}}
 $values=foreach($point in $points){'new('+ $point.X.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)+'f,'+$point.Y.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)+'f)'}
 $lines.Add('new Vector2[] {'+($values-join ',')+'},')
}
$lines.Add('}; }')
[IO.File]::WriteAllLines((Join-Path $Root 'src\Mod\BrandGeometry.cs'),$lines)
