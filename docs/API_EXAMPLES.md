# KamatekCRM API - Example Requests

Here are working examples for interacting with the backend API.

## 1. Authentication (Login)
**POST** `http://localhost:5050/api/auth/login`

**Body (JSON):**
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
  "message": "Giriş başarılı.",
  "token": "eyJhbGciOi...",
  "userId": 1,
  "fullName": "Admin User",
  "role": "Admin"
}
```

## 2. Get Assigned Tasks (Technician)
**GET** `http://localhost:5050/api/technician/tasks`

**Headers:**
`Authorization: Bearer <YOUR_TOKEN>`

**Response:**
```json
[
  {
    "id": 1,
    "title": "Camera Installation",
    "status": 0,
    "customer": { "fullName": "John Doe", ... }
  }
]
```

## 3. Update Task Status
**PUT** `http://localhost:5050/api/technician/tasks/1/status`

**Headers:**
`Authorization: Bearer <YOUR_TOKEN>`

**Body (JSON):**
```json
2  // (JobStatus enum value: 2 = WaitingForParts, 4 = Completed)
```

## 4. Admin Create Task
**POST** `http://localhost:5050/api/admin/tasks`

**Headers:**
`Authorization: Bearer <YOUR_TOKEN>`

**Body (JSON):**
```json
{
  "title": "Server Maintenance",
  "customerId": 1,
  "description": "Routine checkup",
  "assignedTechnicianId": 2,
  "status": 0
}
```
