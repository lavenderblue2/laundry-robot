# Autonomous Laundry Collection Robot System

A complete autonomous laundry collection and delivery system combining computer vision-based line following, Bluetooth beacon navigation, and weight measurement. This thesis project demonstrates the integration of robotics, web services, and mobile applications for automated service delivery.

## Overview

The system consists of:
- **Robot Controller**: Raspberry Pi 5-based autonomous robot with camera-based navigation
- **Backend Server**: ASP.NET Core 8 API and admin dashboard
- **Mobile App**: React Native/Expo customer interface
- **Python Services**: ArUco marker detection and legacy reference implementations

## System Architecture

```
┌─────────────────┐      ┌──────────────────┐      ┌─────────────────┐
│   Mobile App    │◄────►│  Backend Server  │◄────►│  Robot Control  │
│ (React Native)  │      │  (ASP.NET Core)  │      │ (Raspberry Pi)  │
└─────────────────┘      └──────────────────┘      └─────────────────┘
                                  │
                         ┌────────┴────────┐
                         │   MySQL DB      │
                         └─────────────────┘
```

## Features

### Robot Capabilities
- **Computer Vision Line Following**: PID-controlled navigation using camera input
- **Bluetooth Beacon Navigation**: RSSI-based room detection and localization
- **Obstacle Avoidance**: Ultrasonic sensor-based collision detection
- **Weight Measurement**: HX711 load cell interface with 0.0001kg precision
- **Real-time Communication**: Continuous server synchronization (1Hz data exchange)
- **Remote Control**: Emergency stop, maintenance mode, headlight control

### Request Management
- Dual request types (Pickup and Delivery)
- **Manual Request Creation** - Admin-initiated requests for walk-in customers or assisted service
  - **Walk-In Mode**: Customer at shop with laundry, manual weight entry, skips robot pickup
  - **Robot Delivery Mode**: Normal robot pickup with auto-assignment
  - Visible admin badges in mobile app showing manual override
- 13+ distinct status states tracking complete lifecycle
- Automatic robot assignment
- Real-time status updates
- Payment integration and accounting
- Admin-customer messaging system

### Multi-Component System
- Web-based admin dashboard for staff management
- Mobile app for customer requests and tracking
- Support for multiple robots with queue-based assignment
- Configurable pricing and operational parameters
- Beacon-based multi-room support

## Project Structure

```
laundry-robot/
├── AdministratorWeb/       # ASP.NET Core 8 MVC server + API
├── LineFollowerRobot/      # .NET 8 robot controller (ARM64)
├── UserApp/                # React Native/Expo mobile app
├── RobotProject.Shared/    # Shared DTOs between robot and server
├── ArucoPy/                # Python Flask service for ArUco detection
├── Python/                 # Legacy Python line-following reference
├── WeightTest/             # HX711 weight sensor calibration utility
└── *.sql                   # Database migration and maintenance scripts
```

## Technology Stack

### Backend
- ASP.NET Core 8.0 (MVC + Web API)
- Entity Framework Core with MySQL
- JWT Bearer Authentication
- ASP.NET Identity

### Robot Hardware/Software
- Raspberry Pi 5 (ARM64)
- .NET 8.0 Console Application
- System.Device.Gpio for hardware control
- OpenCV via camera capture
- Bluetooth LE beacon detection

**Hardware Components**:
- 4x DC motors with GPIO control
- HC-SR04 ultrasonic sensor
- HX711 load cell amplifier
- CSI camera module
- Bluetooth adapter
- LED headlights

### Mobile Application
- React Native with Expo SDK 53
- Axios for HTTP requests
- AsyncStorage for local persistence
- JWT-based authentication

### Python Services
- OpenCV for computer vision
- Flask for ArUco web service
- gpiozero for GPIO control (legacy)

## Getting Started

### Prerequisites

- .NET SDK 8.0 or later
- MySQL 8.0+
- Node.js 18+ and npm
- Raspberry Pi 5 with camera and Bluetooth modules
- Python 3.8+ (for ArUco service)

### Backend Setup

1. Configure database connection in `AdministratorWeb/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-server;Database=laundry;User=your-user;Password=your-password;"
  }
}
```

2. Apply database migrations:
```bash
cd AdministratorWeb
dotnet ef database update
```

3. Run the server:
```bash
dotnet run
```

The admin dashboard will be available at `https://localhost:5001`

### Robot Setup

1. Install .NET 8.0 runtime on Raspberry Pi 5:
```bash
wget https://dot.net/v1/dotnet-install.sh
sudo bash dotnet-install.sh --runtime aspnetcore --channel 8.0
```

2. Configure robot settings in `LineFollowerRobot/appsettings.json`:
```json
{
  "RobotName": "Robot-42",
  "ApiBaseUrl": "https://your-server/api",
  "Camera": {
    "Width": 320,
    "Height": 240,
    "Fps": 5
  },
  "PID": {
    "Kp": 0.2,
    "Ki": 0.0,
    "Kd": 0.05
  }
}
```

3. Build and deploy:
```bash
dotnet publish -c Release -r linux-arm64 --self-contained
```

4. Run on Raspberry Pi:
```bash
sudo ./LineFollowerRobot
```

### Mobile App Setup

1. Install dependencies:
```bash
cd UserApp
npm install
```

2. Configure API endpoint in `services/api.ts`:
```typescript
const API_BASE_URL = 'https://your-server/api';
```

3. Start development server:
```bash
npx expo start
```

4. Build for production:
```bash
# Android
eas build --platform android --profile preview

# iOS
eas build --platform ios --profile preview
```

### ArUco Detection Service (Optional)

```bash
cd ArucoPy
pip install opencv-contrib-python flask colorama
python ArucoPy.py
```

## Configuration

### Laundry Settings
Configure via admin dashboard or database:
- **Price per kg**: Default 25.00
- **Minimum charge**: Default 50.00
- **Weight limits**: 1.0kg - 50.0kg per request
- **Line color**: RGB values for floor line detection
- **Stop color**: RGB values for destination markers

### Beacon Configuration
Add Bluetooth beacons via admin dashboard:
- MAC address
- Room assignment
- RSSI threshold (-40 to -75 dBm)
- Priority level
- Base station designation

### Robot Hardware Configuration
GPIO pin assignments in `appsettings.json`:
```json
{
  "Motors": {
    "FrontLeft": [5, 6],
    "FrontRight": [19, 26],
    "BackLeft": [16, 20],
    "BackRight": [13, 21]
  },
  "Sensors": {
    "UltrasonicTrigger": 17,
    "UltrasonicEcho": 27,
    "WeightData": 18,
    "WeightClock": 23
  }
}
```

## API Documentation

### Robot Endpoints
```
POST   /api/robot/{name}/register         - Robot self-registration
POST   /api/robot/{name}/ping             - Heartbeat ping
POST   /api/robot/{name}/data-exchange    - Bidirectional data sync
GET    /api/robot/{name}/status           - Get commands
```

### Mobile App Endpoints
```
POST   /api/auth/login                    - User authentication
POST   /api/requests                      - Create pickup/delivery request
GET    /api/requests/active               - Get active request
GET    /api/requests/my-requests          - Request history
PUT    /api/requests/{id}/confirm-loaded  - Confirm laundry loaded
```

### Admin Web Endpoints
- Dashboard and statistics
- Request management (CRUD)
- **Manual Request Creation** - Create requests on behalf of customers
  - `POST /Requests/CreateManualRequest` - Create walk-in or robot delivery requests
  - Validates robot availability, customer beacon assignment
  - Supports manual weight entry for walk-in customers
- **Delivery Start Validation** - Safety checks before dispatching robots
  - `POST /Requests/StartDelivery` - Validates robot availability before starting delivery
  - Prevents dispatch when no robots are online
  - Error: "No bots active - cannot start delivery"
- Robot monitoring and control
- Beacon configuration
- User management
- Payment tracking and adjustments

## System Workflow

### Pickup Request
1. Customer creates request via mobile app
2. System assigns available robot
3. Robot navigates to customer's room using line-following + beacons
4. Customer loads laundry onto robot
5. Robot returns to base station
6. System measures weight and calculates cost
7. Customer completes payment
8. Laundry enters washing cycle

### Delivery Request
1. Admin marks laundry as ready for delivery
2. Admin loads cleaned laundry onto robot
3. Robot navigates to customer's room
4. Customer receives laundry and confirms delivery

### Manual Request (Admin-Created)
#### Walk-In Service
1. Customer brings laundry directly to shop
2. Admin creates manual walk-in request via dashboard
3. Admin enters customer selection and actual laundry weight
4. System calculates cost based on weight (Status: Washing)
5. Customer pays and collects laundry when ready
6. Request reflects in customer's mobile app with admin badge

#### Robot Delivery (Admin-Assisted)
1. Admin creates manual robot delivery request
2. System validates robot availability and customer beacon
3. Robot auto-assigned and dispatched to customer room
4. Normal pickup workflow proceeds
5. Mobile app shows admin-created badge for transparency

## Request Lifecycle States

```
Pickup:   Pending → Accepted → InProgress → RobotEnRoute → ArrivedAtRoom
         → LaundryLoaded → ReturnedToBase → WeighingComplete
         → PaymentPending → Completed

Delivery: FinishedWashing → FinishedWashingReadyToDeliver
         → DeliveringToCustomer → DeliveredToCustomer → Completed
```

## Database Schema

Core tables:
- **LaundryRequests**: Request details, status, weight, cost
- **LaundrySettings**: System configuration and pricing
- **BluetoothBeacons**: Beacon registry with room mapping
- **RobotState**: Persisted robot state for recovery
- **Payments**: Payment transactions and adjustments
- **ApplicationUser**: AspNet Identity + custom fields
- **Messages**: Admin-customer communication

## Development

### Building from Source

Backend:
```bash
cd AdministratorWeb
dotnet build -c Release
```

Robot:
```bash
cd LineFollowerRobot
dotnet publish -c Release -r linux-arm64
```

Mobile App:
```bash
cd UserApp
npm install
npx expo prebuild
```

### Running Tests

```bash
# Backend tests
dotnet test

# Mobile app (if configured)
npm test
```

## Deployment

### Production Server
The system is deployed at:
- Web/API: `https://laundry.nexusph.site/`
- Database: MySQL 8.0 at `140.245.47.120:3306`

### Robot Deployment
1. Build ARM64 release
2. Copy to Raspberry Pi
3. Configure as systemd service for auto-start
4. Ensure GPIO permissions and camera enabled

### Mobile App Deployment
Build using EAS (Expo Application Services):
```bash
eas build --platform android --profile preview
eas build --platform ios --profile preview
```

## Hardware Setup

### Robot Assembly
1. Mount 4 DC motors in quadruped-like configuration
2. Connect motor drivers to GPIO pins
3. Install ultrasonic sensor on front
4. Mount CSI camera for line detection
5. Install HX711 load cell platform
6. Configure Bluetooth adapter
7. Wire LED headlights

### Calibration
- **Weight Sensor**: Use WeightTest utility to calibrate offset and reference unit
- **Line Detection**: Adjust binary threshold (0-255) based on floor color
- **PID Tuning**: Adjust Kp, Ki, Kd for smooth line following
- **Beacon RSSI**: Test beacon range and adjust thresholds

## Troubleshooting

### Robot Issues
- **Line lost**: Check camera focus, lighting, and threshold value
- **Beacon not detected**: Verify RSSI threshold (-75 dBm for 2-3m range)
- **Weight inaccurate**: Run calibration with known weights
- **Motors not responding**: Check GPIO permissions (`sudo usermod -aG gpio $USER`)

### Server Issues
- **Database connection**: Verify MySQL credentials and firewall rules
- **API timeout**: Check CORS policy and SSL certificate
- **Migration errors**: Reset database and reapply migrations

### Mobile App Issues
- **Login fails**: Verify API endpoint URL
- **Token expired**: App automatically handles refresh (24h expiration)
- **Build errors**: Clear cache with `npx expo start -c`

## Known Limitations

- Robot authentication uses name-only (no cryptographic signing)
- Auto-accept feature disabled (manual admin approval required)
- Battery monitoring implemented but not fully integrated
- ArUco marker detection optional (beacon mode preferred)
- Single-threaded line detection (5 FPS maximum)

## Future Improvements

- Multi-robot fleet coordination
- Predictive maintenance based on sensor data
- Mobile payment gateway integration
- Advanced path planning with A* algorithm
- Computer vision-based obstacle recognition
- Customer notification system (SMS/push)
- Analytics dashboard for operational insights

## License

This project is developed as a thesis project. Please contact the authors for licensing information.

## Acknowledgments

- Thesis project for autonomous service robotics
- Built with .NET, React Native, and Python
- Raspberry Pi Foundation for excellent ARM hardware
- OpenCV community for computer vision tools
