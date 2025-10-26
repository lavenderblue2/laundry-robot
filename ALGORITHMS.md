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

    SetTargets --> GetFloorColor{Room Has<br/>Floor Color?}
    GetFloorColor -->|Yes| SendWithColor[Send Active Beacons<br/>+ Navigation Targets<br/>+ Floor Color DISABLED]
    GetFloorColor -->|No| SendBeaconOnly[Send Active Beacons<br/>+ Navigation Targets<br/>Beacon-Only Mode]

    NoTarget --> SendEmpty[Send Active Beacons<br/>All IsNavigationTarget=false]

    SendWithColor --> RobotNavigates[Robot Checks Beacon RSSI<br/>Reports When RSSI >= Threshold]
    SendBeaconOnly --> RobotNavigates
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
