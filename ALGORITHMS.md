# Laundry Robot System - Algorithm Flowcharts

This document contains visual flowcharts for all major algorithms in the laundry robot system.

---

## 1. Complete Request Lifecycle

End-to-end flow from customer request creation to completion and payment.

```mermaid
flowchart TD
    Start([Customer Creates Request]) --> CheckDupe{Has Active<br/>Request?}
    CheckDupe -->|Yes| Reject([Reject: Duplicate Request])
    CheckDupe -->|No| AssignRobot[Auto-Assign Robot]

    AssignRobot --> CheckAvail{Robot<br/>Available?}
    CheckAvail -->|No| NoRobot([Reject: No Robots Available])
    CheckAvail -->|Yes| CheckAutoAccept{Auto-Accept<br/>Enabled?}

    CheckAutoAccept -->|No| Queue[Status: Pending<br/>Wait for Admin Approval]
    CheckAutoAccept -->|Yes| CheckBusy{Any Robot<br/>Busy?}

    CheckBusy -->|Yes| Queue
    CheckBusy -->|No| Accept[Status: Accepted<br/>Start Line Following]

    Queue --> AdminApprove{Admin<br/>Approves?}
    AdminApprove -->|No| Declined([Status: Declined])
    AdminApprove -->|Yes| Accept

    Accept --> Navigate1[Robot Navigates to Customer Room]
    Navigate1 --> Arrived1[Status: ArrivedAtRoom<br/>Wait for Customer Confirmation]

    Arrived1 --> CustomerLoad{Customer<br/>Loads Laundry?}
    CustomerLoad -->|Timeout| Timeout1([Status: Cancelled])
    CustomerLoad -->|Yes| Loaded[Status: LaundryLoaded<br/>Return to Base]

    Loaded --> Navigate2[Robot Returns to Base]
    Navigate2 --> BaseArrival[Verify at Base Beacon]
    BaseArrival --> Washing[Status: Washing<br/>Process Next Queue]

    Washing --> AdminWash[Admin: Mark Washing Done]
    AdminWash --> Delivery[Status: FinishedWashingGoingToRoom<br/>Deliver Clean Laundry]

    Delivery --> Navigate3[Robot Navigates to Customer Room]
    Navigate3 --> Arrived2[Status: FinishedWashingArrivedAtRoom<br/>Wait for Unloading]

    Arrived2 --> CustomerUnload{Customer<br/>Unloads?}
    CustomerUnload -->|Timeout| Timeout2([Status: Cancelled])
    CustomerUnload -->|Yes| Unloaded[Status: FinishedWashingGoingToBase<br/>Return to Base]

    Unloaded --> Navigate4[Robot Returns to Base]
    Navigate4 --> Complete[Status: Completed<br/>Process Next Queue]

    Complete --> Payment[Customer Pays]
    Payment --> End([Request Finished])

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style Reject fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style NoRobot fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Declined fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Timeout1 fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Timeout2 fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Accept fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style Loaded fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style Washing fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style Delivery fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style Complete fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
```

---

## 2. Request Status State Machine

State diagram showing all possible status transitions and triggers.

```mermaid
stateDiagram-v2
    [*] --> Pending: Customer Creates Request

    Pending --> Accepted: Admin Accepts OR Auto-Accept
    Pending --> Declined: Admin Declines
    Pending --> Cancelled: Customer Cancels

    Accepted --> ArrivedAtRoom: Robot Arrives at Customer Room
    Accepted --> Cancelled: Timeout or Cancel

    ArrivedAtRoom --> LaundryLoaded: Customer Confirms Loaded
    ArrivedAtRoom --> Cancelled: Timeout

    LaundryLoaded --> Washing: Robot Returns to Base (Verified)
    LaundryLoaded --> Cancelled: Cancel Request

    Washing --> FinishedWashingGoingToRoom: Admin Marks Done + Start Delivery
    Washing --> Cancelled: Cancel Request

    FinishedWashingGoingToRoom --> FinishedWashingArrivedAtRoom: Robot Arrives for Delivery
    FinishedWashingGoingToRoom --> Cancelled: Timeout or Cancel

    FinishedWashingArrivedAtRoom --> FinishedWashingGoingToBase: Customer Confirms Unloaded
    FinishedWashingArrivedAtRoom --> Cancelled: Timeout

    FinishedWashingGoingToBase --> Completed: Robot Returns to Base

    Completed --> [*]: Payment Processed
    Declined --> [*]
    Cancelled --> [*]

    note right of Pending
        Queue: Waiting for robot availability
    end note

    note right of Accepted
        Robot navigating to customer room
    end note

    note right of Washing
        Laundry being washed by admin
        Robot available for next request
    end note

    note right of Completed
        Clean laundry delivered
        Robot available for next request
    end note
```

---

## 3. Auto-Assignment & Queueing Algorithm

How robots are assigned to requests and queue management.

```mermaid
flowchart TD
    Start([New Request Created]) --> GetRobots[Get All Active Online Robots]

    GetRobots --> AnyActive{Any Active<br/>Robots?}
    AnyActive -->|No| NoRobots([Fail: No Robots Available])
    AnyActive -->|Yes| FindAvailable[Find Available Robots<br/>Status = Available]

    FindAvailable --> HasAvailable{Found Available<br/>Robots?}

    HasAvailable -->|Yes| SelectFirst[Select First Available Robot]
    SelectFirst --> CheckAutoAccept{Auto-Accept<br/>Enabled?}

    CheckAutoAccept -->|No| AssignPending[Assign Robot<br/>Status: Pending]
    CheckAutoAccept -->|Yes| CheckOtherBusy{Any Other<br/>Active Requests?}

    CheckOtherBusy -->|Yes| AssignPending
    CheckOtherBusy -->|No| AssignAccepted[Assign Robot<br/>Status: Accepted<br/>Start Line Following]

    HasAvailable -->|No| FindBusy[Find Busy Robots]
    FindBusy --> HasBusy{Found Busy<br/>Robots?}

    HasBusy -->|No| NoRobots
    HasBusy -->|Yes| FindLeastRecent[Find Least Recently<br/>Assigned Busy Robot]

    FindLeastRecent --> GetCurrentReq[Get Current Request<br/>of That Robot]
    GetCurrentReq --> ResetRequest[Reset Current Request<br/>Back to Pending]
    ResetRequest --> ResetRobot[Reset Robot to Available]
    ResetRobot --> AssignToNew[Assign to New Request]
    AssignToNew --> CheckAutoAccept

    AssignPending --> WaitInQueue[Wait in Queue]
    WaitInQueue --> RobotFree{Robot Becomes<br/>Available?}
    RobotFree -->|Yes| AutoProcess[Auto-Process Queue<br/>Oldest Pending First]
    AutoProcess --> AssignAccepted

    AssignAccepted --> End([Robot Dispatched])

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style NoRobots fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style AssignAccepted fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style WaitInQueue fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style AutoProcess fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
```

---

## 4. Robot Arrival Detection Algorithm

How the system determines when a robot has reached its destination.

```mermaid
flowchart TD
    Start([Robot Reports IsInTarget=true]) --> GetRequest[Get Active Request for Robot]

    GetRequest --> HasRequest{Has Active<br/>Request?}
    HasRequest -->|No| Ignore([Ignore - No Active Request])

    HasRequest -->|Yes| CheckStatus{Check Request<br/>Status}

    CheckStatus -->|Accepted| ArrivedUser[Update Status:<br/>ArrivedAtRoom<br/>Stop Line Following]

    CheckStatus -->|LaundryLoaded| CheckBaseBeacon{Detected Beacons<br/>Match Base Beacon?}
    CheckBaseBeacon -->|Yes| CheckRSSI{RSSI >=<br/>Threshold?}
    CheckRSSI -->|Yes| AtBase[Update Status: Washing<br/>Robot Available<br/>Process Next Queue]
    CheckRSSI -->|No| NotCloseEnough[Continue Navigation<br/>Not Close Enough]
    CheckBaseBeacon -->|No| StillTraveling[Still Traveling to Base<br/>Keep Line Following]

    CheckStatus -->|FinishedWashingGoingToRoom| ArrivedDelivery[Update Status:<br/>FinishedWashingArrivedAtRoom<br/>Stop Line Following]

    CheckStatus -->|FinishedWashingGoingToBase| ArrivedAfterDelivery[Update Status: Completed<br/>Robot Available<br/>Process Next Queue]

    CheckStatus -->|Cancelled| CheckCancelledBase{At Base<br/>Beacon?}
    CheckCancelledBase -->|Yes| CancelComplete[Mark Cancelled Complete<br/>Robot Available<br/>Process Next Queue]
    CheckCancelledBase -->|No| ReturnToBase[Continue to Base<br/>Keep Line Following]

    ArrivedUser --> End([Arrival Processed])
    AtBase --> End
    ArrivedDelivery --> End
    ArrivedAfterDelivery --> End
    CancelComplete --> End
    Ignore --> End

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style AtBase fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style ArrivedUser fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style ArrivedDelivery fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style ArrivedAfterDelivery fill:#6B9AC4,stroke:#3D5A80,stroke-width:2px,color:#1E3A5F
    style CancelComplete fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style StillTraveling fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style NotCloseEnough fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
```

---

## 5. Navigation Target Management

How beacon navigation targets are dynamically assigned based on request status.

```mermaid
flowchart TD
    Start([Robot Requests Navigation Config]) --> GetRequest[Get Active Request for Robot]

    GetRequest --> HasRequest{Has Active<br/>Request?}
    HasRequest -->|No| NoTarget[No Navigation Target<br/>Clear All Targets]

    HasRequest -->|Yes| CheckStatus{Check Request<br/>Status}

    CheckStatus -->|Accepted| CustomerRoom[Target: Customer Room<br/>Get Customer's Assigned Beacon]
    CheckStatus -->|LaundryLoaded| BaseRoom1[Target: Base Room<br/>Get Base Beacon]
    CheckStatus -->|FinishedWashingGoingToRoom| CustomerRoom2[Target: Customer Room<br/>Get Customer's Assigned Beacon]
    CheckStatus -->|FinishedWashingGoingToBase| BaseRoom2[Target: Base Room<br/>Get Base Beacon]
    CheckStatus -->|Cancelled| BaseRoom3[Target: Base Room<br/>Return to Base]

    CustomerRoom --> LookupCustomer[Query Database:<br/>Get Customer by ID]
    CustomerRoom2 --> LookupCustomer

    LookupCustomer --> HasBeacon{Customer Has<br/>Assigned Beacon?}
    HasBeacon -->|No| NoTarget
    HasBeacon -->|Yes| GetRoomBeacons[Get All Beacons<br/>for Customer Room]

    BaseRoom1 --> GetBaseBeacons[Get All Beacons<br/>Marked as Base]
    BaseRoom2 --> GetBaseBeacons
    BaseRoom3 --> GetBaseBeacons

    GetRoomBeacons --> SetTargets[Set IsNavigationTarget=true<br/>for Room Beacons]
    GetBaseBeacons --> SetTargets

    SetTargets --> SendBeacons[Send Active Beacons<br/>+ Navigation Targets<br/>Beacon-Only Mode]

    NoTarget --> SendEmpty[Send Active Beacons<br/>All IsNavigationTarget=false]

    SendBeacons --> RobotNavigates[Robot Checks Beacon RSSI<br/>Reports When RSSI >= Threshold]
    SendEmpty --> RobotIdle[Robot Idles<br/>No Navigation Target]

    RobotNavigates --> End([Navigation Config Sent])
    RobotIdle --> End

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style CustomerRoom fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style CustomerRoom2 fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style BaseRoom1 fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style BaseRoom2 fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style BaseRoom3 fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style SetTargets fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    style RobotNavigates fill:#6B9AC4,stroke:#3D5A80,stroke-width:2px,color:#1E3A5F
```

---

## 6. Line Following Algorithm (PID Control)

Robot's core navigation using camera-based line detection and PID controller.

```mermaid
flowchart TD
    Start([Line Following Active]) --> CaptureFrame[Capture Camera Frame]

    CaptureFrame --> DetectLine{Line<br/>Detected?}

    DetectLine -->|Yes| CalcError[Calculate Position Error<br/>from Center Line]
    CalcError --> PID[PID Controller:<br/>P = Kp × error<br/>I = Ki × integral<br/>D = Kd × derivative]

    PID --> Output[Calculate Motor Output<br/>pidOutput = P + I + D]

    Output --> CheckError{Error<br/>Magnitude?}

    CheckError -->|< 30| Straight[Move Forward<br/>Small error]
    CheckError -->|30-80| GentleCorrect[Gentle Correction<br/>Left/Right Forward]
    CheckError -->|80-150| StrongCorrect[Strong Correction<br/>Left/Right Forward]
    CheckError -->|> 150| FullTurn[Full Turn<br/>Left/Right]

    Straight --> UpdateTracking[Update Last Direction<br/>Continue Line Following]
    GentleCorrect --> UpdateTracking
    StrongCorrect --> UpdateTracking
    FullTurn --> UpdateTracking

    DetectLine -->|No| LineLost[Line Lost!<br/>Increment Lost Counter]
    LineLost --> CheckCounter{Lost Counter<br/>> 20 frames?}

    CheckCounter -->|No| Recovery[Try Recovery:<br/>Turn in Last Known Direction]
    Recovery --> UpdateTracking

    CheckCounter -->|Yes| Stop[Stop Robot<br/>Report Error]

    UpdateTracking --> Delay[Frame Delay: 77ms<br/>Target: 13 FPS]
    Delay --> CaptureFrame

    Stop --> End([Line Following Stopped])

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style PID fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style Straight fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    style GentleCorrect fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    style StrongCorrect fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style FullTurn fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style Stop fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Recovery fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
```

---

## 7. Beacon Scanning & Tracking

Bluetooth Low Energy beacon detection and RSSI tracking process.

```mermaid
flowchart TD
    Start([Beacon Scanner Started]) --> InitBluetooth[Initialize Bluetooth Adapter]

    InitBluetooth --> StartScan[Start BLE Scanning<br/>Continuous Mode]

    StartScan --> DetectDevice{BLE Device<br/>Detected?}

    DetectDevice -->|Yes| CheckUUID{Device has<br/>Beacon UUID?}
    CheckUUID -->|No| DetectDevice

    CheckUUID -->|Yes| ParseData[Parse Beacon Data:<br/>MAC Address<br/>RSSI<br/>UUID]

    ParseData --> CalcDistance[Calculate Distance<br/>from RSSI<br/>Using TxPower Formula]

    CalcDistance --> CheckTracked{Already<br/>Tracking?}

    CheckTracked -->|No| AddNew[Add to Tracked Beacons<br/>Create New Entry]
    CheckTracked -->|Yes| UpdateExisting[Update Existing:<br/>RSSI<br/>Distance<br/>LastSeen Timestamp]

    AddNew --> CheckConfig{Has Server<br/>Configuration?}
    UpdateExisting --> CheckConfig

    CheckConfig -->|Yes| ApplyConfig[Apply Configuration:<br/>Name<br/>Room Name<br/>RSSI Threshold<br/>IsNavigationTarget]
    CheckConfig -->|No| UseDefaults[Use Default Values]

    ApplyConfig --> ReportToServer[Report to Server<br/>in Data Exchange]
    UseDefaults --> ReportToServer

    ReportToServer --> CheckStale[Check for Stale Beacons<br/>LastSeen > 10 seconds]

    CheckStale --> RemoveStale[Remove Stale Beacons<br/>from Tracking List]

    RemoveStale --> DetectDevice

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style StartScan fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style ParseData fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    style CalcDistance fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style ApplyConfig fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style ReportToServer fill:#6B9AC4,stroke:#3D5A80,stroke-width:2px,color:#1E3A5F
```

---

## 8. Data Exchange Protocol

Bidirectional communication between robot and server.

```mermaid
flowchart TD
    Start([Every 1 Second]) --> CollectData[Collect Robot Data:<br/>Detected Beacons<br/>Weight Reading<br/>Ultrasonic Distance]

    CollectData --> CheckArrival[Check If At Navigation Target<br/>RSSI >= Threshold?]

    CheckArrival --> BuildRequest[Build Data Exchange Request:<br/>RobotName<br/>Timestamp<br/>DetectedBeacons<br/>IsInTarget<br/>WeightKg<br/>UltrasonicDistance]

    BuildRequest --> SendPOST[POST to Server:<br/>/api/Robot/RobotName/data-exchange]

    SendPOST --> WaitResponse{Server<br/>Response?}

    WaitResponse -->|Timeout| LogError[Log Error:<br/>Server Unreachable]
    WaitResponse -->|Success| ParseResponse[Parse Server Response:<br/>ActiveBeacons<br/>IsLineFollowing<br/>FollowColor<br/>EmergencyStop<br/>MaintenanceMode]

    ParseResponse --> UpdateBeacons[Update Beacon Configurations<br/>with Server Data]

    UpdateBeacons --> UpdateColor[Update Line Color<br/>from Server]

    UpdateColor --> CheckEmergency{Emergency<br/>Stop?}

    CheckEmergency -->|Yes| EmergencyStop[Execute Emergency Stop<br/>Stop All Motors]
    CheckEmergency -->|No| CheckMaintenance{Maintenance<br/>Mode?}

    CheckMaintenance -->|Yes| StopLine[Stop Line Following]
    CheckMaintenance -->|No| CheckLineFollow{Should Line<br/>Follow?}

    CheckLineFollow -->|Yes| StartLine[Start Line Following]
    CheckLineFollow -->|No| StopLineNormal[Stop Line Following]

    EmergencyStop --> Wait[Wait 1 Second]
    StopLine --> Wait
    StartLine --> Wait
    StopLineNormal --> Wait
    LogError --> Wait

    Wait --> CollectData

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style BuildRequest fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style SendPOST fill:#6B9AC4,stroke:#3D5A80,stroke-width:2px,color:#1E3A5F
    style ParseResponse fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    style EmergencyStop fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style LogError fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style StartLine fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
```

---

## 9. Multi-Robot Coordination

How multiple robots share workload and process queued requests.

```mermaid
flowchart TD
    Start([System Monitoring]) --> CheckQueue{Pending Requests<br/>in Queue?}

    CheckQueue -->|No| Idle[All Robots Idle<br/>or Working]
    CheckQueue -->|Yes| FindAvailable[Find Available Robots]

    FindAvailable --> HasAvailable{Found Available<br/>Robot?}

    HasAvailable -->|No| Wait[Wait for Robot<br/>to Complete Task]
    HasAvailable -->|Yes| GetOldest[Get Oldest Pending Request<br/>FIFO Order]

    GetOldest --> AssignRobot[Assign Available Robot<br/>to Request]

    AssignRobot --> UpdateStatus[Update Request:<br/>Status = Accepted<br/>AssignedRobotName]

    UpdateStatus --> StartTask[Start Robot Line Following<br/>Send Navigation Target]

    StartTask --> MonitorProgress[Monitor Robot Progress]

    MonitorProgress --> CheckComplete{Task<br/>Complete?}

    CheckComplete -->|No| MonitorProgress
    CheckComplete -->|Yes| UpdateRobot[Update Robot:<br/>Status = Available<br/>Clear CurrentTask]

    UpdateRobot --> CheckMoreQueue{More Pending<br/>Requests?}

    CheckMoreQueue -->|Yes| GetOldest
    CheckMoreQueue -->|No| Done[Robot Returns to Idle]

    Wait --> RobotFree{Robot Becomes<br/>Available?}
    RobotFree -->|Yes| GetOldest
    RobotFree -->|No| Wait

    Idle --> CheckQueue
    Done --> CheckQueue

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style AssignRobot fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style StartTask fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style MonitorProgress fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style UpdateRobot fill:#6B9AC4,stroke:#3D5A80,stroke-width:2px,color:#1E3A5F
    style Wait fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
```

---

## 10. Timeout & Error Handling

Failure detection and recovery mechanisms.

```mermaid
flowchart TD
    Start([Timeout Monitor Running]) --> CheckRequests[Check All Active Requests]

    CheckRequests --> ScanRequests{For Each<br/>Active Request}

    ScanRequests --> CheckStatus{Request<br/>Status?}

    CheckStatus -->|Accepted| CheckAcceptedTime{Time Since Accepted<br/>> Timeout?}
    CheckStatus -->|ArrivedAtRoom| CheckArrivedTime{Time Since Arrived<br/>> Timeout?}
    CheckStatus -->|FinishedWashingArrivedAtRoom| CheckDeliveryTime{Time Since Arrived<br/>> Timeout?}
    CheckStatus -->|Other| ScanRequests

    CheckAcceptedTime -->|No| ScanRequests
    CheckAcceptedTime -->|Yes| TimeoutAccepted[Timeout Detected!<br/>Robot didn't arrive]

    CheckArrivedTime -->|No| ScanRequests
    CheckArrivedTime -->|Yes| TimeoutArrived[Timeout Detected!<br/>Customer didn't load]

    CheckDeliveryTime -->|No| ScanRequests
    CheckDeliveryTime -->|Yes| TimeoutDelivery[Timeout Detected!<br/>Customer didn't unload]

    TimeoutAccepted --> CancelRequest[Update Status: Cancelled<br/>Reason: Navigation Timeout]
    TimeoutArrived --> CancelRequest
    TimeoutDelivery --> CancelRequest

    CancelRequest --> NotifyCustomer[Send Notification:<br/>Request Cancelled Due to Timeout]

    NotifyCustomer --> CheckRobot{Robot<br/>Still Active?}

    CheckRobot -->|Yes| ReturnToBase[Send Robot to Base<br/>Clear Navigation Target]
    CheckRobot -->|No| MarkOffline[Mark Robot as Offline<br/>Log Disconnect]

    ReturnToBase --> ProcessNext[Process Next<br/>Queued Request]
    MarkOffline --> ProcessNext

    ProcessNext --> Wait[Wait 10 Seconds]
    Wait --> CheckRequests

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style TimeoutAccepted fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style TimeoutArrived fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style TimeoutDelivery fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CancelRequest fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style NotifyCustomer fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style ReturnToBase fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style MarkOffline fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style ProcessNext fill:#6B9AC4,stroke:#3D5A80,stroke-width:2px,color:#1E3A5F
```

---

## 11. Weight Validation Logic

Verify laundry loaded/unloaded using HX711 load cell sensor.

```mermaid
flowchart TD
    Start([Weight Sensor Active]) --> ReadSensor[Read HX711 Load Cell<br/>Get Raw ADC Value]

    ReadSensor --> Convert[Convert to Kilograms<br/>Apply Calibration Factor]

    Convert --> Filter[Apply Moving Average Filter<br/>Reduce Noise]

    Filter --> UpdateReading[Update Current Weight]

    UpdateReading --> ReportServer[Report to Server<br/>in Data Exchange]

    ReportServer --> CheckRequest{Active Request<br/>Status?}

    CheckRequest -->|ArrivedAtRoom| CheckLoad{Weight ><br/>Min Threshold?}
    CheckRequest -->|FinishedWashingArrivedAtRoom| CheckUnload{Weight <<br/>Min Threshold?}
    CheckRequest -->|Other| Delay

    CheckLoad -->|No| WaitLoad[Wait for Customer<br/>to Load Laundry]
    CheckLoad -->|Yes| ValidateLoad{Weight <<br/>Max Threshold?}

    ValidateLoad -->|No| Overweight[Alert: Overweight!<br/>Exceeds Maximum]
    ValidateLoad -->|Yes| LoadConfirmed[Laundry Loaded<br/>Enable Confirmation Button]

    CheckUnload -->|No| WaitUnload[Wait for Customer<br/>to Unload Laundry]
    CheckUnload -->|Yes| UnloadConfirmed[Laundry Unloaded<br/>Enable Confirmation Button]

    WaitLoad --> Delay
    WaitUnload --> Delay
    Overweight --> Delay
    LoadConfirmed --> Delay
    UnloadConfirmed --> Delay

    Delay[Wait 500ms] --> ReadSensor

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style Convert fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style Filter fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style ReportServer fill:#6B9AC4,stroke:#3D5A80,stroke-width:2px,color:#1E3A5F
    style LoadConfirmed fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    style UnloadConfirmed fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    style Overweight fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style WaitLoad fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style WaitUnload fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
```

---

## 12. Cancellation Flow

What happens when a request is cancelled by customer or admin.

```mermaid
flowchart TD
    Start([Request Cancellation Initiated]) --> GetRequest[Get Request Details]

    GetRequest --> CheckStatus{Current<br/>Status?}

    CheckStatus -->|Pending| SimplCancel[Update Status: Cancelled<br/>No Robot Action Needed]
    CheckStatus -->|Accepted| CancelActive[Update Status: Cancelled<br/>Robot En Route]
    CheckStatus -->|ArrivedAtRoom| CancelAtRoom[Update Status: Cancelled<br/>Robot at Customer Room]
    CheckStatus -->|LaundryLoaded| CancelLoaded[Update Status: Cancelled<br/>Robot Has Laundry]
    CheckStatus -->|Washing| CancelWashing[Update Status: Cancelled<br/>Laundry Being Washed]
    CheckStatus -->|FinishedWashingGoingToRoom| CancelDelivery[Update Status: Cancelled<br/>Robot Delivering]
    CheckStatus -->|FinishedWashingArrivedAtRoom| CancelDelivered[Update Status: Cancelled<br/>Robot at Room for Delivery]

    SimplCancel --> NotifyCustomer[Send Cancellation<br/>Notification]

    CancelActive --> SendToBase1[Set Navigation Target: Base<br/>Robot Returns to Base]
    CancelAtRoom --> SendToBase1
    CancelLoaded --> SendToBase1

    SendToBase1 --> WaitReturn1[Monitor Robot Return]

    WaitReturn1 --> AtBase1{Robot At<br/>Base?}
    AtBase1 -->|No| WaitReturn1
    AtBase1 -->|Yes| CompleteCancel1[Robot Available<br/>Process Next Queue]

    CancelWashing --> CheckLaundry{Laundry<br/>Status?}
    CheckLaundry -->|Not Washed| ReturnDirty[Mark: Return Dirty Laundry<br/>to Customer]
    CheckLaundry -->|Washing| FinishWash[Admin Must Finish Washing<br/>Then Deliver or Return]

    CancelDelivery --> SendToBase2[Set Navigation Target: Base<br/>Return Clean Laundry to Base]
    CancelDelivered --> SendToBase2

    SendToBase2 --> WaitReturn2[Monitor Robot Return]
    WaitReturn2 --> AtBase2{Robot At<br/>Base?}
    AtBase2 -->|No| WaitReturn2
    AtBase2 -->|Yes| HoldClean[Hold Clean Laundry at Base<br/>Await Admin Action]

    NotifyCustomer --> ProcessRefund[Process Refund<br/>if Payment Made]
    CompleteCancel1 --> ProcessRefund
    ReturnDirty --> ProcessRefund
    FinishWash --> ProcessRefund
    HoldClean --> ProcessRefund

    ProcessRefund --> End([Cancellation Complete])

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style SimplCancel fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CancelActive fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CancelAtRoom fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CancelLoaded fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CancelWashing fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style CancelDelivery fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CancelDelivered fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style SendToBase1 fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style SendToBase2 fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style CompleteCancel1 fill:#6B9AC4,stroke:#3D5A80,stroke-width:2px,color:#1E3A5F
    style ProcessRefund fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
```

---

## 13. Offline Robot Handling

Robot disconnect detection and recovery.

```mermaid
flowchart TD
    Start([Robot Monitor Running]) --> CheckRobots[Check All Registered Robots]

    CheckRobots --> ScanRobots{For Each<br/>Robot}

    ScanRobots --> CheckPing{LastPing ><br/>Timeout Threshold?}

    CheckPing -->|No| Healthy[Robot Online<br/>Continue Monitoring]
    CheckPing -->|Yes| MarkOffline[Mark Robot as Offline<br/>IsOffline = true]

    MarkOffline --> CheckAssigned{Robot Has<br/>Assigned Request?}

    CheckAssigned -->|No| LogDisconnect[Log Disconnect Event<br/>No Impact on Requests]

    CheckAssigned -->|Yes| GetRequest[Get Active Request<br/>for This Robot]

    GetRequest --> CheckReqStatus{Request<br/>Status?}

    CheckReqStatus -->|Pending| ReassignPending[Keep as Pending<br/>Will Auto-Assign When<br/>Robot Comes Back Online]

    CheckReqStatus -->|Accepted| CancelEnRoute[Cancel Request<br/>Robot Disconnected En Route]
    CheckReqStatus -->|ArrivedAtRoom| CancelAtRoom[Cancel Request<br/>Robot Disconnected at Room]
    CheckReqStatus -->|LaundryLoaded| HandleLoaded[Critical: Robot Has Laundry!<br/>Admin Intervention Required]
    CheckReqStatus -->|FinishedWashingGoingToRoom| HandleDelivery[Critical: Clean Laundry!<br/>Admin Intervention Required]

    CancelEnRoute --> NotifyCustomer[Send Notification:<br/>Service Disruption<br/>Robot Offline]
    CancelAtRoom --> NotifyCustomer

    HandleLoaded --> AlertAdmin[Send Alert to Admin:<br/>Robot Offline with Laundry<br/>Manual Recovery Needed]
    HandleDelivery --> AlertAdmin

    NotifyCustomer --> OfferRefund[Offer Refund or<br/>Reassignment to Another Robot]

    AlertAdmin --> AdminAction[Wait for Admin<br/>to Manually Resolve]

    ReassignPending --> WaitReconnect[Monitor for Reconnection]
    LogDisconnect --> WaitReconnect
    OfferRefund --> WaitReconnect
    AdminAction --> WaitReconnect

    WaitReconnect --> CheckReconnect{Robot<br/>Reconnects?}

    CheckReconnect -->|Yes| Restore[Restore Robot Status<br/>IsOffline = false<br/>Resume Operations]
    CheckReconnect -->|No| WaitReconnect

    Healthy --> Wait
    Restore --> Wait

    Wait[Wait 30 Seconds] --> CheckRobots

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style MarkOffline fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CancelEnRoute fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CancelAtRoom fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style HandleLoaded fill:#E8A0A0,stroke:#C67373,stroke-width:3px,color:#6B3A3A
    style HandleDelivery fill:#E8A0A0,stroke:#C67373,stroke-width:3px,color:#6B3A3A
    style AlertAdmin fill:#E8A0A0,stroke:#C67373,stroke-width:3px,color:#6B3A3A
    style NotifyCustomer fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style Restore fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    style Healthy fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
```

---

## 14. Message & Notification System

Two-way communication between customers and administrators with real-time updates.

```mermaid
flowchart TD
    Start([Customer/Admin Opens Messages]) --> CheckRole{User<br/>Role?}

    CheckRole -->|Customer| CustomerView[Mobile App: Message List]
    CheckRole -->|Admin| AdminView[Web: Conversations Dashboard]

    AdminView --> ShowConversations[Display All Conversations<br/>with Unread Counts]
    ShowConversations --> SelectConvo{Admin Selects<br/>Customer?}

    SelectConvo -->|Yes| LoadThread[Load Conversation Thread<br/>Mark Admin Messages as Read]
    SelectConvo -->|No| PollUnread[AJAX Polling:<br/>Check for New Messages]

    CustomerView --> LoadMessages[Fetch Message History<br/>Paginated by lastMessageId]
    LoadMessages --> DisplayMessages[Display Messages<br/>Admin and Customer]

    DisplayMessages --> MarkRead[Mark Admin Messages<br/>as Read Automatically]

    LoadThread --> AdminCompose{Admin Sends<br/>Message?}
    MarkRead --> CustomerCompose{Customer Sends<br/>Message?}

    AdminCompose -->|Yes| ValidateAdmin[Validate Content<br/>Check Image if Attached]
    CustomerCompose -->|Yes| ValidateCustomer[Validate Content<br/>Check Image if Attached]

    ValidateAdmin --> CheckImage{Image<br/>Attached?}
    ValidateCustomer --> CheckImage

    CheckImage -->|Yes| ValidateImage{Valid Image?<br/>JPEG/PNG/GIF<br/>Max 5MB}
    ValidateImage -->|No| RejectImage[Reject: Invalid Image]
    ValidateImage -->|Yes| SaveImage[Save to File System<br/>UUID-based Filename]

    CheckImage -->|No| SaveMessage[Save Message to Database<br/>Link to Request if Provided]
    SaveImage --> SaveMessage

    SaveMessage --> NotifyRecipient[Trigger Notification<br/>to Recipient]

    NotifyRecipient --> UpdateUI[Real-time UI Update<br/>Show New Message]

    UpdateUI --> PollNew{Continue<br/>Polling?}
    PollUnread --> PollNew

    PollNew -->|Yes| WaitPoll[Wait 2-3 Seconds]
    WaitPoll --> CheckNewMessages[GET /api/messages/new<br/>Since Last ID]

    CheckNewMessages --> HasNew{New Messages<br/>Found?}
    HasNew -->|Yes| UpdateUI
    HasNew -->|No| PollNew

    PollNew -->|No| End([Exit Messages])
    RejectImage --> End

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style RejectImage fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style SaveMessage fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style NotifyRecipient fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style PollUnread fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
```

---

## 15. User Authentication & Management

Customer registration, login, and profile management with JWT token authentication.

```mermaid
flowchart TD
    Start([User Opens App/Web]) --> CheckAuth{Already<br/>Authenticated?}

    CheckAuth -->|Yes| ValidateToken{Token<br/>Valid?}
    ValidateToken -->|Yes| Dashboard([Enter Dashboard])
    ValidateToken -->|No| LoginScreen[Show Login Screen]

    CheckAuth -->|No| LoginScreen

    LoginScreen --> UserChoice{User<br/>Action?}

    UserChoice -->|Login| EnterCreds[Enter Email/Username<br/>and Password]
    UserChoice -->|Register| EnterRegInfo[Enter Registration Info:<br/>FirstName, LastName<br/>Email, Password]

    EnterCreds --> SubmitLogin[POST /api/auth/login]

    SubmitLogin --> ValidateCreds{Credentials<br/>Valid?}
    ValidateCreds -->|No| LoginFail[Show Error Message]
    LoginFail --> LoginScreen

    ValidateCreds -->|Yes| CheckEmailConfirmed{Email<br/>Confirmed?}
    CheckEmailConfirmed -->|No| RequireConfirm[Request Email Confirmation]
    CheckEmailConfirmed -->|Yes| GenerateToken[Generate JWT Token<br/>24-hour Expiration]

    GenerateToken --> ReturnToken[Return Token with:<br/>CustomerId, CustomerName<br/>Token, Expiration]

    ReturnToken --> StoreToken[Store Token Locally<br/>Mobile: SecureStorage<br/>Web: Cookie/LocalStorage]

    EnterRegInfo --> ValidateReg{Registration<br/>Valid?}
    ValidateReg -->|No| RegFail[Show Validation Errors:<br/>Email already exists<br/>Password mismatch<br/>Missing fields]
    RegFail --> EnterRegInfo

    ValidateReg -->|Yes| CreateUser[Create User Account<br/>Hash Password<br/>Assign Member Role]

    CreateUser --> RegSuccess[Registration Successful<br/>Auto-login User]
    RegSuccess --> GenerateToken

    StoreToken --> CheckRole{User<br/>Role?}

    CheckRole -->|Administrator| AdminDashboard[Redirect to Admin Dashboard<br/>Full System Access]
    CheckRole -->|Member| CustomerDashboard[Redirect to Customer Dashboard<br/>Request Management]

    AdminDashboard --> AdminFeatures{Admin<br/>Action?}
    CustomerDashboard --> CustomerFeatures{Customer<br/>Action?}

    AdminFeatures -->|Manage Users| UserCRUD[Create/Edit/Delete Users<br/>Assign Roles<br/>Assign Beacons]
    AdminFeatures -->|Manage Requests| ReqMgmt[Accept/Decline Requests<br/>Monitor Status]
    AdminFeatures -->|View Accounting| ViewAcct[Revenue Reports<br/>Payment Processing]

    CustomerFeatures -->|View Profile| GetProfile[GET /api/user/profile]
    CustomerFeatures -->|Edit Profile| UpdateProfile[PUT /api/user/profile<br/>Update Name, Email, Phone]
    CustomerFeatures -->|Create Request| CreateReq[POST /api/requests]

    RequireConfirm --> LoginScreen
    UserCRUD --> Dashboard
    ReqMgmt --> Dashboard
    ViewAcct --> Dashboard
    GetProfile --> Dashboard
    UpdateProfile --> Dashboard
    CreateReq --> Dashboard

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style Dashboard fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style LoginFail fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style RegFail fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style GenerateToken fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style StoreToken fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style AdminDashboard fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style CustomerDashboard fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
```

---

## 16. Payment & Accounting Workflow

Complete financial management from payment processing to revenue reporting.

```mermaid
flowchart TD
    Start([Request Completed]) --> CheckPayment{Payment<br/>Exists?}

    CheckPayment -->|No| CreatePayment[Create Payment Record<br/>Amount = Weight × RatePerKg<br/>Status = Pending]
    CheckPayment -->|Yes| GetPayment[Get Existing Payment]

    CreatePayment --> PaymentDashboard[Admin: Accounting Dashboard]
    GetPayment --> PaymentDashboard

    PaymentDashboard --> ShowMetrics[Display Metrics:<br/>Total Revenue<br/>Outstanding Amounts<br/>Payment Breakdown]

    ShowMetrics --> AdminAction{Admin<br/>Action?}

    AdminAction -->|Mark as Paid| SelectMethod[Select Payment Method:<br/>Cash, Card, PayPal, etc.]
    SelectMethod --> RecordPayment[Update Payment:<br/>Status = Completed<br/>CompletedAt = Now<br/>Method = Selected]

    AdminAction -->|Issue Refund| EnterRefund[Enter Refund Amount<br/>and Reason]
    EnterRefund --> ValidateRefund{Amount <=<br/>Paid Amount?}
    ValidateRefund -->|No| RefundError[Show Error:<br/>Exceeds Payment]
    ValidateRefund -->|Yes| ProcessRefund[Update Payment:<br/>Status = Refunded<br/>RefundAmount<br/>RefundReason<br/>RefundedAt = Now]

    AdminAction -->|Mark as Failed| EnterFailReason[Enter Failure Reason]
    EnterFailReason --> MarkFailed[Update Payment:<br/>Status = Failed<br/>FailureReason]

    AdminAction -->|Cancel Payment| EnterCancelReason[Enter Cancellation Reason]
    EnterCancelReason --> MarkCancelled[Update Payment:<br/>Status = Cancelled<br/>CancellationReason<br/>CancelledAt = Now]

    AdminAction -->|Create Adjustment| SelectAdjType{Adjustment<br/>Type?}
    SelectAdjType -->|Add Revenue| AddRevenue[Create PaymentAdjustment:<br/>Type = AddRevenue<br/>Amount = +Amount]
    SelectAdjType -->|Subtract Revenue| SubRevenue[Create PaymentAdjustment:<br/>Type = SubtractRevenue<br/>Amount = -Amount]
    SelectAdjType -->|Supply Expense| AddExpense[Create PaymentAdjustment:<br/>Type = SupplyExpense<br/>Amount = -Amount]
    SelectAdjType -->|Complete Payment| CompleteAdjustment[Create PaymentAdjustment:<br/>Type = CompletePayment<br/>Link to RequestId]

    RecordPayment --> UpdateRevenue[Calculate Total Revenue:<br/>Completed - Refunded]
    ProcessRefund --> UpdateRevenue
    MarkFailed --> UpdateRevenue
    MarkCancelled --> UpdateRevenue
    AddRevenue --> UpdateRevenue
    SubRevenue --> UpdateRevenue
    AddExpense --> UpdateRevenue
    CompleteAdjustment --> UpdateRevenue

    UpdateRevenue --> CalcProfit[Calculate Net Profit:<br/>Total Revenue - Expenses]

    CalcProfit --> AdminReport{Generate<br/>Report?}

    AdminReport -->|Yes| SelectPeriod{Report<br/>Period?}
    SelectPeriod -->|Today| ReportToday[Filter: Created Today]
    SelectPeriod -->|This Week| ReportWeek[Filter: Last 7 Days]
    SelectPeriod -->|This Month| ReportMonth[Filter: Current Month]
    SelectPeriod -->|This Year| ReportYear[Filter: Current Year]
    SelectPeriod -->|Custom| ReportCustom[Filter: Date Range]

    ReportToday --> GenerateReport[Generate Sales Report:<br/>Revenue by Method<br/>Top Customers<br/>Transaction Counts<br/>Daily/Hourly Trends]
    ReportWeek --> GenerateReport
    ReportMonth --> GenerateReport
    ReportYear --> GenerateReport
    ReportCustom --> GenerateReport

    GenerateReport --> ExportChoice{Export<br/>Format?}
    ExportChoice -->|View| DisplayReport[Display Report in Browser]
    ExportChoice -->|Print| PrintReport[Print-Friendly View]
    ExportChoice -->|CSV| ExportCSV[Download CSV File]
    ExportChoice -->|PDF| ExportPDF[Generate PDF Report]

    DisplayReport --> End([Accounting Dashboard])
    PrintReport --> End
    ExportCSV --> End
    ExportPDF --> End
    RefundError --> AdminAction
    AdminReport -->|No| End

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style RefundError fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style RecordPayment fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style ProcessRefund fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style GenerateReport fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style UpdateRevenue fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
```

---

## 17. API Architecture & Data Exchange

Bidirectional communication between mobile app, robot, and web server.

```mermaid
flowchart TD
    Start([Client Initiates Request]) --> IdentifyClient{Client<br/>Type?}

    IdentifyClient -->|Mobile App| MobileAuth[Authorization Header:<br/>Bearer JWT Token]
    IdentifyClient -->|Robot| RobotAuth[URL Parameter:<br/>Robot Name]
    IdentifyClient -->|Web Browser| WebAuth[Cookie-based Session:<br/>ASP.NET Identity]

    MobileAuth --> ValidateJWT{Token<br/>Valid?}
    ValidateJWT -->|No| Unauthorized([401 Unauthorized])
    ValidateJWT -->|Yes| ExtractClaims[Extract Claims:<br/>CustomerId<br/>CustomerName]

    RobotAuth --> CheckRobotReg{Robot<br/>Registered?}
    CheckRobotReg -->|No| RegisterRobot[POST /api/robot/register<br/>Capture IP Address]
    CheckRobotReg -->|Yes| ValidateRobot[Load Robot State<br/>from Memory]
    RegisterRobot --> ValidateRobot

    WebAuth --> ValidateSession{Session<br/>Valid?}
    ValidateSession -->|No| RedirectLogin[Redirect to Login Page]
    ValidateSession -->|Yes| CheckRole[Check User Role<br/>Administrator/Member]

    ExtractClaims --> RouteAPI{API<br/>Endpoint?}
    ValidateRobot --> RouteAPI
    CheckRole --> RouteWeb{Web<br/>Endpoint?}

    RouteAPI -->|/api/requests| RequestAPI[Request Management API:<br/>Create Request<br/>Get Status<br/>Confirm Actions]
    RouteAPI -->|/api/messages| MessageAPI[Messaging API:<br/>Send/Receive<br/>Mark Read<br/>Get Unread Count]
    RouteAPI -->|/api/payment| PaymentAPI[Payment API:<br/>Process Payment<br/>Get History<br/>Check Status]
    RouteAPI -->|/api/robot/data-exchange| DataExchange[Robot Data Exchange:<br/>Robot → Server: Beacons, Weight, Status<br/>Server → Robot: Targets, Commands]
    RouteAPI -->|/api/user| UserAPI[User API:<br/>Get Profile<br/>Update Profile<br/>Get Admins]

    RouteWeb -->|/dashboard| DashboardMVC[Dashboard Controller:<br/>Overview, Settings]
    RouteWeb -->|/requests| RequestMVC[Request Controller:<br/>Accept, Decline, Manage]
    RouteWeb -->|/accounting| AccountingMVC[Accounting Controller:<br/>Payments, Reports, Adjustments]
    RouteWeb -->|/robots| RobotMVC[Robot Controller:<br/>Status, Control, Camera]
    RouteWeb -->|/messages| MessageMVC[Message Controller:<br/>Conversations, Responses]
    RouteWeb -->|/users| UserMVC[User Controller:<br/>CRUD, Roles, Beacons]
    RouteWeb -->|/beacons| BeaconMVC[Beacon Controller:<br/>CRUD, Configuration]

    RequestAPI --> ProcessRequest[Process Business Logic:<br/>Validate Input<br/>Check Permissions<br/>Execute Operation]
    MessageAPI --> ProcessRequest
    PaymentAPI --> ProcessRequest
    DataExchange --> ProcessRequest
    UserAPI --> ProcessRequest

    DashboardMVC --> ProcessRequest
    RequestMVC --> ProcessRequest
    AccountingMVC --> ProcessRequest
    RobotMVC --> ProcessRequest
    MessageMVC --> ProcessRequest
    UserMVC --> ProcessRequest
    BeaconMVC --> ProcessRequest

    ProcessRequest --> DatabaseOp{Database<br/>Operation?}

    DatabaseOp -->|Read| QueryDB[EF Core Query:<br/>Get Entities<br/>Apply Filters<br/>Include Relations]
    DatabaseOp -->|Write| WriteDB[EF Core Write:<br/>Add/Update/Delete<br/>SaveChanges<br/>Transaction]

    QueryDB --> MapToDTO[Map Entity to DTO:<br/>Exclude Sensitive Data<br/>Format for Client]
    WriteDB --> MapToDTO

    MapToDTO --> FormatResponse{Response<br/>Format?}

    FormatResponse -->|JSON API| ReturnJSON[Return JSON:<br/>Status Code<br/>Data Object<br/>Error Messages]
    FormatResponse -->|HTML View| ReturnView[Return Razor View:<br/>Render HTML<br/>Include Data Model]

    ReturnJSON --> ClientReceive[Client Receives Response]
    ReturnView --> ClientReceive

    ClientReceive --> ClientProcess{Success?}

    ClientProcess -->|Yes| UpdateUI[Update Client UI:<br/>Display Data<br/>Show Confirmation]
    ClientProcess -->|No| ShowError[Show Error Message:<br/>Validation Errors<br/>System Errors]

    UpdateUI --> End([Request Complete])
    ShowError --> End
    Unauthorized --> End
    RedirectLogin --> End

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style Unauthorized fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style ShowError fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style DataExchange fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style ProcessRequest fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style MapToDTO fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
```

---

## 18. Manual Request Creation (Admin-Initiated)

Admin-created manual requests for walk-in customers or assisted requests.

```mermaid
flowchart TD
    Start([Admin Clicks Create Manual Request]) --> LoadCustomers[Load Active Customers<br/>Load Available Robots]

    LoadCustomers --> ShowModal[Display Modal:<br/>Customer Selection<br/>Request Type<br/>Weight Input<br/>Notes]

    ShowModal --> AdminInput{Admin<br/>Submits?}
    AdminInput -->|Cancel| Cancel([Modal Closed])

    AdminInput -->|Submit| ValidateCustomer{Customer<br/>Selected?}
    ValidateCustomer -->|No| Error1([Error: Customer Required])
    ValidateCustomer -->|Yes| CheckDupe{Customer Has<br/>Active Request?}

    CheckDupe -->|Yes| Error2([Error: Duplicate Active Request])
    CheckDupe -->|No| CheckType{Request<br/>Type?}

    CheckType -->|Walk-In| ValidateWeight{Weight<br/>Provided?}
    ValidateWeight -->|No| Error3([Error: Weight Required for Walk-In])
    ValidateWeight -->|Yes| CheckWeightRange{Weight<br/>0.1-50 kg?}
    CheckWeightRange -->|No| Error4([Error: Invalid Weight Range])

    CheckWeightRange -->|Yes| CreateWalkIn[Create Request:<br/>Type = WalkIn<br/>Status = Washing<br/>AssignedRobotName = WALK_IN<br/>Weight = Input<br/>Cost = Weight × Rate<br/>Instructions = ADMIN_MANUAL]

    CheckType -->|Robot Delivery| CheckRobots{Robots<br/>Available?}
    CheckRobots -->|No| Error5([Error: No Robots Online])
    CheckRobots -->|Yes| AssignRobot[Auto-Assign Robot<br/>Get Customer Beacon]

    AssignRobot --> CheckBeacon{Customer Has<br/>Beacon?}
    CheckBeacon -->|No| Error6([Error: Customer Room Not Configured])
    CheckBeacon -->|Yes| CreateRobotReq[Create Request:<br/>Type = RobotDelivery<br/>Status = Accepted<br/>AssignedRobotId<br/>Instructions = ADMIN_MANUAL<br/>Start Line Following]

    CreateWalkIn --> NotifyCustomer[Send Notification:<br/>Admin Created Request<br/>Mobile App Badge]
    CreateRobotReq --> NotifyCustomer

    NotifyCustomer --> RefreshDash[Refresh Dashboard<br/>Show Success Message]

    RefreshDash --> End([Request Created])

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style Error1 fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Error2 fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Error3 fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Error4 fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Error5 fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style Error6 fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CreateWalkIn fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style CreateRobotReq fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style NotifyCustomer fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
```

---

## 19. Admin Request Management

Administrator workflow for accepting, declining, and managing laundry requests.

```mermaid
flowchart TD
    Start([Admin Opens Request Dashboard]) --> LoadRequests[Load All Requests<br/>with Filters:<br/>Status, Date, Customer]

    LoadRequests --> DisplayRequests[Display Request List:<br/>Pending Requests Top<br/>Active Requests<br/>Completed Requests<br/>Available Robots Count<br/>Create Manual Request Button]

    DisplayRequests --> AdminSelect{Admin<br/>Selects Request?}

    AdminSelect -->|No| AutoRefresh[Auto-refresh Every 5s]
    AutoRefresh --> LoadRequests

    AdminSelect -->|Yes| CheckReqStatus{Request<br/>Status?}

    CheckReqStatus -->|Pending| ShowActions[Show Actions:<br/>Accept<br/>Decline]
    CheckReqStatus -->|Accepted/InProgress| ShowMonitor[Show Monitoring:<br/>Robot Status<br/>Location<br/>Cancel Option]
    CheckReqStatus -->|ArrivedAtRoom| ShowWaiting[Waiting for Customer<br/>to Load Laundry<br/>Cancel Option]
    CheckReqStatus -->|Washing| ShowWashing[Laundry Washing<br/>Mark Done<br/>Start Delivery]
    CheckReqStatus -->|Completed| ShowPayment[Payment Processing<br/>Mark as Paid<br/>Issue Refund]

    ShowActions --> AdminChoice{Admin<br/>Action?}

    AdminChoice -->|Accept| CheckRobots[Get Available Robots]
    AdminChoice -->|Decline| EnterReason[Enter Decline Reason]

    CheckRobots --> AnyRobots{Available<br/>Robots?}
    AnyRobots -->|No| CheckBusy[Get All Active Robots]
    CheckBusy --> SelectLRU[Select Least Recently Used Robot<br/>Reassign from Current Task]

    AnyRobots -->|Yes| SelectAvailable[Select First Available Robot]

    SelectAvailable --> AssignRobot[Assign Robot to Request:<br/>Set AssignedRobotName<br/>Update Robot Status = Busy<br/>Set Robot Task]
    SelectLRU --> AssignRobot

    AssignRobot --> UpdateStatus[Update Request Status:<br/>Status = Accepted<br/>AcceptedAt = Now<br/>AcceptedBy = Admin]

    UpdateStatus --> ConfigureNav[Configure Navigation:<br/>Get Customer Beacon<br/>Set as NavigationTarget<br/>Start Line Following]

    ConfigureNav --> NotifyCustomer[Send Notification:<br/>Robot on the way!]

    EnterReason --> DeclineRequest[Update Request:<br/>Status = Declined<br/>DeclineReason<br/>DeclinedAt = Now]

    DeclineRequest --> NotifyDecline[Send Notification:<br/>Request Declined]

    ShowMonitor --> MonitorAction{Admin<br/>Action?}
    MonitorAction -->|Cancel| ConfirmCancel{Confirm<br/>Cancellation?}
    ConfirmCancel -->|No| ShowMonitor
    ConfirmCancel -->|Yes| CancelRequest[Update Request:<br/>Status = Cancelled<br/>Free Robot<br/>Return to Base]

    ShowWashing --> WashAction{Admin<br/>Action?}
    WashAction -->|Mark Done| MarkFinished[Update Status:<br/>FinishedWashing]
    WashAction -->|Start Delivery| CheckRobotsOnline{Any Robots<br/>Online?}

    CheckRobotsOnline -->|No| ErrorNoBots([Error: No Bots Active<br/>Cannot Start Delivery])
    CheckRobotsOnline -->|Yes| StartDelivery[Update Status:<br/>FinishedWashingGoingToRoom<br/>Robot Delivers to Customer]

    MarkFinished --> NotifyReady[Notify Customer:<br/>Laundry Ready for Pickup]
    StartDelivery --> NotifyDelivering[Notify Customer:<br/>Robot Delivering Clean Laundry]
    ErrorNoBots --> UpdateDashboard

    ShowWaiting --> WaitTimeout{Timeout<br/>Reached?}
    WaitTimeout -->|Yes| AutoCancel[Auto-cancel Request<br/>Robot Returns to Base]
    WaitTimeout -->|No| ContinueWait[Continue Monitoring]

    ShowPayment --> PaymentAction{Payment<br/>Action?}
    PaymentAction -->|Mark Paid| RecordPayment[Record Payment:<br/>Status = Completed<br/>Method = Cash/Card<br/>CompletedAt = Now]
    PaymentAction -->|Refund| IssueRefund[Process Refund:<br/>Status = Refunded<br/>RefundAmount<br/>RefundReason]

    NotifyCustomer --> UpdateDashboard[Refresh Dashboard:<br/>Update Request List]
    NotifyDecline --> UpdateDashboard
    CancelRequest --> UpdateDashboard
    NotifyReady --> UpdateDashboard
    NotifyDelivering --> UpdateDashboard
    AutoCancel --> UpdateDashboard
    RecordPayment --> UpdateDashboard
    IssueRefund --> UpdateDashboard
    ContinueWait --> UpdateDashboard

    UpdateDashboard --> CheckQueue[Check Pending Queue]

    CheckQueue --> HasPending{Pending<br/>Requests?}
    HasPending -->|Yes| AutoProcess[Auto-process Next Pending<br/>If Auto-Accept Enabled]
    HasPending -->|No| End([Dashboard Ready])

    AutoProcess --> End

    style Start fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    style End fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    style DeclineRequest fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style CancelRequest fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style AutoCancel fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style ErrorNoBots fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    style AssignRobot fill:#F4D19B,stroke:#D4A574,stroke-width:2px,color:#6B4E2A
    style NotifyCustomer fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    style ShowWashing fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    style UpdateDashboard fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
```

---

## Color Legend

- 🟢 **Soft Green** - Start/Entry points
- 🔵 **Muted Blue** - Completion/Success states
- 🔴 **Soft Coral** - Errors/Rejections/Cancellations
- 🟡 **Soft Amber** - Active/In-progress states
- 🟣 **Soft Lavender** - Processing/Washing states
- 🟠 **Soft Peach** - Waiting/Queued states
- 🌿 **Muted Mint** - Information/Configuration states

---

## How to View

- **GitHub**: These diagrams will render automatically when viewing this file on GitHub
- **VS Code**: Install "Markdown Preview Mermaid Support" extension
- **Export to Images**: Copy the Mermaid code to https://mermaid.live/
- **Documentation**: Most modern documentation tools support Mermaid syntax
