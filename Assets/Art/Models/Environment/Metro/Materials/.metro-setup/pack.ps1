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
public static class MetroChannelPacker {
 public static byte[] Read(Bitmap b) {
  var d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
  try { var v=new byte[b.Width*b.Height*4]; for(int y=0;y<b.Height;y++) Marshal.Copy(IntPtr.Add(d.Scan0,y*d.Stride),v,y*b.Width*4,b.Width*4); return v; }
  finally {b.UnlockBits(d);}
 }
 static byte[] Mask(string p,int w,int h) {
  if(String.IsNullOrEmpty(p))return null;
  using(var b=new Bitmap(p)) {if(b.Width!=w||b.Height!=h)throw new Exception("Mask dimensions differ: "+p);return Read(b);}
 }
 public static int Pack(string rough,string metal,string ao,string output) {
  int w,h;byte[] r;
  using(var b=new Bitmap(rough)){w=b.Width;h=b.Height;r=Read(b);}
  var m=Mask(metal,w,h);var o=Mask(ao,w,h);var v=new byte[r.Length];
  for(int i=0;i<v.Length;i+=4){v[i]=0;v[i+1]=o==null?(byte)255:o[i+2];v[i+2]=m==null?(byte)0:m[i+2];v[i+3]=(byte)(255-r[i+2]);}
  using(var b=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
   var d=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
   try {for(int y=0;y<h;y++)Marshal.Copy(v,y*w*4,IntPtr.Add(d.Scan0,y*d.Stride),w*4);} finally {b.UnlockBits(d);}
   b.Save(output,ImageFormat.Png);
  }
  using(var b=new Bitmap(output)) {var check=Read(b);if(check.Length!=v.Length)throw new Exception("Output size mismatch");for(int i=0;i<v.Length;i++)if(check[i]!=v[i])throw new Exception("Channel mismatch: "+output+" at byte "+i);}
  return w*h;
 }
}
'@
$jobs = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'jobs.json') -Raw | ConvertFrom-Json
$results = @()
foreach ($job in $jobs) {
 $pixels = [MetroChannelPacker]::Pack($job.roughness,$job.metallic,$job.ao,$job.output)
 $results += [PSCustomObject]@{output=$job.output;pixels=$pixels;verified=$true}
 Write-Output ([System.IO.Path]::GetFileName($job.output))
}
ConvertTo-Json -InputObject $results -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'packing-validation.json') -Encoding utf8
