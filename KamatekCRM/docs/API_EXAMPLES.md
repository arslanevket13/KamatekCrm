# KamatekCRM API - Developer Guide & Examples

Base URL: `http://localhost:5050`
Remote URL: `http://[SERVER_IP]:5050`

## 1. Authentication

All protected endpoints require a JWT Token in the header:
`Authorization: Bearer <YOUR_TOKEN>`

### Login
**POST** `/api/auth/login`

**Request:**
```json
{
  "username": "admin.user",
  "password": "123"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Login successful",
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "User": {
      "id": 1,
      "username": "admin.user",
      "fullName": "Admin User",
      "role": "Admin"
    }
  }
}
```

---

## 2. Tasks (Technician)

### Get My Tasks
**GET** `/api/tasks`
*Returns tasks assigned to the logged-in technician.*

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 105,
      "title": "CCTV Installation",
      "description": "Install 4 cameras at the entrance.",
      "status": 0, // 0: Pending, 1: InProgress, 2: Completed
      "priority": 2, // 0: Low, 1: Medium, 2: High
      "scheduledDate": "2023-10-27T14:30:00",
      "customer": {
        "id": 12,
        "name": "John Doe",
        "address": "123 Main St, Istanbul",
        "phone": "555-0123",
        "latitude": 41.0082,
        "longitude": 28.9784
      }
    }
  ]
}
```

### Get Task Details
**GET** `/api/tasks/{id}`

### Update Task Status
**PUT** `/api/tasks/{id}/status`

**Request:**
```json
{
  "status": 1, // JobStatus Enum
  "note": "Arrived at location, starting work." // Optional
}
```

---

## 3. Photos & Attachments

### Upload Photo
**POST** `/api/tasks/{id}/photos`
*Content-Type: multipart/form-data*

**Form Data:**
*   `file`: (Binary File)
*   `description`: "Before installation"

**Response:**
```json
{
  "success": true,
  "data": {
    "id": 55,
    "url": "/uploads/tasks/105/photo_guid.jpg",
    "thumbnailUrl": "/uploads/tasks/105/thumb_photo_guid.jpg",
    "createdAt": "2023-10-27T15:00:00"
  }
}
```

---

## 4. Admin Operations

### Create Task
**POST** `/api/tasks` (Admin only)

**Request:**
```json
{
  "title": "Server Maintenance",
  "description": "Monthly routine check.",
  "customerId": 12,
  "assignedTechnicianId": 5,
  "priority": 1,
  "scheduledDate": "2023-11-01T09:00:00"
}
```
