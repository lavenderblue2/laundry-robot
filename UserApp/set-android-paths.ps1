# Android Build Environment - Path Setup Script
# Automatically finds JDK and Android SDK and sets environment variables

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Android Build Environment - Path Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Searching for JDK and Android SDK installations..." -ForegroundColor Yellow
Write-Host ""

# ============================================
# Find JDK Installation
# ============================================
Write-Host "Looking for JDK 17..." -ForegroundColor Cyan

$jdkPaths = @(
    "C:\Program Files\Eclipse Adoptium\jdk-17*",
    "C:\Program Files\OpenJDK\jdk-17*",
    "C:\Program Files\Java\jdk-17*",
    "C:\Program Files (x86)\Eclipse Adoptium\jdk-17*",
    "C:\Program Files (x86)\OpenJDK\jdk-17*"
)

$jdkDir = $null
foreach ($path in $jdkPaths) {
    $found = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) {
        $jdkDir = $found.FullName
        break
    }
}

if (-not $jdkDir) {
    Write-Host "[ERROR] JDK 17 not found!" -ForegroundColor Red
    Write-Host "Please install OpenJDK 17 from: https://adoptium.net/" -ForegroundColor Yellow
    Write-Host "Make sure to use the .msi installer" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Verify java.exe exists
if (-not (Test-Path "$jdkDir\bin\java.exe")) {
    Write-Host "[ERROR] java.exe not found in: $jdkDir\bin\" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "[OK] JDK found at: $jdkDir" -ForegroundColor Green

# ============================================
# Find Android SDK Installation
# ============================================
Write-Host "Looking for Android SDK..." -ForegroundColor Cyan

$androidSdkPaths = @(
    "$env:LOCALAPPDATA\Android\Sdk",
    "$env:APPDATA\..\Local\Android\Sdk",
    "C:\Android\Sdk",
    "$env:ProgramFiles\Android\Sdk",
    "${env:ProgramFiles(x86)}\Android\Sdk"
)

$androidSdkDir = $null
foreach ($path in $androidSdkPaths) {
    if (Test-Path $path) {
        $androidSdkDir = (Resolve-Path $path).Path
        break
    }
}

if (-not $androidSdkDir) {
    Write-Host "[ERROR] Android SDK not found!" -ForegroundColor Red
    Write-Host "Please open Android Studio and complete the SDK installation" -ForegroundColor Yellow
    Write-Host "Then run this script again" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

# Verify platform-tools exists
if (-not (Test-Path "$androidSdkDir\platform-tools")) {
    Write-Host "[WARNING] Android SDK found but platform-tools missing!" -ForegroundColor Yellow
    Write-Host "Please open Android Studio SDK Manager and install:" -ForegroundColor Yellow
    Write-Host "  - Android SDK Platform-Tools" -ForegroundColor White
    Write-Host "  - Android SDK Build-Tools" -ForegroundColor White
    Write-Host "  - Android 14.0 (API 34)" -ForegroundColor White
    Read-Host "Press Enter to continue anyway"
}

Write-Host "[OK] Android SDK found at: $androidSdkDir" -ForegroundColor Green

# ============================================
# Set Environment Variables
# ============================================
Write-Host ""
Write-Host "Setting environment variables..." -ForegroundColor Cyan

try {
    # Set JAVA_HOME
    [System.Environment]::SetEnvironmentVariable("JAVA_HOME", $jdkDir, [System.EnvironmentVariableTarget]::Machine)
    Write-Host "[OK] JAVA_HOME = $jdkDir" -ForegroundColor Green

    # Set ANDROID_HOME
    [System.Environment]::SetEnvironmentVariable("ANDROID_HOME", $androidSdkDir, [System.EnvironmentVariableTarget]::Machine)
    Write-Host "[OK] ANDROID_HOME = $androidSdkDir" -ForegroundColor Green

    # Set ANDROID_SDK_ROOT (some tools use this instead)
    [System.Environment]::SetEnvironmentVariable("ANDROID_SDK_ROOT", $androidSdkDir, [System.EnvironmentVariableTarget]::Machine)
    Write-Host "[OK] ANDROID_SDK_ROOT = $androidSdkDir" -ForegroundColor Green

    # Update PATH
    $currentPath = [System.Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)

    $pathsToAdd = @(
        "$jdkDir\bin",
        "$androidSdkDir\platform-tools",
        "$androidSdkDir\tools",
        "$androidSdkDir\tools\bin",
        "$androidSdkDir\cmdline-tools\latest\bin",
        "$androidSdkDir\emulator"
    )

    $pathUpdated = $false
    foreach ($pathToAdd in $pathsToAdd) {
        if (Test-Path $pathToAdd) {
            if ($currentPath -notlike "*$pathToAdd*") {
                $currentPath = "$currentPath;$pathToAdd"
                $pathUpdated = $true
                Write-Host "[OK] Added to PATH: $pathToAdd" -ForegroundColor Green
            }
            else {
                Write-Host "[SKIP] Already in PATH: $pathToAdd" -ForegroundColor Gray
            }
        }
    }

    if ($pathUpdated) {
        [System.Environment]::SetEnvironmentVariable("Path", $currentPath, [System.EnvironmentVariableTarget]::Machine)
        Write-Host "[OK] PATH updated successfully" -ForegroundColor Green
    }
}
catch {
    Write-Host "[ERROR] Failed to set environment variables: $_" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# ============================================
# Create gradle.properties for better performance
# ============================================
Write-Host ""
Write-Host "Creating gradle.properties..." -ForegroundColor Cyan

$gradlePropertiesPath = "$env:USERPROFILE\.gradle\gradle.properties"
$gradlePropertiesDir = Split-Path $gradlePropertiesPath -Parent

if (-not (Test-Path $gradlePropertiesDir)) {
    New-Item -ItemType Directory -Path $gradlePropertiesDir -Force | Out-Null
}

$gradleProperties = @"
# Gradle settings optimized for faster builds
org.gradle.jvmargs=-Xmx4096m -XX:MaxMetaspaceSize=512m -XX:+HeapDumpOnOutOfMemoryError
org.gradle.parallel=true
org.gradle.configureondemand=true
org.gradle.daemon=true

# Android settings
android.useAndroidX=true
android.enableJetifier=true
"@

Set-Content -Path $gradlePropertiesPath -Value $gradleProperties -Force
Write-Host "[OK] gradle.properties created at: $gradlePropertiesPath" -ForegroundColor Green

# ============================================
# Verify Installation
# ============================================
Write-Host ""
Write-Host "Verifying installation..." -ForegroundColor Cyan

# Refresh environment variables in current session
$env:JAVA_HOME = $jdkDir
$env:ANDROID_HOME = $androidSdkDir
$env:PATH = "$env:PATH;$jdkDir\bin;$androidSdkDir\platform-tools"

# Test Java
try {
    $javaVersion = & "$jdkDir\bin\java.exe" -version 2>&1 | Select-Object -First 1
    Write-Host "[OK] Java version: $javaVersion" -ForegroundColor Green
}
catch {
    Write-Host "[WARNING] Could not verify Java installation" -ForegroundColor Yellow
}

# Test ADB (if platform-tools installed)
if (Test-Path "$androidSdkDir\platform-tools\adb.exe") {
    try {
        $adbVersion = & "$androidSdkDir\platform-tools\adb.exe" version 2>&1 | Select-Object -First 1
        Write-Host "[OK] ADB installed: $adbVersion" -ForegroundColor Green
    }
    catch {
        Write-Host "[WARNING] Could not verify ADB installation" -ForegroundColor Yellow
    }
}

# ============================================
# Summary
# ============================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Environment Variables Set:" -ForegroundColor Yellow
Write-Host "  JAVA_HOME        = $jdkDir" -ForegroundColor White
Write-Host "  ANDROID_HOME     = $androidSdkDir" -ForegroundColor White
Write-Host "  ANDROID_SDK_ROOT = $androidSdkDir" -ForegroundColor White
Write-Host ""
Write-Host "IMPORTANT NEXT STEPS:" -ForegroundColor Red
Write-Host "1. RESTART YOUR COMPUTER (or at least logout/login)" -ForegroundColor Yellow
Write-Host "   Environment variables won't work until you restart!" -ForegroundColor Yellow
Write-Host ""
Write-Host "2. After restart, open PowerShell and run:" -ForegroundColor Yellow
Write-Host "   cd E:\THESIS\laundry-robot\UserApp\laundry-app" -ForegroundColor White
Write-Host "   npx expo prebuild" -ForegroundColor White
Write-Host "   cd android" -ForegroundColor White
Write-Host "   .\gradlew assembleRelease" -ForegroundColor White
Write-Host ""
Write-Host "3. Your APK will be at:" -ForegroundColor Yellow
Write-Host "   android\app\build\outputs\apk\release\app-release.apk" -ForegroundColor White
Write-Host ""

Read-Host "Press Enter to exit"
