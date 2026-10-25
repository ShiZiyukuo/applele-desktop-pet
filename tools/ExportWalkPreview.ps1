param([Parameter(Mandatory=$true)][string]$InputImage,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$inputPath=[IO.Path]::GetFullPath($InputImage)
$source=New-Object System.Windows.Media.Imaging.BitmapImage
$source.BeginInit();$source.UriSource=New-Object Uri($inputPath);$source.CacheOption=[System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad;$source.EndInit();$source.Freeze()
$width=[int][math]::Floor($source.PixelWidth/4);$height=[int][math]::Floor($source.PixelHeight/2)
$encoder=New-Object System.Windows.Media.Imaging.GifBitmapEncoder
$background=New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Color]::FromRgb(38,41,46))
for($index=0;$index -lt 8;$index++){
    $rectangle=New-Object System.Windows.Int32Rect(($index%4)*$width),([math]::Floor($index/4)*$height),$width,$height
    $cel=New-Object System.Windows.Media.Imaging.CroppedBitmap($source,$rectangle)
    $png=New-Object System.Windows.Media.Imaging.PngBitmapEncoder;$png.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($cel))
    $pngStream=[IO.File]::Create((Join-Path $OutputDirectory ('frame-{0:00}.png' -f ($index+1))))
    try{$png.Save($pngStream)}finally{$pngStream.Dispose()}
    $visual=New-Object System.Windows.Media.DrawingVisual;$drawing=$visual.RenderOpen()
    try{$drawing.DrawRectangle($background,$null,(New-Object System.Windows.Rect(0,0,$width,$height)));$drawing.DrawImage($cel,(New-Object System.Windows.Rect(0,0,$width,$height)))}finally{$drawing.Close()}
    $render=New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width,$height,96,96,([System.Windows.Media.PixelFormats]::Pbgra32));$render.Render($visual)
    $metadata=New-Object System.Windows.Media.Imaging.BitmapMetadata('gif');$metadata.SetQuery('/grctlext/Delay',[UInt16]12);$metadata.SetQuery('/grctlext/Disposal',[byte]2)
    if($index -eq 0){$metadata.SetQuery('/appext/application',[Text.Encoding]::ASCII.GetBytes('NETSCAPE2.0'));$metadata.SetQuery('/appext/data',[byte[]](3,1,0,0,0))}
    $frame=[System.Windows.Media.Imaging.BitmapFrame]::Create($render,$null,$metadata,$null);$encoder.Frames.Add($frame)
}
$stream=[IO.File]::Create((Join-Path $OutputDirectory 'walk-loop.gif'))
try{$encoder.Save($stream)}finally{$stream.Dispose()}
# WPF may discard per-frame timing on save. Preserve compressed image data and write
# standard GIF control/loop metadata explicitly; this does not modify any pose pixels.
$gifPath=Join-Path $OutputDirectory 'walk-loop.gif';$bytes=[IO.File]::ReadAllBytes($gifPath)
$headerLength=13;if($bytes[10] -band 128){$headerLength+=3*[math]::Pow(2,($bytes[10] -band 7)+1)}
$offset=$headerLength;$frameCount=0
while($offset -lt $bytes.Length){
    $kind=$bytes[$offset++];if($kind -eq 59){break}
    if($kind -eq 33){
        $label=$bytes[$offset++]
        if($label -eq 249){$bytes[$offset+1]=($bytes[$offset+1] -band 3) -bor 8;$bytes[$offset+2]=12;$bytes[$offset+3]=0}
    }elseif($kind -eq 44){
        $frameCount++;$packed=$bytes[$offset+8];$offset+=9;if($packed -band 128){$offset+=3*[math]::Pow(2,($packed -band 7)+1)};$offset++
    }else{throw 'Invalid GIF block'}
    do{$length=$bytes[$offset++];$offset+=$length}while($length -gt 0)
}
if($frameCount -ne 8){throw 'Preview must contain eight image blocks'}
$loop=[byte[]](33,255,11)+[Text.Encoding]::ASCII.GetBytes('NETSCAPE2.0')+[byte[]](3,1,0,0,0)
$result=New-Object IO.MemoryStream
try{$result.Write($bytes,0,$headerLength);$result.Write($loop,0,$loop.Length);$result.Write($bytes,$headerLength,$bytes.Length-$headerLength);[IO.File]::WriteAllBytes($gifPath,$result.ToArray())}finally{$result.Dispose()}
[ordered]@{source=$inputPath;width=$source.PixelWidth;height=$source.PixelHeight;columns=4;rows=2;frames=8;frameDelayCentiseconds=12;method='fixed-grid playback only; no per-frame repositioning, warping or interpolated extra poses';approved=$false;productionAppUpdated=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'preview-info.json') -Encoding utf8
Write-Output (Join-Path $OutputDirectory 'walk-loop.gif')
