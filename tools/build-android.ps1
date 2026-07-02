param(
    [string]$UnityEditor
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$androidTools = Join-Path $env:LOCALAPPDATA 'Unity\AndroidPlayer'
$env:JAVA_HOME = Join-Path $androidTools 'OpenJDK'
$env:ANDROID_SDK_ROOT = Join-Path $androidTools 'SDK'
$env:ANDROID_HOME = $env:ANDROID_SDK_ROOT
$env:ANDROID_NDK_ROOT = Join-Path $env:ANDROID_SDK_ROOT 'ndk\27.2.12479018'

if (-not $UnityEditor) {
    $UnityEditor = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Filter Unity.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '6000\.0\.75f1' } |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $UnityEditor -or -not (Test-Path -LiteralPath $UnityEditor)) {
    throw 'Unity 6000.0.75f1 was not found. Install it with Android Build Support first.'
}

$logPath = Join-Path $projectRoot 'Builds\Android\unity-build.log'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $logPath) | Out-Null

& $UnityEditor -batchmode -quit -projectPath $projectRoot `
    -executeMethod AndroidDevelopmentBuild.Build `
    -logFile $logPath

if ($LASTEXITCODE -ne 0) {
    throw "Unity Android build failed. See $logPath"
}

$apk = Join-Path $projectRoot 'Builds\Android\ThreeBody-EventHorizon-debug.apk'
if (-not (Test-Path -LiteralPath $apk)) {
    throw "Build completed without producing $apk"
}

Get-Item -LiteralPath $apk
