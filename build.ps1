$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outputDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
$sourceFiles = @((Join-Path $projectRoot 'src\LanguageSwitch.cs'), (Join-Path $projectRoot 'src\Program.cs'), (Join-Path $projectRoot 'src\AssemblyInfo.cs'))
$manifestArgument = '/win32manifest:' + (Join-Path $projectRoot 'src\app.manifest')
& $compilerPath /nologo /target:winexe /platform:anycpu /optimize+ $manifestArgument /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ('/out:' + (Join-Path $outputDir 'HancomLanguageSwitch.exe')) $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
# Console build exposes exactly the same implementation for verification.
& $compilerPath /nologo /target:exe /platform:anycpu /optimize+ $manifestArgument /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ('/out:' + (Join-Path $outputDir 'HancomLanguageSwitch.Cli.exe')) $sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'CLI build failed.' }
Get-FileHash -LiteralPath (Join-Path $outputDir 'HancomLanguageSwitch.exe') -Algorithm SHA256
