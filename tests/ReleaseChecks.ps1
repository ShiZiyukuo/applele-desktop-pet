param([string]$PublishDirectory='app',[string]$ScratchDirectory='',[string]$ReportPath='docs/release-failure-checks.json')
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
if(-not [IO.Path]::IsPathRooted($PublishDirectory)){$PublishDirectory=Join-Path $project $PublishDirectory}
if(-not [IO.Path]::IsPathRooted($ReportPath)){$ReportPath=Join-Path $project $ReportPath}
$application=Join-Path $PublishDirectory 'Applele.exe'
$root=if($ScratchDirectory){[IO.Path]::GetFullPath($ScratchDirectory)}else{Join-Path ([IO.Path]::GetTempPath()) ('Applele-checks-'+[guid]::NewGuid().ToString('N'))}
New-Item -ItemType Directory -Path $root -Force | Out-Null
$cases=@('missing-sprite','corrupt-config','opaque-image')
$results=@()
Add-Type -AssemblyName System.Drawing
foreach($name in $cases) {
    $folder=Join-Path $root $name
    New-Item -ItemType Directory -Path (Join-Path $folder 'config') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $project 'config/pet.json') -Destination (Join-Path $folder 'config/pet.json') -Force
    if($name -eq 'corrupt-config') {Set-Content -LiteralPath (Join-Path $folder 'config/pet.json') -Value '{invalid-json' -Encoding utf8}
    if($name -eq 'opaque-image') {
        $bitmap=New-Object Drawing.Bitmap(32,32)
        $graphics=[Drawing.Graphics]::FromImage($bitmap)
        try{$graphics.Clear([Drawing.Color]::White);$bitmap.Save((Join-Path $folder 'opaque.png'),[Drawing.Imaging.ImageFormat]::Png)}finally{$graphics.Dispose();$bitmap.Dispose()}
        $configuration=Get-Content -LiteralPath (Join-Path $folder 'config/pet.json') -Raw|ConvertFrom-Json
        $configuration.forms[0].idle.file='opaque.png'
        $configuration|ConvertTo-Json -Depth 30|Set-Content -LiteralPath (Join-Path $folder 'config/pet.json') -Encoding utf8
    }
    $pet=Start-Process -FilePath $application -ArgumentList '--check','--resource-root',('"'+$folder+'"') -WindowStyle Hidden -PassThru
    if(-not $pet.WaitForExit(10000)){$pet.Kill();throw ('Failure case hung: '+$name)}
    $errorReport=Get-Content -LiteralPath (Join-Path $folder 'startup-error.json') -Raw|ConvertFrom-Json
    $expected=switch($name){'missing-sprite'{'FileNotFoundException'} 'corrupt-config'{'JsonException'} 'opaque-image'{'InvalidDataException'}}
    if($pet.ExitCode -ne 1 -or $errorReport.error -ne $expected){throw ('Incorrect failure handling: '+$name)}
    $results+=[ordered]@{case=$name;passed=$true;exitCode=$pet.ExitCode;error=$errorReport.error;message=$errorReport.Message;processExited=$pet.HasExited}
}
$blocked=Join-Path $root 'blocked-error-log'
New-Item -ItemType Directory -Path (Join-Path $blocked 'config') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $blocked 'startup-error.json') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $project 'config/pet.json') -Destination (Join-Path $blocked 'config/pet.json') -Force
$pet=Start-Process -FilePath $application -ArgumentList '--check','--resource-root',('"'+$blocked+'"') -WindowStyle Hidden -PassThru
if(-not $pet.WaitForExit(10000)){$pet.Kill();throw 'Unwritable log failure case hung'}
if($pet.ExitCode -ne 1){throw 'Unwritable log prevented orderly error exit'}
$results+=[ordered]@{case='blocked-error-log';passed=$true;exitCode=$pet.ExitCode;error='diagnostic path intentionally blocked by a directory';processExited=$pet.HasExited}
$results|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $ReportPath -Encoding utf8
$results|ConvertTo-Json -Depth 8

