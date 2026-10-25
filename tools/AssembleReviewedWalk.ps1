param([string]$MapFile,[string]$OutputDirectory)
$ErrorActionPreference='Stop';Add-Type -AssemblyName System.Drawing
$map=Get-Content -LiteralPath $MapFile -Raw|ConvertFrom-Json
New-Item -ItemType Directory -Path $OutputDirectory,(Join-Path $OutputDirectory 'sources') -Force|Out-Null
$atlas=New-Object Drawing.Bitmap(1536,1024,[Drawing.Imaging.PixelFormat]::Format32bppPArgb)
$graphics=[Drawing.Graphics]::FromImage($atlas);$graphics.Clear([Drawing.Color]::Transparent)
$graphics.InterpolationMode='HighQualityBicubic';$graphics.PixelOffsetMode='HighQuality'
$transforms=@()
try{
    for($index=0;$index -lt 8;$index++){
        $item=$map[$index];$name='frame-{0:00}.png' -f ($index+1)
        Copy-Item -LiteralPath $item.file -Destination (Join-Path $OutputDirectory ('sources/'+$name)) -Force
        $source=New-Object Drawing.Bitmap($item.file)
        try{
            $factor=[math]::Min(384.0/$source.Width,512.0/$source.Height)
            $width=[single]($source.Width*$factor);$height=[single]($source.Height*$factor)
            $target=New-Object Drawing.RectangleF([single](($index%4)*384+(384-$width)/2)),([single]([math]::Floor($index/4)*512+(512-$height)/2)),$width,$height
            $graphics.DrawImage($source,$target)
            $transforms+=[ordered]@{frame=($index+1);sourceWidth=$source.Width;sourceHeight=$source.Height;uniformScale=$factor;note='Whole source canvas fitted into a fixed cell; no limb warping, pose interpolation or per-body recentering.'}
        }finally{$source.Dispose()}
    }
    $atlas.Save((Join-Path $OutputDirectory 'sprite-atlas.png'),[Drawing.Imaging.ImageFormat]::Png)
    $sheet=New-Object Drawing.Bitmap(1536,1024)
    $sheetGraphics=[Drawing.Graphics]::FromImage($sheet);$font=New-Object Drawing.Font('Segoe UI',16,[Drawing.FontStyle]::Bold)
    try{
        $sheetGraphics.Clear([Drawing.Color]::FromArgb(38,41,46));$sheetGraphics.DrawImageUnscaled($atlas,0,0)
        for($index=0;$index -lt 8;$index++){$sheetGraphics.DrawString(($index+1).ToString(),$font,[Drawing.Brushes]::White,[single](($index%4)*384+12),[single]([math]::Floor($index/4)*512+10))}
        $sheet.Save((Join-Path $OutputDirectory 'eight-frames.png'),[Drawing.Imaging.ImageFormat]::Png)
    }finally{$font.Dispose();$sheetGraphics.Dispose();$sheet.Dispose()}
}finally{$graphics.Dispose();$atlas.Dispose()}
$transforms|ConvertTo-Json -Depth 5|Set-Content (Join-Path $OutputDirectory 'layout-transforms.json') -Encoding utf8
