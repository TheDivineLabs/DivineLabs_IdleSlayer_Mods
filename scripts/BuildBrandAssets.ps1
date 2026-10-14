$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$images=@()
foreach($size in @(16,32,48,64,128,256)){
 $bmp=[Drawing.Bitmap]::new($size,$size);$g=[Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.ScaleTransform($size/128.0,$size/128.0)
 $g.Clear([Drawing.ColorTranslator]::FromHtml('#0e121c'))
 $blue=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#40b8f2'));$white=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#dce8f3'))
 $outer=[Drawing.PointF[]]@([Drawing.PointF]::new(30,30),[Drawing.PointF]::new(62,30),[Drawing.PointF]::new(86,54),[Drawing.PointF]::new(86,74),[Drawing.PointF]::new(62,98),[Drawing.PointF]::new(30,98))
 $g.FillPolygon($blue,$outer)
 $hole=[Drawing.PointF[]]@([Drawing.PointF]::new(47,47),[Drawing.PointF]::new(55,47),[Drawing.PointF]::new(69,61),[Drawing.PointF]::new(69,67),[Drawing.PointF]::new(55,81),[Drawing.PointF]::new(47,81))
 $dark=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#0e121c'));$g.FillPolygon($dark,$hole)
 $right=[Drawing.PointF[]]@([Drawing.PointF]::new(86,30),[Drawing.PointF]::new(100,30),[Drawing.PointF]::new(100,98),[Drawing.PointF]::new(74,98),[Drawing.PointF]::new(88,84),[Drawing.PointF]::new(86,84));$g.FillPolygon($white,$right)
 $stream=[IO.MemoryStream]::new();$bmp.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$images+=,@{Size=$size;Bytes=$stream.ToArray()}
 if($size -eq 256){$bmp.Save((Join-Path $root 'assets/DivineLabs.png'),[Drawing.Imaging.ImageFormat]::Png)}
 $stream.Dispose();$g.Dispose();$bmp.Dispose();$blue.Dispose();$white.Dispose();$dark.Dispose()
}
$out=[IO.File]::Create((Join-Path $root 'assets/DivineLabs.ico'));$writer=[IO.BinaryWriter]::new($out)
try{$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$images.Count);$offset=6+16*$images.Count;foreach($im in $images){$dimension=if($im.Size -eq 256){0}else{$im.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$im.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$im.Bytes.Length};foreach($im in $images){$writer.Write([byte[]]$im.Bytes)}}finally{$writer.Dispose()}
