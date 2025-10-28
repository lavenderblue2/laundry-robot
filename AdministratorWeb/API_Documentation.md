# Laundry Service API Documentation

## Authentication

### Generate JWT Token
**POST** `/api/auth/token`

Request Body:
```json
{
  "customerId": "customer123",
  "customerName": "John Doe"
}
```

Response:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "customerId": "customer123",
  "customerName": "John Doe",
  "expiresAt": "2025-01-06T10:30:00Z"
}
```

## Requests

### Create Laundry Request
**POST** `/api/requests`

Headers:
```
Authorization: Bearer {jwt_token}
```

Request Body:
```json
{
  "customerId": "customer123",
  "customerName": "John Doe",
  "customerPhone": "+1234567890",
  "address": "123 Main St, City, State",
  "instructions": "Handle with care",
  "type": 2,
  "scheduledAt": "2025-01-06T14:00:00Z"
}
```

Response:
```json
{
  "id": 1,
  "status": "Pending",
  "message": "Request submitted successfully"
}
```

### Get Request Status
**GET** `/api/requests/status/{requestId}`

Headers:
```
Authorization: Bearer {jwt_token}
```

Response:
```json
{
  "id": 1,
  "status": "Completed",
  "weight": 5.2,
  "totalCost": 78.00,
  "requestedAt": "2025-01-05T10:00:00Z",
  "scheduledAt": "2025-01-06T14:00:00Z",
  "completedAt": "2025-01-06T16:30:00Z",
  "assignedRobot": "LaundryBot-01",
  "declineReason": null
}
```

## Payments

### Process Payment
**POST** `/api/payment/{requestId}/pay`

Headers:
```
Authorization: Bearer {jwt_token}
```

Request Body:
```json
{
  "paymentMethod": 1,
  "paymentReference": "GCASH-1234567890",
  "notes": "GCash manual payment"
}
```

Response:
```json
{
  "paymentId": 1,
  "transactionId": "TXN_20250105_ABC12345",
  "amount": 78.00,
  "status": "Completed",
  "processedAt": "2025-01-05T17:00:00Z",
  "message": "Payment processed successfully"
}
```

### Get Payment Status
**GET** `/api/payment/{requestId}/payment-status`

Headers:
```
Authorization: Bearer {jwt_token}
```

Response:
```json
{
  "requestId": 1,
  "isPaid": true,
  "totalCost": 78.00,
  "weight": 5.2,
  "payment": {
    "paymentId": 1,
    "amount": 78.00,
    "method": "GCash",
    "status": "Completed",
    "transactionId": "TXN_20250105_ABC12345",
    "processedAt": "2025-01-05T17:00:00Z"
  }
}
```

### Get Payment History
**GET** `/api/payment/history`

Headers:
```
Authorization: Bearer {jwt_token}
```

Response:
```json
[
  {
    "id": 1,
    "amount": 78.00,
    "method": "GCash",
    "status": "Completed",
    "transactionId": "TXN_20250105_ABC12345",
    "createdAt": "2025-01-05T17:00:00Z",
    "processedAt": "2025-01-05T17:00:00Z",
    "request": {
      "id": 1,
      "type": "PickupAndDelivery",
      "address": "123 Main St, City, State",
      "weight": 5.2,
      "requestedAt": "2025-01-05T10:00:00Z"
    }
  }
]
```

## Enums

### RequestType
- `0` = Pickup
- `1` = Delivery  
- `2` = PickupAndDelivery

### RequestStatus
- `0` = Pending
- `1` = Accepted
- `2` = InProgress
- `3` = Completed
- `4` = Declined
- `5` = Cancelled

### PaymentMethod
- `0` = Cash
- `1` = GCash

### PaymentStatus
- `0` = Pending
- `1` = Completed
- `2` = Failed
- `3` = Refunded
- `4` = Cancelled