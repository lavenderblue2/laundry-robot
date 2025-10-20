# Android Build Environment Setup Script
# This script downloads and configures everything needed to build Android APKs

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Android Build Environment Setup" -ForegroundColor Cyan
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

# Set installation directory
$installDir = "E:\THESIS\AndroidBuildTools"
$jdkDir = "$installDir\jdk-17"
$androidSdkDir = "$installDir\android-sdk"

Write-Host "Installation directory: $installDir" -ForegroundColor Green
Write-Host ""

# Create installation directory
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
    Write-Host "[OK] Created installation directory" -ForegroundColor Green
}

# ============================================
# Step 1: Download and Install JDK 17
# ============================================
Write-Host ""
Write-Host "Step 1: Installing JDK 17..." -ForegroundColor Cyan

$jdkUrl = "https://aka.ms/download-jdk/microsoft-jdk-17.0.12-windows-x64.zip"
$jdkZip = "$installDir\jdk-17.zip"

if (-not (Test-Path $jdkDir)) {
    Write-Host "Downloading JDK 17 (approx 175 MB)..." -ForegroundColor Yellow
    try {
        # Use BITS transfer for reliable download with progress
        Import-Module BitsTransfer
        Start-BitsTransfer -Source $jdkUrl -Destination $jdkZip -Description "Downloading JDK 17"

        Write-Host "Extracting JDK..." -ForegroundColor Yellow
        Expand-Archive -Path $jdkZip -DestinationPath $installDir -Force

        # Find the extracted JDK folder (it has version number in name)
        $extractedJdk = Get-ChildItem -Path $installDir -Directory | Where-Object { $_.Name -like "jdk-17*" } | Select-Object -First 1
        if ($extractedJdk) {
            Rename-Item -Path $extractedJdk.FullName -NewName "jdk-17" -Force
        }

        Remove-Item $jdkZip -Force
        Write-Host "[OK] JDK 17 installed successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "[ERROR] Failed to download/install JDK: $_" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "[OK] JDK 17 already installed" -ForegroundColor Green
}

# ============================================
# Step 2: Download and Install Android SDK Command Line Tools
# ============================================
Write-Host ""
Write-Host "Step 2: Installing Android SDK..." -ForegroundColor Cyan

$cmdlineToolsUrl = "https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip"
$cmdlineToolsZip = "$installDir\cmdline-tools.zip"

if (-not (Test-Path $androidSdkDir)) {
    New-Item -ItemType Directory -Path $androidSdkDir -Force | Out-Null
}

$cmdlineToolsDir = "$androidSdkDir\cmdline-tools\latest"

if (-not (Test-Path $cmdlineToolsDir)) {
    Write-Host "Downloading Android SDK Command Line Tools (approx 150 MB)..." -ForegroundColor Yellow
    try {
        Start-BitsTransfer -Source $cmdlineToolsUrl -Destination $cmdlineToolsZip -Description "Downloading Android SDK Tools"

        Write-Host "Extracting Android SDK Tools..." -ForegroundColor Yellow
        Expand-Archive -Path $cmdlineToolsZip -DestinationPath "$androidSdkDir\temp" -Force

        # Move to correct location
        New-Item -ItemType Directory -Path "$androidSdkDir\cmdline-tools" -Force | Out-Null
        Move-Item -Path "$androidSdkDir\temp\cmdline-tools" -Destination "$androidSdkDir\cmdline-tools\latest" -Force
        Remove-Item "$androidSdkDir\temp" -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item $cmdlineToolsZip -Force

        Write-Host "[OK] Android SDK Command Line Tools installed" -ForegroundColor Green
    }
    catch {
        Write-Host "[ERROR] Failed to download/install Android SDK Tools: $_" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "[OK] Android SDK Command Line Tools already installed" -ForegroundColor Green
}

# ============================================
# Step 3: Install Required Android SDK Components
# ============================================
Write-Host ""
Write-Host "Step 3: Installing Android SDK components..." -ForegroundColor Cyan

$env:JAVA_HOME = $jdkDir
$env:ANDROID_HOME = $androidSdkDir
$sdkmanager = "$cmdlineToolsDir\bin\sdkmanager.bat"

if (Test-Path $sdkmanager) {
    Write-Host "Installing Android SDK Platform 34..." -ForegroundColor Yellow

    # Accept licenses first
    Write-Host "y" | & $sdkmanager --licenses 2>&1 | Out-Null

    # Install required components
    $components = @(
        "platform-tools",
        "platforms;android-34",
        "build-tools;34.0.0",
        "ndk;25.1.8937393"
    )

    foreach ($component in $components) {
        Write-Host "Installing $component..." -ForegroundColor Yellow
        & $sdkmanager $component --sdk_root=$androidSdkDir 2>&1 | Out-Null
    }

    Write-Host "[OK] Android SDK components installed" -ForegroundColor Green
}
else {
    Write-Host "[ERROR] SDK Manager not found!" -ForegroundColor Red
    exit 1
}

# ============================================
# Step 4: Set Environment Variables
# ============================================
Write-Host ""
Write-Host "Step 4: Setting environment variables..." -ForegroundColor Cyan

try {
    # Set JAVA_HOME
    [System.Environment]::SetEnvironmentVariable("JAVA_HOME", $jdkDir, [System.EnvironmentVariableTarget]::Machine)
    Write-Host "[OK] JAVA_HOME set to: $jdkDir" -ForegroundColor Green

    # Set ANDROID_HOME
    [System.Environment]::SetEnvironmentVariable("ANDROID_HOME", $androidSdkDir, [System.EnvironmentVariableTarget]::Machine)
    Write-Host "[OK] ANDROID_HOME set to: $androidSdkDir" -ForegroundColor Green

    # Update PATH
    $currentPath = [System.Environment]::GetEnvironmentVariable("Path", [System.EnvironmentVariableTarget]::Machine)

    $pathsToAdd = @(
        "$jdkDir\bin",
        "$androidSdkDir\platform-tools",
        "$androidSdkDir\cmdline-tools\latest\bin",
        "$androidSdkDir\emulator"
    )

    foreach ($pathToAdd in $pathsToAdd) {
        if ($currentPath -notlike "*$pathToAdd*") {
            $currentPath = "$currentPath;$pathToAdd"
        }
    }

    [System.Environment]::SetEnvironmentVariable("Path", $currentPath, [System.EnvironmentVariableTarget]::Machine)
    Write-Host "[OK] PATH updated" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to set environment variables: $_" -ForegroundColor Red
    exit 1
}

# ============================================
# Step 5: Create gradle.properties
# ============================================
Write-Host ""
Write-Host "Step 5: Creating gradle.properties..." -ForegroundColor Cyan

$gradlePropertiesPath = "$env:USERPROFILE\.gradle\gradle.properties"
$gradlePropertiesDir = Split-Path $gradlePropertiesPath -Parent

if (-not (Test-Path $gradlePropertiesDir)) {
    New-Item -ItemType Directory -Path $gradlePropertiesDir -Force | Out-Null
}

$gradleProperties = @"
org.gradle.jvmargs=-Xmx4096m -XX:MaxMetaspaceSize=512m -XX:+HeapDumpOnOutOfMemoryError
org.gradle.parallel=true
org.gradle.configureondemand=true
org.gradle.daemon=true
android.useAndroidX=true
android.enableJetifier=true
"@

Set-Content -Path $gradlePropertiesPath -Value $gradleProperties -Force
Write-Host "[OK] gradle.properties created" -ForegroundColor Green

# ============================================
# Summary
# ============================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Environment Variables Set:" -ForegroundColor Yellow
Write-Host "  JAVA_HOME    = $jdkDir" -ForegroundColor White
Write-Host "  ANDROID_HOME = $androidSdkDir" -ForegroundColor White
Write-Host ""
Write-Host "IMPORTANT: You must restart your terminal/IDE for changes to take effect!" -ForegroundColor Red
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Close this PowerShell window" -ForegroundColor White
Write-Host "2. Close VS Code / Terminal" -ForegroundColor White
Write-Host "3. Open a NEW PowerShell window" -ForegroundColor White
Write-Host "4. Navigate to: E:\THESIS\laundry-robot\UserApp\laundry-app" -ForegroundColor White
Write-Host "5. Run: npx expo prebuild" -ForegroundColor White
Write-Host "6. Run: cd android && .\gradlew assembleRelease" -ForegroundColor White
Write-Host ""
Write-Host "Your APK will be in: android\app\build\outputs\apk\release\app-release.apk" -ForegroundColor Green
Write-Host ""

Read-Host "Press Enter to exit"
