$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$drawingDir = Split-Path ([System.Drawing.Bitmap].Assembly.Location)
$refs = @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Rectangle].Assembly.Location, [System.Runtime.InteropServices.Marshal].Assembly.Location)
$refs += @(Get-ChildItem -LiteralPath $drawingDir -Filter 'System.Private.Windows*.dll' | Select-Object -ExpandProperty FullName)
Add-Type -ReferencedAssemblies $refs -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class MetroGlassPacker {
 static byte[] Read(Bitmap b) {
  var d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
  try {var v=new byte[b.Width*b.Height*4];for(int y=0;y<b.Height;y++)Marshal.Copy(IntPtr.Add(d.Scan0,y*d.Stride),v,y*b.Width*4,b.Width*4);return v;} finally {b.UnlockBits(d);}
 }
 public static int Pack(string color,string opacity,string output) {
  int w,h;byte[] v,a;
  using(var b=new Bitmap(color)){w=b.Width;h=b.Height;v=Read(b);}
  using(var b=new Bitmap(opacity)){if(b.Width!=w||b.Height!=h)throw new Exception("Mask dimensions differ");a=Read(b);}
  for(int i=0;i<v.Length;i+=4)v[i+3]=a[i+2];
  using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
   var d=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
   try{for(int y=0;y<h;y++)Marshal.Copy(v,y*w*4,IntPtr.Add(d.Scan0,y*d.Stride),w*4);}finally{b.UnlockBits(d);}
   b.Save(output,ImageFormat.Png);
  }
  using(var b=new Bitmap(output)){var check=Read(b);for(int i=0;i<v.Length;i++)if(check[i]!=v[i])throw new Exception("Pixel mismatch: "+output+" at "+i);}
  return w*h;
 }
}
'@
$texRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\Textures'))
$results = @()
foreach ($family in @('LateralTren','PuertasLaterales','PuertaDelantera')) {
 $pixels = [MetroGlassPacker]::Pack((Join-Path $texRoot ($family+'_albedo.jpeg')),(Join-Path $texRoot ($family+'_opacity.jpeg')),(Join-Path $texRoot ('Glass\'+$family+'_Glass.png')))
 $results += [PSCustomObject]@{family=$family;pixels=$pixels;verified=$true}
}
ConvertTo-Json -InputObject $results | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'texture-validation.json') -Encoding utf8
$results | ConvertTo-Json -Compress
