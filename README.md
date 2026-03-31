# Smart HR Attendance & Payroll Management

<div align="center">

# Smart HR  
**Business-Oriented HR Operations Platform for Attendance, Payroll, Leave, Scheduling, Overtime, and AI-Assisted Review**

![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-512BD4?logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-Backend-239120?logo=csharp&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-ORM-7A3E9D)
![SQL Server](https://img.shields.io/badge/SQL%20Server-Database-CC2927?logo=microsoftsqlserver&logoColor=white)
![JWT](https://img.shields.io/badge/JWT-Authentication-000000?logo=jsonwebtokens)
![Redis](https://img.shields.io/badge/Redis-Cache-DC382D?logo=redis&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Containerized-2496ED?logo=docker&logoColor=white)
![Chart.js](https://img.shields.io/badge/Chart.js-Dashboard-FF6384?logo=chartdotjs&logoColor=white)
![Status](https://img.shields.io/badge/Status-Portfolio%20Ready-16A34A)
![Architecture](https://img.shields.io/badge/Architecture-Role--Based%20Multi--Workspace-2563EB)
![AI](https://img.shields.io/badge/AI-Payroll%20Summary%20Assistant-8B5CF6)

</div>

---

## Business Production Highlights

Smart HR is not a basic CRUD demo. It is designed as a **business-style, multi-workspace HR platform** with:

- **4 role experiences**: Admin, HR, Manager, Employee
- **separate portals and navigation flows** instead of one overloaded dashboard
- **attendance, leave, payroll, overtime, and scheduling workflows**
- **approval and audit-aware operations**
- **manager scope restrictions**
- **self-service portal for employees**
- **AI Payroll Summary Assistant** for payroll anomaly and executive-style review
- **engineering depth** through JWT auth, refresh token, cache, logging direction, audit direction, and Docker readiness

This project is suitable for:

- thesis / capstone / final project demonstration
- portfolio / GitHub showcase
- CV project description
- interview discussion around business rules and system design

---

## Table of Contents

- [Business Production Highlights](#business-production-highlights)
- [Project Overview](#project-overview)
- [Main Roles](#main-roles)
- [Feature Coverage](#feature-coverage)
- [AI Payroll Summary Assistant](#ai-payroll-summary-assistant)
- [Business Rules](#business-rules)
- [Tech Stack](#tech-stack)
- [Architecture Diagram (Text)](#architecture-diagram-text)
- [Project Tree](#project-tree)
- [Screenshots](#screenshots)
- [Demo Accounts / Notes](#demo-accounts--notes)
- [How to Run Locally](#how-to-run-locally)
- [Optional Docker Run](#optional-docker-run)
- [API Coverage Summary](#api-coverage-summary)
- [Engineering Depth](#engineering-depth)
- [Suggested Demo Flow](#suggested-demo-flow)
- [Future Improvements](#future-improvements)

---

## Project Overview

**Smart HR** is a role-based HR management platform that helps organizations manage:

- employee records
- department and position structures
- attendance tracking
- attendance adjustment workflows
- payroll generation and payroll review
- leave request workflows
- overtime requests and approvals
- shift and schedule assignments
- reports, summaries, and exports
- audit-oriented activity review
- AI-generated payroll summaries

The project separates **operational users** from **self-service users**:

- **Admin / HR / Manager** work in a business workspace
- **Employee** works in a self-service portal

This design makes the system more realistic, clearer to use, and easier to scale into a production-like HR platform.

---

## Main Roles

### 1. Admin
Admin has organization-wide control.

**Admin can:**
- manage employees
- manage departments and positions
- manage attendance records
- generate and review payroll
- review leave workflows
- review overtime workflows
- manage schedules and shifts
- review reports and dashboards
- use AI payroll summary
- monitor audit-heavy operational data
- perform governance-oriented actions

---

### 2. HR
HR focuses on daily workforce operations.

**HR can:**
- manage employee operations
- review attendance and adjustment workflows
- review leave workflows
- support payroll operations
- use reports and dashboards
- use AI summary for payroll insight
- work in the Admin/HR business workspace

---

### 3. Manager
Manager works with limited scope.

**Manager can:**
- review leave requests within scope
- review overtime requests within scope
- view scoped operational reports
- monitor team-level actions
- work only with permitted department/team data

---

### 4. Employee
Employee uses a self-service portal.

**Employee can:**
- view personal attendance records
- view personal payroll history
- submit leave requests
- submit overtime requests
- track request status and timeline
- update personal profile
- change password
- review recent HR activity affecting their records

---

## Feature Coverage

Below is the full functional scope of the current Smart HR system.

### Authentication & Access Control
- username/password login
- role-based routing after login
- Admin/HR/Manager workspace access
- Employee self-service access
- JWT authentication
- refresh token direction
- logout / revoke direction
- protected APIs
- protected page flow

---

### Landing / Portal Separation
- dedicated landing / login concept
- separate Admin workspace and Employee workspace
- cleaner multi-page business design
- reduced overcrowding compared with single-page HR demos

---

### Admin / HR Overview Dashboard
- monthly/yearly filter
- KPI blocks
- governance-oriented cards
- approval and audit feed
- workforce health indicators
- recent operational summaries
- business-friendly overview layout

---

### Employee Self-Service Overview
- welcome panel
- personal KPI cards
- recent attendances
- recent payrolls
- recent leave requests
- self-service timeline
- workflow visibility
- personal summary blocks

---

### Employee Management
- employee listing
- employee search
- department filter
- status filter
- pagination
- view employee
- add employee
- edit employee
- delete employee
- role/account-linked employee visibility
- employee summary cards

---

### Department & Position Management
- department listing
- department search
- department headcount summary
- add / edit / delete department
- position listing
- position search
- add / edit / delete position
- employee count per position
- role structure administration

---

### Attendance Management
- attendance list
- filter by month/year/status/employee
- add attendance
- edit attendance
- delete attendance
- attendance KPI cards
- hours summary
- risk metrics
- attendance chart
- operational attendance review page

---

### Employee Attendance Self-Service
- personal attendance history
- filter by month/year/date/status
- export support area
- self-service attendance summary
- late / absent / remote visibility
- personal work-pattern review

---

### Attendance Adjustment Workflow
- employee correction request
- adjustment queue
- pending / approved / rejected states
- audit history
- recent adjustment feed
- workflow timeline visibility
- summary cards for pending vs reviewed requests

---

### Payroll Management
- payroll listing
- payroll filter by employee/month/year
- search and paging
- generate payroll for all employees
- generate payroll for a single employee
- payroll summary cards
- payroll charts / trend / composition
- payroll detail review direction
- bonus / deduction visibility
- print/export support area

---

### Payroll Period Governance
- payroll period lock UI concept
- governance explanation panel
- monthly lock awareness
- designed to protect reviewed payroll periods
- graceful fallback when period API is not fully configured

---

### Employee Payroll Self-Service
- personal payroll history
- filter by month/year
- payroll KPI summary
- salary trend visualization
- salary composition view
- payroll history table
- take-home salary visibility
- employee-friendly payroll reading experience

---

### Leave Workflow
- employee leave submission
- leave type selection
- date range selection
- reason entry
- leave list and filtering
- approve / reject review flow
- approval note / reject note
- pending / approved / rejected / cancelled states
- leave summary cards
- leave timeline / history support

---

### Leave Balance Policy
- leave balance-oriented service direction
- annual leave structure
- sick leave structure
- remaining and used fields
- pending-aware leave balance response model
- policy-friendly structure for real HR logic

---

### Overtime Workflow
- employee overtime request submission
- start / end time support
- request filtering
- pending / approved / rejected states
- admin/hr/manager review
- approved hours summary
- payroll impact note
- export direction
- approval queue visibility

---

### Shift & Schedule Management
- reusable shift catalog
- shift code / name / start / end / hours / notes
- night-shift support
- add / edit / delete shift
- schedule assignment by work date
- employee-to-shift assignment
- lock / open row behavior
- schedule notes
- department-aware schedule listing

---

### Reports & Business Summaries
- dedicated reports page
- monthly and yearly filters
- department-level comparison
- month-to-month trend chart
- attendance summary
- leave summary
- payroll summary
- operational highlight cards
- open approval queue visibility
- export support sections
- business-facing reporting design

---

### Audit / Operational Review
- leave audit feed
- payroll audit feed
- attendance adjustment feed
- workflow decision visibility
- governance-oriented operational storytelling
- employee self-service timeline

---

### AI Payroll Summary Assistant
- prompt template selector
- department focus selector
- helper / explanation block
- custom instruction input
- visible-context helper
- result panel
- cache-friendly repeated query design
- payroll anomaly and executive-style summary assistance

---

## AI Payroll Summary Assistant

The **AI Payroll Summary Assistant** is one of the most distinctive features of this project.

### Main use cases
- summarize monthly payroll anomalies
- identify unusual overtime impact
- explain payroll pressure in HR-friendly language
- generate executive-style payroll summaries
- review payroll in department context
- reuse prompt templates for consistency

### UI components
- Prompt template
- Department focus
- Custom instruction
- Generate AI Summary
- Use Visible Context
- Prompt idea cards
- Result / empty state area

### Example prompts
- “Summarize payroll anomalies for this month.”
- “Which employees had unusual overtime impact on payroll?”
- “Give HR a short executive brief for payroll and attendance pressure.”

---

## Business Rules

The project includes practical workflow-oriented rules beyond CRUD.

### Role-Based Portal Separation
- Employee should not use Admin pages
- Admin / HR / Manager should not use Employee portal as normal users

### Manager Scope
- Manager only works within assigned scope
- manager should not review out-of-scope workflows or reports

### Leave Workflow
- requests follow status transitions
- approval/rejection can include notes
- attendance conflict can block approval depending on configuration

### Attendance Adjustment
- employee requests correction instead of directly editing records
- pending vs reviewed workload is visible operationally

### Payroll Governance
- payroll is generated by selected month/year
- period lock concept protects reviewed payroll data
- generate / adjust / delete behavior can be governed by period state

### Overtime to Payroll Relationship
- approved overtime is designed to influence payroll context
- overtime review is connected to payroll understanding

### Leave Balance
- allocation / used / remaining structure is available
- pending-aware balance direction exists for future strengthening

---

## Tech Stack

### Backend
- ASP.NET Core Web API
- C#
- Entity Framework Core
- SQL Server
- JWT Authentication
- Refresh Token direction
- Redis Cache direction
- service layer
- DTO-based request/response structure

### Frontend
- HTML
- CSS
- JavaScript
- Bootstrap 5
- Bootstrap Icons
- Chart.js

### Dev / Ops
- Visual Studio / .NET 8 SDK
- Docker
- Docker Compose

---

## Architecture Diagram (Text)

```text
┌──────────────────────────────────────────────────────────────┐
│                        Smart HR Frontend                     │
│                                                              │
│   ┌──────────────────────┐    ┌──────────────────────────┐   │
│   │ Admin / HR / Manager │    │ Employee Self-Service    │   │
│   │ wwwroot/admin/*      │    │ wwwroot/employee/*       │   │
│   └──────────┬───────────┘    └────────────┬─────────────┘   │
└──────────────┼─────────────────────────────┼─────────────────┘
               │                             │
               └──────────────┬──────────────┘
                              │ HTTP / JWT
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                 ASP.NET Core Web API Backend                 │
│                                                              │
│  Controllers   Services   DTOs   Middleware   Validation    │
│                                                              │
│  - Auth                                             - Logs   │
│  - Dashboard                                        - Audit  │
│  - Employees                                        - Cache  │
│  - Attendances                                      - AI     │
│  - Payrolls                                                 │
│  - LeaveRequests                                            │
│  - Departments / Positions                                  │
│  - Reports / Overtime / Schedule direction                  │
└──────────────┬───────────────────────────────┬───────────────┘
               │                               │
               ▼                               ▼
      ┌───────────────────┐          ┌────────────────────┐
      │   SQL Server DB   │          │     Redis Cache    │
      │ Employees         │          │ Dashboard cache    │
      │ Attendances       │          │ Report cache       │
      │ LeaveRequests     │          │ Leave balance      │
      │ Payrolls          │          │ AI summary cache   │
      │ Positions         │          └────────────────────┘
      │ RefreshTokens     │
      │ Shift / Schedule  │
      │ Overtime          │
      └───────────────────┘
```

---

## Project Tree

```text
smart_hr_attendance&payroll_management/
├── Controllers/
│   ├── AuthController.cs
│   ├── DashboardController.cs
│   ├── EmployeesController.cs
│   ├── AttendancesController.cs
│   ├── LeaveRequestsController.cs
│   ├── PayrollsController.cs
│   ├── DepartmentsController.cs
│   ├── PositionsController.cs
│   └── ...
├── Data/
│   ├── AppDbContext.cs
│   ├── DbSeeder.cs
│   └── ...
├── DTOs/
│   ├── LoginRequest.cs
│   ├── PayrollResponse.cs
│   ├── LeaveRequestResponse.cs
│   ├── EmployeeProfileResponse.cs
│   └── ...
├── Entities/
│   ├── AppUser.cs
│   ├── Employee.cs
│   ├── Attendance.cs
│   ├── LeaveRequest.cs
│   ├── Payroll.cs
│   ├── Position.cs
│   ├── RefreshToken.cs
│   └── ...
├── Middleware/
│   └── ...
├── Models/
│   └── JwtSettings.cs
├── Services/
│   ├── PasswordService.cs
│   ├── TokenService.cs
│   ├── LeaveBalancePolicyService.cs
│   ├── PayrollComputationService.cs
│   └── ...
├── Validation/
│   └── ...
├── wwwroot/
│   ├── admin/
│   │   ├── overview.html
│   │   ├── employees.html
│   │   ├── attendances.html
│   │   ├── payrolls.html
│   │   ├── leaves.html
│   │   ├── departments.html
│   │   ├── reports.html
│   │   ├── schedules.html
│   │   ├── overtime.html
│   │   ├── admin.css
│   │   └── admin.js
│   ├── employee/
│   │   ├── overview.html
│   │   ├── attendances.html
│   │   ├── payrolls.html
│   │   ├── leaves.html
│   │   ├── overtime.html
│   │   ├── profile.html
│   │   ├── login.html
│   │   ├── employee.css
│   │   └── employee.js
│   ├── index.html
│   └── login.html
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
├── Dockerfile
└── docker-compose.yml
```

---

## Screenshots

### Landing Page
![Landing Page](docs/screenshots/Login.png)

### Admin Overview Dashboard
![Admin Overview](docs/screenshots/Admin/Overview/admin_overview_1.png)

### Payroll Page + AI Assistant
![Admin Payroll AI 1](docs/screenshots/Admin/Payrolls/admin_payroll_1.png)
![Admin Payroll AI 2](docs/screenshots/Admin/Payrolls/admin_payroll_2.png)

### Reports Dashboard
![Admin Reports 1](docs/screenshots/Admin/Reports/Admin_reports_1.png)
![Admin Reports 2](docs/screenshots/Admin/Reports/Admin_reports_2.png)
![Admin Reports 3](docs/screenshots/Admin/Reports/Admin_reports_3.png)

### Leave Approval Workflow
![Leave Request](docs/screenshots/Admin/Leave_Requests/leave_request.png)

### Shift & Schedule Management
![Schedule Management](docs/screenshots/Admin/Schedules/schedules.png)

### Employee Self-Service Overview
![Employee Overview 1](docs/screenshots/Employees/Overview/Emp_overview_1.png)
![Employee Overview 2](docs/screenshots/Employees/Overview/Emp_overview_2.png)
![Employee Overview 3](docs/screenshots/Employees/Overview/Emp_overview_3.png)

### Employee Payroll Page
![Employee Payroll](docs/screenshots/Employees/Payrolls/emp_payroll.png)

### HR Workspace
![HR Workspace 1](docs/screenshots/Hr/Overview/Hr_overview_1.png)
![HR Workspace 2](docs/screenshots/Hr/Overview/Hr_overview_2.png)
![HR Workspace 3](docs/screenshots/Hr/Overview/Hr_overview_3.png)

### Manager Approval
![Manager Approval 1](docs/screenshots/Manager/Overview/M_overview_1.png)
![Manager Approval 2](docs/screenshots/Manager/Overview/M_overview_2.png)

## Demo Accounts / Notes

> Do **not** commit real usernames/passwords to a public repository.  
> For public GitHub usage, describe roles only or use dummy demo credentials.

### Suggested demo roles
- Admin
- HR
- Manager
- Employee

### Demo note
Before demo:
- seed sufficient data
- prepare month/year with good charts
- prepare payroll data for AI summary
- prepare pending leave/overtime items for workflow demo

---

## How to Run Locally

### Prerequisites
Make sure you have:

- .NET 8 SDK
- SQL Server
- Visual Studio or compatible .NET IDE
- Redis (optional but recommended)
- Docker Desktop (optional)

### Run steps
1. Clone the repository
2. Open the solution in Visual Studio
3. Configure `appsettings.json` and `appsettings.Development.json`
4. Check SQL Server connection string
5. Apply migrations if required
6. Seed demo data
7. Run the backend API
8. Open the frontend pages under `wwwroot/admin` or `wwwroot/employee`

### Optional Redis local run
```bash
docker run -d --name smart-hr-redis -p 6379:6379 redis:7-alpine
```

---

## Optional Docker Run

If your current Docker configuration matches your project paths:

```bash
docker compose up -d
or to run only specific services:
docker run -d --name smart-hr-redis -p 6379:6379 redis:7-alpine
or if using docker compose with a compose file:
docker compose -f "your_path_project" up -d redis db
To stop and remove the container:
docker compose -f "your_path_project" stop redis db
```

Typical services:
- API
- SQL Server
- Redis

> Make sure your Dockerfile uses the correct `.csproj` and `.dll` names from your current project.

---

## API Coverage Summary

The current project covers these API areas:

### Auth APIs
- login
- refresh token
- logout
- my profile
- change password

### Dashboard APIs
- overview
- department headcount
- recent leaves
- recent payrolls
- recent attendances
- employee status summary

### Employee APIs
- list/search/filter
- create/update/delete
- my profile update

### Attendance APIs
- list/filter
- create/update/delete
- my attendances
- adjustment workflow support

### Payroll APIs
- list/filter
- generate all
- generate single
- my payrolls
- governance / period direction
- AI summary direction

### Leave APIs
- create request
- query/filter
- approve/reject
- my leave requests
- leave balance direction

### Department / Position APIs
- CRUD operations
- search/filter
- organizational structure support

### Overtime / Schedule / Reports APIs
- workflow-specific support depending on current build stage

---

## Engineering Depth

This project includes more than UI and CRUD.

### Authentication
- JWT auth
- refresh token direction
- logout revoke direction

### Cache
- Redis direction
- dashboard/report/AI summary/leave balance cache support

### Logging & Stability
- structured logging direction
- global exception handling direction
- runtime stabilization effort
- graceful no-data / fallback states

### Audit
- audit-aware operational feeds
- approval/rejection visibility
- self-service timeline direction

### Test Readiness
Strong candidates for unit/integration tests:
- payroll computation
- leave balance policy
- auth flow
- leave approval
- payroll generation
- AI summary endpoint

### Deployment Readiness
- Dockerfile direction
- docker-compose direction
- configuration separation
- portfolio-ready project structure

---

## Suggested Demo Flow

A good 5–7 minute demo can follow this order:

1. Login as **Admin**
2. Show **Admin Overview Dashboard**
3. Open **Payroll page**
4. Show payroll filters, generation actions, and governance block
5. Use **AI Payroll Summary Assistant**
6. Open **Reports page**
7. Login as **Manager**
8. Review a leave or overtime approval flow
9. Login as **Employee**
10. Show self-service overview, payroll, leave, and overtime

### Why this demo flow works
It demonstrates:
- role separation
- workflow depth
- business dashboards
- AI integration
- production-like structure

---

## Future Improvements

Potential next steps:
- finalize payroll period lock backend APIs
- improve integration test separation
- strengthen Docker run consistency
- add email / notification workflow
- add background jobs
- expand AI provider integration
- improve export formats
- improve mobile responsiveness further
- add organization charts and richer manager analytics

---

## Final Note

**Smart HR** is designed to be a **portfolio-ready, business-oriented HR management system**.

It demonstrates:
- multiple business modules
- multiple roles
- workflow-based operations
- business dashboard design
- AI-assisted payroll analysis
- engineering depth beyond a simple student CRUD project
