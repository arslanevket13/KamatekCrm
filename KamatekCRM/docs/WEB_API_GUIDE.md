# KamatekCRM Web API & Technician Integration Guide

This document describes the internal Web API that powers the Blazor Technician App and the security protocols ensuring safe remote access.

## 1. API Architecture (Self-Hosting)

The API is an ASP.NET Core application self-hosted inside the WPF process using the **Kestrel** web server.

### Configuration
*   **Binding**: Binds to `http://0.0.0.0:5050` to accept connections from any network interface (Local + LAN).
*   **Lifecycle**: The API starts automatically when the WPF application initializes via `ProcessManager.StartServices()`.

---

## 2. Authentication Protocol (JWT)

The system uses **JSON Web Token (JWT)** for stateless, secure authentication.

### Authentication Flow:
1.  **Login**: Technician sends credentials to `/api/auth/login`.
2.  **Validation**: `AuthService` validates the user against the PostgreSQL database.
3.  **Token Issuance**: API generates a JWT signed with a Secure Symmetric Key.
4.  **Authorization**: Technician includes the token in the `Authorization: Bearer <token>` header for subsequent requests.

### Token Security:
*   **Algorithm**: HS256.
*   **Payload**: Includes User ID, Username, and Role claims.
*   **Validation**: Each request is validated against the Issuer and Audience defined in `appsettings.json`.

---

## 3. Technician Workflow API

The API exposes endpoints specifically designed for the mobile technician experience:

### 3.1 Task Management
*   **GET `/api/tasks`**: Retrieves assigned tasks for the authenticated technician.
*   **GET `/api/tasks/{id}`**: Detailed task view with customer location and device history.
*   **PUT `/api/tasks/{id}/status`**: Updates the job status (e.g., In Progress, Completed) and logs the transition.

### 3.2 Digital Evidence (Photos)
*   **POST `/api/tasks/{id}/photos`**: Allows technicians to upload site photos (Before/After) directly from their device.
*   **Storage**: Photos are stored on the server's local disk and served via static file middleware.

---

## 4. Remote Access Security

To allow technicians to access the API from outside the local network:
*   **Firewall**: Port 5050 and 7000 must be open (`Add-NetFirewallRule`).
*   **VPN/DDNS**: It is recommended to use a VPN or a Dynamic DNS service to expose the WPF host securely.
