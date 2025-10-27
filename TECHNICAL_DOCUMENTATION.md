# TECHNICAL DOCUMENTATION
## Autonomous Laundry Collection Robot System

**Project:** Thesis Project - Complete autonomous service robotics system
**Location:** E:\THESIS\laundry-robot
**Version:** 1.0.0

---

## TABLE OF CONTENTS

1. [System Overview](#system-overview)
2. [Tech Stack Summary](#tech-stack-summary)
3. [Web Application (AdministratorWeb)](#web-application)
4. [Robot Controller (LineFollowerRobot)](#robot-controller)
5. [Mobile Application (UserApp)](#mobile-application)
6. [Database](#database)
7. [Hardware Components](#hardware-components)
8. [Quick Start Guide](#quick-start-guide)

---

## SYSTEM OVERVIEW

This system consists of **4 main components**:

| Component | Technology | Purpose |
|-----------|-----------|---------|
| **Web Application** | ASP.NET Core 8 MVC + Web API | Admin dashboard, API backend, request management |
| **Robot Controller** | .NET 8 Console (ARM64) | Autonomous robot control on Raspberry Pi 5 |
| **Mobile App** | React Native/Expo | Customer-facing mobile application |
| **Database** | MySQL 8.0+ | Data persistence |

---

## TECH STACK SUMMARY

### Web Application
- **Framework:** ASP.NET Core 8.0 (MVC + Web API)
- **ORM:** Entity Framework Core 8.0
- **Authentication:** ASP.NET Identity + JWT Bearer
- **Database Driver:** Pomelo.EntityFrameworkCore.MySql 8.0.2
- **View Engine:** Razor (.cshtml)
- **Language:** C# 12

### Robot Controller
- **Runtime:** .NET 8.0
- **Platform:** Raspberry Pi 5 (Linux ARM64)
- **GPIO Library:** System.Device.Gpio 4.0.1
- **Computer Vision:** SixLabors.ImageSharp 3.1.11
- **Video Processing:** FFMpegCore 5.2.0
- **Bluetooth:** Linux.Bluetooth 6.0.0-pre2
- **QR Detection:** ZXing.Net 0.16.10
- **Language:** C# 12

### Mobile Application
- **Framework:** React Native 0.79.5
- **Build Tool:** Expo SDK 53
- **Language:** TypeScript 5.8.3
- **Navigation:** Expo Router 5.1.5
- **HTTP Client:** Axios 1.11.0
- **Storage:** AsyncStorage + Expo Secure Store
- **UI:** React Native SVG, Lucide Icons, Expo Vector Icons

### Database
- **Type:** MySQL 8.0+
- **Management:** Entity Framework Core Migrations
- **Connection:** Remote server at 140.245.47.120:3306

---

## WEB APPLICATION

### What It Is
ASP.NET Core 8 MVC application with Web API backend. Provides admin dashboard for managing laundry requests, robots, users, payments, and system settings.

### Tech Stack
```
AdministratorWeb/
├── Language: C# 12
├── Framework: ASP.NET Core 8.0
├── Pattern: MVC + Web API
├── ORM: Entity Framework Core 8.0
├── Database: MySQL 8.0 (via Pomelo)
├── Authentication: ASP.NET Identity + JWT
└── Views: Razor (.cshtml)
```

### Key Dependencies
```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.16" />
<PackageReference Include="Pomelo.EntityFrameworkCore.MySql" Version="8.0.2" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.16" />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.4-beta1" />
```

### Structure
```
AdministratorWeb/
├── Controllers/
│   ├── API/ (7 controllers)
│   │   ├── AuthController - JWT authentication
│   │   ├── RobotController - Robot API endpoints
│   │   ├── RequestsController - Request management API
│   │   ├── PaymentController - Payment processing
│   │   ├── MessagesController - Customer messaging
│   │   └── UserController - User management
│   └── MVC/ (9 controllers)
│       ├── DashboardController - Admin dashboard
│       ├── RequestsController - Request management UI
│       ├── RobotsController - Robot management UI
│       ├── AccountingController - Payment/accounting UI
│       └── ...more
├── Services/ (5 services)
│   ├── JwtTokenService - Token generation
│   ├── NotificationService - Push notifications
│   ├── RobotManagementService - Robot fleet management
│   ├── RequestTimeoutService - Request timeout handling
│   └── OrphanedRequestCleanupService - Cleanup service
├── Models/ (25+ models)
│   ├── ApplicationUser, LaundryRequest, LaundryRobot
│   ├── BluetoothBeacon, Payment, Message
│   └── DTOs (10+ DTOs for API communication)
├── Data/
│   ├── ApplicationDbContext - EF Core context
│   └── DbSeeder - Database seeding
├── Views/ (40+ .cshtml files)
│   ├── Dashboard/, Requests/, Robots/, Accounting/
│   └── Shared/ - Layout and partials
├── Migrations/ (20+ migration files)
├── wwwroot/ - Static assets (CSS, JS, images)
└── appsettings.json - Configuration
```

### Key Features
- **Dashboard:** Real-time robot status, request statistics
- **Request Management:** Full lifecycle tracking (13+ states)
- **Robot Fleet Management:** Monitor and control multiple robots
- **User Management:** Customer and admin user management
- **Accounting System:** Payment tracking and financial reports
- **Beacon Management:** Room location configuration
- **Messaging:** Admin-customer communication
- **Settings:** System-wide configuration (pricing, operating hours)

### API Endpoints

#### Robot API
```
POST   /api/robot/{name}/register       - Robot self-registration
POST   /api/robot/{name}/ping           - Heartbeat/status update
POST   /api/robot/{name}/data-exchange  - Bidirectional data sync (1Hz)
GET    /api/robot/{name}/status         - Get commands from server
```

#### Mobile App API
```
POST   /api/auth/login                  - User authentication
POST   /api/requests                    - Create laundry request
GET    /api/requests/active             - Get active request
GET    /api/requests/my-requests        - Get request history
PUT    /api/requests/{id}/confirm       - Confirm pickup/delivery
```

### Configuration (appsettings.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=140.245.47.120;Port=3306;Database=laundry;User=root;Password=***"
  },
  "Jwt": {
    "Key": "***",
    "Issuer": "LaundryAdministrator",
    "Audience": "LaundryMobileApp",
    "ExpirationHours": 24
  },
  "LaundrySettings": {
    "RatePerKg": 25.00,
    "MinimumCharge": 50.00
  }
}
```

### How to Build

#### Prerequisites
- .NET 8.0 SDK
- MySQL 8.0+ Server
- Visual Studio 2022 or VS Code

#### Build Steps
```bash
# Navigate to project directory
cd E:\THESIS\laundry-robot\AdministratorWeb

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Build for production (Release mode)
dotnet build --configuration Release
```

#### Publish for Deployment
```bash
# Publish for Windows (x64)
dotnet publish -c Release -r win-x64 --self-contained false -o publish/web

# Publish for Linux (ARM64) - for deployment alongside robot
dotnet publish -c Release -r linux-arm64 --self-contained true -o publish/web-linux
```

### How to Run

#### Development Mode
```bash
# Run with hot reload
dotnet watch run

# Or run normally
dotnet run

# Access at: https://localhost:5001 or http://localhost:5000
```

#### Production Mode
```bash
# Run the published build
cd publish/web
dotnet AdministratorWeb.dll

# With custom port
dotnet AdministratorWeb.dll --urls "http://0.0.0.0:8080;https://0.0.0.0:8443"
```

#### Database Setup
```bash
# Apply migrations
dotnet ef database update

# Or use SQL script
mysql -h 140.245.47.120 -u root -p laundry < schema.sql
```

#### Environment Variables (Production)
```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Server=140.245.47.120;Port=3306;Database=laundry;User=root;Password=***"
export Jwt__Key="your-secret-key"
```

---

## ROBOT CONTROLLER

### What It Is
.NET 8 console application running on Raspberry Pi 5 (Linux ARM64). Controls robot hardware, performs line following, beacon navigation, and communicates with server.

### Tech Stack
```
LineFollowerRobot/
├── Language: C# 12
├── Runtime: .NET 8.0 ARM64
├── Platform: Raspberry Pi 5 (Linux)
├── GPIO: System.Device.Gpio 4.0.1
├── Vision: SixLabors.ImageSharp 3.1.11
├── Video: FFMpegCore 5.2.0
├── Bluetooth: Linux.Bluetooth 6.0.0-pre2
└── QR Codes: ZXing.Net 0.16.10
```

### Key Dependencies
```xml
<PackageReference Include="System.Device.Gpio" Version="4.0.1" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.11" />
<PackageReference Include="FFMpegCore" Version="5.2.0" />
<PackageReference Include="Linux.Bluetooth" Version="6.0.0-pre2" />
<PackageReference Include="ZXing.Net" Version="0.16.10" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.4-beta1" />
```

### Structure
```
LineFollowerRobot/
├── Services/ (13+ services)
│   ├── LineFollowerService - Main line-following logic with PID
│   ├── LineFollowerMotorService - Motor control
│   ├── LineDetectionCameraService - Camera feed processing
│   ├── BluetoothBeaconService - BLE beacon detection (RSSI-based)
│   ├── BluetoothBeaconHostedService - Background beacon scanning
│   ├── CommandPollingService - Poll commands from server
│   ├── RobotServerCommunicationService - API communication (1Hz)
│   ├── RobotRegistrationService - Self-registration on startup
│   ├── RobotImageUploadService - Upload camera feed
│   ├── UltrasonicSensorService - Obstacle detection
│   ├── Hx711Service - Weight sensor integration
│   ├── HeadlightService - LED control
│   └── CameraStreamService - Stream camera feed
├── Controllers/
│   ├── CameraController - Camera management
│   └── MotorController - Motor control interface
├── CameraMiddleware/ - Image processing pipeline
├── Interfaces/ - Service contracts
└── appsettings.json - Robot configuration
```

### Key Features
- **Line Following:** Binary + adaptive thresholding with PID control
- **Beacon Navigation:** Bluetooth LE RSSI-based room detection
- **Obstacle Avoidance:** HC-SR04 ultrasonic sensors (front/rear)
- **Weight Measurement:** HX711 load cell (0.0001kg precision)
- **Camera Feed:** Real-time image upload to server
- **QR Detection:** WiFi network auto-configuration
- **Remote Control:** Emergency stop, headlights, maintenance modes
- **Heartbeat:** 1Hz data exchange with server

### Configuration (appsettings.json)
```json
{
  "Robot": {
    "Name": "Robot-42",
    "ApiServer": "https://laundry.nexusph.site/",
    "DataExchangeIntervalMs": 1000,
    "ImageUploadEnabled": true
  },
  "LineFollower": {
    "Camera": {
      "Width": 320,
      "Height": 240,
      "FPS": 5
    },
    "PID": {
      "Kp": 0.2,
      "Ki": 0.0,
      "Kd": 0.05
    },
    "GPIO": {
      "Motors": {
        "FrontLeftPin1": 5, "FrontLeftPin2": 6,
        "FrontRightPin1": 19, "FrontRightPin2": 26,
        "BackLeftPin1": 16, "BackLeftPin2": 20,
        "BackRightPin1": 13, "BackRightPin2": 21
      },
      "Ultrasonic": {
        "TrigPin": 17,
        "EchoPin": 27
      },
      "WeightSensor": {
        "DataPin": 18,
        "ClockPin": 23
      }
    }
  },
  "BluetoothBeacon": {
    "DefaultRssiThreshold": -40,
    "ScanIntervalMs": 100
  }
}
```

### How to Build

#### Prerequisites
- .NET 8.0 SDK (on development machine)
- Raspberry Pi 5 with Raspberry Pi OS (64-bit)
- Camera module or USB camera
- Hardware components (motors, sensors, Bluetooth adapter)

#### Build Steps (on Windows/Linux)
```bash
# Navigate to robot project
cd E:\THESIS\laundry-robot\LineFollowerRobot

# Restore dependencies
dotnet restore

# Build for ARM64 (Raspberry Pi 5)
dotnet build -r linux-arm64
```

#### Publish for Raspberry Pi
```bash
# Self-contained build (includes .NET runtime)
dotnet publish -c Release -r linux-arm64 --self-contained true -o publish/robot

# Framework-dependent build (requires .NET runtime on Pi)
dotnet publish -c Release -r linux-arm64 --self-contained false -o publish/robot
```

#### Transfer to Raspberry Pi
```bash
# Using SCP
scp -r publish/robot pi@192.168.1.100:/home/pi/laundry-robot/

# Or use WinSCP/FileZilla for GUI transfer
```

### How to Run

#### On Raspberry Pi
```bash
# SSH into Raspberry Pi
ssh pi@192.168.1.100

# Navigate to robot directory
cd /home/pi/laundry-robot

# Make executable
chmod +x LineFollowerRobot

# Run the robot
sudo ./LineFollowerRobot

# Note: sudo is required for GPIO access
```

#### Run as System Service (Auto-start on boot)
```bash
# Create systemd service file
sudo nano /etc/systemd/system/laundry-robot.service
```

**Service file content:**
```ini
[Unit]
Description=Laundry Collection Robot
After=network.target

[Service]
Type=simple
User=root
WorkingDirectory=/home/pi/laundry-robot
ExecStart=/home/pi/laundry-robot/LineFollowerRobot
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
```

**Enable and start service:**
```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable service (auto-start on boot)
sudo systemctl enable laundry-robot

# Start service now
sudo systemctl start laundry-robot

# Check status
sudo systemctl status laundry-robot

# View logs
sudo journalctl -u laundry-robot -f
```

### Hardware Setup
See [Hardware Components](#hardware-components) section for wiring details.

---

## MOBILE APPLICATION

### What It Is
React Native mobile app built with Expo SDK 53. Customer-facing app for creating laundry requests, tracking robot progress, and managing account.

### Tech Stack
```
UserApp/laundry-app/
├── Language: TypeScript 5.8.3
├── Framework: React Native 0.79.5
├── Build Tool: Expo SDK 53
├── Navigation: Expo Router 5.1.5
├── HTTP: Axios 1.11.0
├── Storage: AsyncStorage + Secure Store
├── Notifications: Expo Notifications
└── UI: Lucide Icons, Expo Vector Icons
```

### Key Dependencies
```json
{
  "expo": "53.0.22",
  "react": "19.0.0",
  "react-native": "0.79.5",
  "expo-router": "~5.1.5",
  "@react-navigation/native": "^7.1.6",
  "@react-navigation/bottom-tabs": "^7.3.10",
  "@react-native-async-storage/async-storage": "2.1.2",
  "expo-secure-store": "~14.2.4",
  "axios": "^1.11.0",
  "expo-notifications": "~0.30.1",
  "lucide-react-native": "^0.542.0"
}
```

### Structure
```
UserApp/laundry-app/
├── app/ - Expo Router file-based routing
│   ├── (tabs)/ - Main tab navigation
│   │   ├── index.tsx - Home/Dashboard
│   │   ├── request.tsx - Create request form
│   │   ├── history.tsx - Request history
│   │   ├── profile.tsx - User profile
│   │   ├── support.tsx - Help/Support
│   │   └── _layout.tsx - Tab layout
│   ├── auth/
│   │   └── login.tsx - Login screen
│   ├── active-request.tsx - Active request tracking
│   ├── request-details.tsx - Request detail view
│   └── _layout.tsx - Root layout
├── services/ (6 services)
│   ├── api.ts - Axios HTTP client setup
│   ├── authService.ts - JWT authentication
│   ├── laundryService.ts - Request operations
│   ├── messageService.ts - Messaging API
│   ├── notificationService.ts - Push notifications
│   └── userService.ts - User profile management
├── components/ - Reusable UI components
│   ├── Collapsible, ExternalLink, HapticTab
│   ├── ParallaxScrollView, RobotAvailability
│   └── CustomAlert, HelloWave
├── contexts/ - React Context providers
├── hooks/ - Custom React hooks
├── constants/ - App constants
├── assets/ - Images, fonts, icons
└── app.json - Expo configuration
```

### Key Features
- **Tab Navigation:** Home, Request, History, Profile, Support
- **Real-time Tracking:** Track robot location and request status
- **Request Management:** Create, view, and confirm requests
- **Payment Processing:** Mark payments as complete
- **Push Notifications:** Real-time updates
- **Local Storage:** Persist auth tokens and data
- **Image Picker:** Upload photos (if needed)
- **Haptic Feedback:** Touch feedback

### Configuration (app.json)
```json
{
  "expo": {
    "name": "laundry-app",
    "slug": "laundry-app",
    "version": "1.0.0",
    "orientation": "portrait",
    "android": {
      "package": "com.laundry.laundry9",
      "edgeToEdgeEnabled": true
    },
    "plugins": [
      "expo-router",
      "expo-splash-screen",
      "expo-notifications"
    ]
  }
}
```

### How to Build

#### Prerequisites
- Node.js 18+ and npm
- Expo CLI (`npm install -g expo-cli`)
- For Android: Android Studio + Java JDK
- For iOS: macOS + Xcode (not applicable here)
- Expo Go app on phone (for development)

#### Setup Steps
```bash
# Navigate to mobile app directory
cd E:\THESIS\laundry-robot\UserApp\laundry-app

# Install dependencies
npm install

# Or using yarn
yarn install
```

#### Development Build
```bash
# Start Expo development server
npm start
# Or: npx expo start

# Options:
# - Press 'a' for Android
# - Press 'w' for Web
# - Scan QR code with Expo Go app
```

#### Production Build

**For Android APK:**
```bash
# Build APK (development build)
npx expo build:android

# Or use EAS Build (recommended)
npm install -g eas-cli
eas login
eas build --platform android

# Build locally with gradle (after prebuild)
npx expo prebuild
cd android
./gradlew assembleRelease

# APK location: android/app/build/outputs/apk/release/app-release.apk
```

**For Android AAB (Google Play):**
```bash
# Build AAB bundle
eas build --platform android --profile production

# Or locally
cd android
./gradlew bundleRelease
# AAB location: android/app/build/outputs/bundle/release/app-release.aab
```

### How to Run

#### Development Mode (Expo Go)
```bash
# Start development server
npm start

# Scan QR code with Expo Go app on your phone
# - Android: Expo Go app from Play Store
# - Changes auto-reload when you save files
```

#### Development Mode (Android Emulator)
```bash
# Start Android emulator in Android Studio first
# Then run:
npm run android
# Or: npx expo run:android
```

#### Production Mode (Install APK)
```bash
# Transfer APK to phone
# Enable "Install from unknown sources" in phone settings
# Open APK file on phone to install

# Or using ADB
adb install app-release.apk
```

#### Web Mode (Testing)
```bash
# Run as web app (testing only)
npm run web
# Or: npx expo start --web

# Access at: http://localhost:8081
```

### Environment Configuration
Create `.env` file in `UserApp/laundry-app/`:
```env
API_URL=https://laundry.nexusph.site/api
```

Update `services/api.ts`:
```typescript
const API_URL = process.env.API_URL || 'https://laundry.nexusph.site/api';
```

---

## DATABASE

### Overview
**Type:** MySQL 8.0+
**Location:** Remote server at 140.245.47.120:3306
**Database Name:** laundry
**Schema File:** `AdministratorWeb/schema.sql`

### Core Tables

#### Users & Authentication
- **AspNetUsers** - User accounts (ASP.NET Identity)
  - Columns: Id, UserName, Email, PasswordHash, FirstName, LastName, CreatedAt, LastLoginAt, IsActive
- **AspNetRoles** - User roles (Admin, Customer)
- **AspNetUserRoles** - User-role mapping
- **AspNetRoleClaims** - Role permissions

#### Robots
- **LaundryRobots**
  - Columns: Id, Name, IpAddress, DomainName, IsActive, CanAcceptRequests, CurrentLocation, LastHeartbeat
- **RobotState** - Robot state persistence

#### Requests
- **LaundryRequests**
  - Columns: Id, CustomerId, CustomerName, CustomerPhone, Address, Instructions
  - Type (Pickup/Delivery), Status (13+ states), Weight, TotalCost, IsPaid
  - RequestedAt, ScheduledAt, CompletedAt, AssignedRobotId, HandledByUserId
  - **Status Values:** Pending, Assigned, OnTheWay, Arrived, Loading, InProgress, WeighingClothes, Returning, ConfirmingDelivery, Completed, Cancelled, Failed, TimedOut

#### Payments
- **Payments**
  - Columns: Id, LaundryRequestId, Amount, Method, Status
  - TransactionId, PaymentReference, CreatedAt, ProcessedAt, ProcessedByUserId, FailureReason
  - **Methods:** Cash, GCash, Card, BankTransfer

#### Navigation
- **BluetoothBeacons**
  - Columns: Id, RoomId, Name, MacAddress, RssiThreshold
  - Used for room location detection

#### Communication
- **Messages** - Admin-customer messaging

#### Settings
- **LaundrySettings**
  - Columns: RatePerKg (25.00), MinimumCharge (50.00)
  - CompanyName, CompanyAddress, CompanyPhone, OperatingHours
  - MaxWeightPerRequest, MinWeightPerRequest

### Database Setup

#### Method 1: Using Entity Framework Migrations (Recommended)
```bash
# Navigate to web app
cd E:\THESIS\laundry-robot\AdministratorWeb

# Update database with latest migrations
dotnet ef database update

# Create new migration (if schema changes)
dotnet ef migrations add MigrationName
```

#### Method 2: Using SQL Script
```bash
# Import schema
mysql -h 140.245.47.120 -u root -p laundry < AdministratorWeb/schema.sql

# Or connect and import
mysql -h 140.245.47.120 -u root -p
> use laundry;
> source E:/THESIS/laundry-robot/AdministratorWeb/schema.sql;
```

#### Maintenance Scripts
```bash
# Reset money/payment data (testing)
mysql -h 140.245.47.120 -u root -p laundry < RESET_DATABASE_MONEY.sql

# Fix specific request issues
mysql -h 140.245.47.120 -u root -p laundry < fix-request-155.sql

# Add line color detection columns
mysql -h 140.245.47.120 -u root -p laundry < add-line-color-columns.sql

# Set RSSI thresholds for beacons
mysql -h 140.245.47.120 -u root -p laundry < fix-rssi-threshold.sql

# Configure base beacon
mysql -h 140.245.47.120 -u root -p laundry < set-base-beacon.sql
```

### Connection String
```
Server=140.245.47.120;Port=3306;Database=laundry;User=root;Password=***;
```

### Backup & Restore
```bash
# Backup database
mysqldump -h 140.245.47.120 -u root -p laundry > backup_$(date +%Y%m%d).sql

# Restore database
mysql -h 140.245.47.120 -u root -p laundry < backup_20241027.sql
```

---

## HARDWARE COMPONENTS

### Raspberry Pi 5 Pinout

#### Motors (4x DC Motors via Motor Driver)
| Motor | GPIO Pin 1 | GPIO Pin 2 | Direction |
|-------|-----------|-----------|-----------|
| Front Left | GPIO 5 | GPIO 6 | Forward/Reverse |
| Front Right | GPIO 19 | GPIO 26 | Forward/Reverse |
| Back Left | GPIO 16 | GPIO 20 | Forward/Reverse |
| Back Right | GPIO 13 | GPIO 21 | Forward/Reverse |

#### Sensors
| Sensor | GPIO Pins | Purpose |
|--------|-----------|---------|
| **Ultrasonic HC-SR04** | Trig: GPIO 17, Echo: GPIO 27 | Obstacle detection (front) |
| **HX711 Weight Sensor** | Data: GPIO 18, Clock: GPIO 23 | Laundry weight measurement |
| **Camera** | CSI/USB | Line detection |

#### Actuators
| Component | GPIO Pin | Purpose |
|-----------|----------|---------|
| **LED Headlights** | TBD | Illumination |

#### Communication
- **Bluetooth LE Adapter** - USB Bluetooth dongle for beacon detection
- **WiFi** - Built-in Raspberry Pi 5 WiFi for server communication

### Hardware Specifications

#### Robot Hardware
- **Controller:** Raspberry Pi 5 (ARM64, 8GB RAM recommended)
- **OS:** Raspberry Pi OS 64-bit (Debian-based)
- **Motor Driver:** L298N or similar H-Bridge (handles 4 DC motors)
- **Power:** 12V battery pack (motors) + 5V USB-C (Raspberry Pi)
- **Camera:** Raspberry Pi Camera Module v2 or USB webcam
- **Weight Sensor:** HX711 + Load Cell (0-50kg capacity)
- **Ultrasonic Sensor:** HC-SR04 (2cm-400cm range)
- **Bluetooth:** USB Bluetooth 4.0+ adapter
- **WiFi:** Built-in Raspberry Pi 5 WiFi or USB adapter

#### Beacon Hardware
- **Type:** Bluetooth LE beacons
- **Standard:** iBeacon or Eddystone compatible
- **Placement:** One beacon per room for location detection
- **RSSI Threshold:** Configurable (-40 dBm default)

### Wiring Diagram
```
Raspberry Pi 5
│
├── GPIO 5,6,16,20,13,21,19,26 → L298N Motor Driver → 4x DC Motors
├── GPIO 17,27 → HC-SR04 Ultrasonic Sensor
├── GPIO 18,23 → HX711 Weight Sensor Module → Load Cell
├── CSI Port → Camera Module v2
├── USB Port → Bluetooth LE Adapter
└── Power: 5V USB-C (from buck converter or separate power supply)

L298N Motor Driver
├── Power Input: 12V from battery
├── GND: Common ground with Raspberry Pi
└── Logic: 5V from Raspberry Pi (if needed)
```

### Safety Notes
- **Common Ground:** Ensure Raspberry Pi and motor driver share common ground
- **Power Isolation:** Use separate power sources for Pi (5V) and motors (12V)
- **GPIO Protection:** Consider using optocouplers for motor driver signals
- **Emergency Stop:** Implement hardware emergency stop button

---

## QUICK START GUIDE

### 1. Initial Setup

#### A. Clone Repository
```bash
cd E:\THESIS
git clone <repository-url> laundry-robot
cd laundry-robot
```

#### B. Database Setup
```bash
# Import database schema
mysql -h 140.245.47.120 -u root -p laundry < AdministratorWeb/schema.sql
```

### 2. Run Web Application

```bash
cd AdministratorWeb

# Install .NET 8 SDK (if not installed)
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0

# Restore and run
dotnet restore
dotnet run

# Access at: https://localhost:5001
# Default admin: admin@laundry.com / Admin123!
```

### 3. Run Mobile App

```bash
cd UserApp/laundry-app

# Install Node.js 18+ (if not installed)
# Download from: https://nodejs.org/

# Install dependencies
npm install

# Start development server
npm start

# Scan QR code with Expo Go app (Android/iOS)
```

### 4. Deploy Robot (Raspberry Pi)

#### On Development Machine:
```bash
cd LineFollowerRobot

# Build for ARM64
dotnet publish -c Release -r linux-arm64 --self-contained true -o publish/robot

# Transfer to Pi (replace IP with your Pi's IP)
scp -r publish/robot pi@192.168.1.100:/home/pi/laundry-robot/
```

#### On Raspberry Pi:
```bash
# SSH into Pi
ssh pi@192.168.1.100

# Run robot
cd /home/pi/laundry-robot
sudo ./LineFollowerRobot

# Or setup as system service (see Robot Controller section)
```

### 5. Production Deployment

#### Web Application
```bash
# Build for production
cd AdministratorWeb
dotnet publish -c Release -o publish/web

# Deploy to server
scp -r publish/web user@laundry.nexusph.site:/var/www/laundry/

# Configure reverse proxy (Nginx/Apache)
# Setup SSL certificate (Let's Encrypt)
# Configure systemd service
```

#### Mobile Application
```bash
# Build APK
cd UserApp/laundry-app
npx expo build:android

# Or use EAS Build
eas build --platform android

# Distribute APK or publish to Google Play Store
```

---

## BUILD & RUN CHEAT SHEET

### Web Application
```bash
# Development
cd AdministratorWeb && dotnet run

# Production Build
dotnet publish -c Release -o publish/web

# Database Migrations
dotnet ef database update
```

### Robot Controller
```bash
# Build for Raspberry Pi
cd LineFollowerRobot && dotnet publish -c Release -r linux-arm64 --self-contained true

# Run on Pi
sudo ./LineFollowerRobot
```

### Mobile App
```bash
# Development
cd UserApp/laundry-app && npm start

# Build APK
npx expo build:android
# Or: eas build --platform android

# Run on Android
npm run android
```

### Database
```bash
# Import schema
mysql -h 140.245.47.120 -u root -p laundry < schema.sql

# Backup
mysqldump -h 140.245.47.120 -u root -p laundry > backup.sql
```

---

## SYSTEM ARCHITECTURE DIAGRAM

```
┌─────────────────────────────────────────────────────────────────┐
│                         USERS                                     │
├─────────────────────────────────────────────────────────────────┤
│  Customer (Mobile App)              Admin (Web Browser)          │
│         │                                   │                     │
│         └───────────────┬───────────────────┘                    │
└─────────────────────────┼─────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                  WEB APPLICATION                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  ASP.NET Core 8 (MVC + Web API)                          │   │
│  │  - Dashboard, Request Management, Robot Fleet            │   │
│  │  - JWT Authentication, API Endpoints                     │   │
│  └──────────────────────────────────────────────────────────┘   │
│                          │                                        │
│                          ▼                                        │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Entity Framework Core → MySQL Database                  │   │
│  │  - Users, Requests, Robots, Payments, Beacons           │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────┬───────────────────────────────────────┘
                          │
                          │ HTTP/REST API (1Hz polling)
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ROBOT CONTROLLER                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  .NET 8 Console Application (Raspberry Pi 5)            │   │
│  │  ┌────────────────────────────────────────────────────┐ │   │
│  │  │  Services:                                         │ │   │
│  │  │  - Line Follower (PID Control)                     │ │   │
│  │  │  - Beacon Navigation (BLE RSSI)                    │ │   │
│  │  │  - Motor Control (4x DC Motors via GPIO)           │ │   │
│  │  │  - Camera Feed (320x240 @ 5 FPS)                   │ │   │
│  │  │  - Ultrasonic Sensor (Obstacle Detection)          │ │   │
│  │  │  - Weight Sensor (HX711 Load Cell)                 │ │   │
│  │  │  - Server Communication (1Hz sync)                 │ │   │
│  │  └────────────────────────────────────────────────────┘ │   │
│  └──────────────────────────────────────────────────────────┘   │
│                          │                                        │
│                          ▼                                        │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Hardware (via GPIO):                                    │   │
│  │  - 4x DC Motors (Line Following)                         │   │
│  │  - Camera (Line Detection)                               │   │
│  │  - HC-SR04 Ultrasonic (Obstacle Avoidance)              │   │
│  │  - HX711 Weight Sensor (Laundry Weighing)               │   │
│  │  - BLE Adapter (Room Detection via Beacons)             │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                  MOBILE APPLICATION                               │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  React Native (Expo)                                     │   │
│  │  - Home, Request, History, Profile, Support              │   │
│  │  - Real-time Request Tracking                            │   │
│  │  - Payment Management                                    │   │
│  │  - Push Notifications                                    │   │
│  └──────────────────────────────────────────────────────────┘   │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            │ HTTP/REST API (JWT Auth)
                            │
                            └───────────► WEB APPLICATION
```

---

## TECHNOLOGY SUMMARY BY COMPONENT

### Web Application (AdministratorWeb)
- **Made of:** C# 12, ASP.NET Core 8 MVC + Web API, Razor views, Entity Framework Core, MySQL
- **Build:** `dotnet build` or `dotnet publish -c Release`
- **Run:** `dotnet run` (dev) or `dotnet AdministratorWeb.dll` (prod)
- **Functionality:**
  - Admin dashboard with real-time robot monitoring
  - Laundry request lifecycle management (13 states)
  - Robot fleet management and control
  - User and customer management
  - Accounting and payment tracking
  - Room/beacon configuration
  - Admin-customer messaging
  - System settings (pricing, hours, etc.)
  - REST API for mobile app and robot communication

### Robot Controller (LineFollowerRobot)
- **Made of:** C# 12, .NET 8 Console, System.Device.Gpio, ImageSharp, FFMpeg, Linux.Bluetooth, ZXing
- **Build:** `dotnet publish -c Release -r linux-arm64 --self-contained true`
- **Run:** `sudo ./LineFollowerRobot` on Raspberry Pi 5
- **Functionality:**
  - Autonomous line following using camera + PID control
  - Bluetooth beacon-based room navigation (RSSI detection)
  - Obstacle avoidance with ultrasonic sensors
  - Laundry weight measurement (HX711 load cell)
  - Real-time server communication (1Hz data exchange)
  - Camera feed upload to server
  - QR code scanning for WiFi configuration
  - Remote control (emergency stop, headlights, maintenance)
  - Self-registration and heartbeat

### Mobile App (UserApp/laundry-app)
- **Made of:** TypeScript, React Native 0.79.5, Expo SDK 53, Expo Router, Axios, AsyncStorage
- **Build:** `npm install` then `npx expo build:android` or `eas build --platform android`
- **Run:** `npm start` (dev with Expo Go) or install APK (production)
- **Functionality:**
  - Create laundry pickup/delivery requests
  - Real-time request status tracking
  - View request history
  - User profile management
  - Payment confirmation
  - Push notifications for status updates
  - Customer support/messaging
  - Robot availability display

### Database (MySQL)
- **Made of:** MySQL 8.0+, Entity Framework Core migrations, SQL scripts
- **Build:** `dotnet ef migrations add MigrationName` (create migration)
- **Run:** `dotnet ef database update` or `mysql < schema.sql`
- **Functionality:**
  - User authentication and authorization
  - Laundry request storage and state management
  - Robot registration and status tracking
  - Payment transaction records
  - Beacon/room configuration
  - Message storage
  - System settings

---

## DEPLOYMENT CHECKLIST

### Web Application
- [ ] Update appsettings.json (connection strings, JWT keys)
- [ ] Run `dotnet publish -c Release`
- [ ] Configure reverse proxy (Nginx/Apache)
- [ ] Setup SSL certificate
- [ ] Configure firewall (ports 80, 443)
- [ ] Setup systemd service for auto-start
- [ ] Test database connectivity
- [ ] Verify API endpoints

### Robot Controller
- [ ] Update appsettings.json (robot name, server URL)
- [ ] Build for linux-arm64
- [ ] Transfer to Raspberry Pi
- [ ] Test GPIO connections
- [ ] Calibrate weight sensor (WeightTest utility)
- [ ] Test camera feed
- [ ] Configure Bluetooth adapter
- [ ] Setup systemd service
- [ ] Test beacon detection
- [ ] Verify server communication

### Mobile Application
- [ ] Update API URL in code
- [ ] Configure app.json (version, package name)
- [ ] Build APK/AAB
- [ ] Test on physical device
- [ ] Configure push notifications
- [ ] Setup Google Play Store (if publishing)
- [ ] Test all user flows
- [ ] Verify offline functionality

### Database
- [ ] Backup existing data
- [ ] Run migrations
- [ ] Seed initial data
- [ ] Configure user accounts
- [ ] Setup beacon configurations
- [ ] Test connections from all components
- [ ] Configure automated backups

---

## TROUBLESHOOTING

### Web Application
**Problem:** Database connection fails
**Solution:** Check MySQL server accessibility, verify connection string in appsettings.json

**Problem:** JWT authentication not working
**Solution:** Ensure JWT secret key matches between web app and mobile app, check token expiration

### Robot Controller
**Problem:** GPIO permission denied
**Solution:** Run with `sudo` or add user to gpio group: `sudo usermod -aG gpio pi`

**Problem:** Camera not detected
**Solution:** Enable camera in `sudo raspi-config`, check CSI/USB connection

**Problem:** Bluetooth adapter not found
**Solution:** Install `bluez`: `sudo apt install bluez`, check USB adapter

**Problem:** Can't connect to server
**Solution:** Check network connectivity, verify server URL in appsettings.json, check firewall

### Mobile Application
**Problem:** API requests fail
**Solution:** Check API_URL configuration, verify server is running, check CORS settings

**Problem:** Build fails
**Solution:** Clear cache: `npx expo start -c`, reinstall: `rm -rf node_modules && npm install`

**Problem:** App crashes on startup
**Solution:** Check logs: `npx expo start`, verify all required permissions

---

## ADDITIONAL RESOURCES

### Documentation
- **README.md** - Project overview and setup
- **ALGORITHMS.md** - Flowcharts and algorithm details
- **schema.sql** - Complete database schema

### Configuration Files
- **global.json** - .NET SDK version
- **appsettings.json** (Web) - Server configuration
- **appsettings.json** (Robot) - Robot configuration
- **app.json** (Mobile) - Expo configuration

### SQL Scripts
- **add-line-color-columns.sql** - Add line color detection
- **fix-request-155.sql** - Fix specific request issues
- **fix-rssi-threshold.sql** - Update beacon RSSI thresholds
- **set-base-beacon.sql** - Configure base station beacon
- **RESET_DATABASE_MONEY.sql** - Reset payment/accounting data

---

## CONTACT & SUPPORT

For issues or questions regarding this system:
1. Check the troubleshooting section above
2. Review log files (systemd journals, application logs)
3. Refer to README.md and ALGORITHMS.md
4. Check database state and migrations
5. Verify hardware connections and configurations

---

**Last Updated:** 2025-10-27
**Documentation Version:** 1.0.0
**Project Version:** 1.0.0
