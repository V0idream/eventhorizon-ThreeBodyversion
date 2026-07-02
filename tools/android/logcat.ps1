param(
    [string]$SdkRoot = "$env:LOCALAPPDATA\Unity\AndroidPlayer\SDK"
)

$ErrorActionPreference = 'Stop'
$adb = Join-Path $SdkRoot 'platform-tools\adb.exe'
if (-not (Test-Path -LiteralPath $adb)) {
    throw 'adb.exe was not found.'
}

& $adb logcat -s Unity ActivityManager AndroidRuntime
