# Architecture

## Overview

A single ASP.NET Core 10 Web API backed by SQLite. The frontend (React or
Next) is not yet built; the API is fully functional and testable in isolation
via HTTP.

```mermaid
flowchart LR
    Client[Client<br/>React SPA / curl] -->|HTTPS + JWT| Kestrel[Kestrel<br/>ASP.NET Core 10]
    Kestrel --> AuthController
    Kestrel --> EmployeesController

    AuthController --> IAuthService
    EmployeesController --> IEmployeeService

    IAuthService --> AppDbContext
    IEmployeeService --> AppDbContext
    IAuthService --> IPasswordHasher
    IAuthService --> ITokenService

    AppDbContext --> SQLite[(SQLite<br/>salary-management.db)]