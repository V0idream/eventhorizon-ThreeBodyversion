param(
    [string]$SdkRoot = "$env:LOCALAPPDATA\Unity\AndroidPlayer\SDK",
    [string]$AvdName = 'EventHorizon_API_33'
)

$ErrorActionPreference = 'Stop'
$env:ANDROID_SDK_ROOT = $SdkRoot
$env:JAVA_HOME = "$env:LOCALAPPDATA\Unity\AndroidPlayer\OpenJDK"
$sdkManager = Get-ChildItem $SdkRoot -Filter sdkmanager.bat -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName
$avdManager = Get-ChildItem $SdkRoot -Filter avdmanager.bat -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $sdkManager -or -not $avdManager) {
    throw 'Android SDK command-line tools were not found. Open Android Studio once and install Android SDK Command-line Tools.'
}

$licenses = (1..20 | ForEach-Object { 'y' }) -join [Environment]::NewLine
$licenses | & $sdkManager --licenses | Out-Host
& $sdkManager 'platform-tools' 'emulator' 'platforms;android-33' 'system-images;android-33;google_apis;x86_64'
if ($LASTEXITCODE -ne 0) {
    throw 'Android SDK package installation failed.'
}

$existing = & $avdManager list avd
if ($existing -notmatch [regex]::Escape("Name: $AvdName")) {
    'no' | & $avdManager create avd --force --name $AvdName --package 'system-images;android-33;google_apis;x86_64' --device 'pixel_6'
    if ($LASTEXITCODE -ne 0) {
        throw 'Android virtual device creation failed.'
    }
}

Write-Output "Android virtual device ready: $AvdName"
