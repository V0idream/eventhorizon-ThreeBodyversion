param(
    [switch]$SkipBuild,
    [string]$SdkRoot = "$env:LOCALAPPDATA\Unity\AndroidPlayer\SDK",
    [string]$AvdName = 'EventHorizon_API_33'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$env:ANDROID_SDK_ROOT = $SdkRoot
$env:JAVA_HOME = "$env:LOCALAPPDATA\Unity\AndroidPlayer\OpenJDK"

if (-not $SkipBuild) {
    & (Join-Path $projectRoot 'tools\build-android.ps1')
}

$adb = Join-Path $SdkRoot 'platform-tools\adb.exe'
$emulator = Join-Path $SdkRoot 'emulator\emulator.exe'
if (-not (Test-Path -LiteralPath $adb) -or -not (Test-Path -LiteralPath $emulator)) {
    throw 'Android platform-tools or emulator were not found.'
}

$onlineDevice = & $adb devices | Select-String '\sdevice$'
if (-not $onlineDevice) {
    Start-Process -FilePath $emulator -ArgumentList @('-avd', $AvdName, '-no-snapshot-save')
    & $adb wait-for-device
    do {
        Start-Sleep -Seconds 2
        $bootComplete = (& $adb shell getprop sys.boot_completed 2>$null).Trim()
    } until ($bootComplete -eq '1')
}

$apk = Join-Path $projectRoot 'Builds\Android\EventHorizon-ThreeBody-debug.apk'
& $adb install -r $apk
if ($LASTEXITCODE -ne 0) {
    throw 'APK installation failed.'
}

& $adb shell monkey -p com.eventhorizon.threebody -c android.intent.category.LAUNCHER 1
