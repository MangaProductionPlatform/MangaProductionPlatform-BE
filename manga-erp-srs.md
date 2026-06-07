# System Requirement Specification (SRS) & Architecture: Manga Production & Publishing ERP
**Version 2.0 — Revised & Patched**
SU26SWP05 | ThinhDP2 Team

---

## 1. System Overview

This system is a specialized enterprise ERP solution designed for modern comic and manga production studios, optimized for Webtoon and digital-first sequential workflows. It manages the entire project lifecycle across five distinct technical domains:

| Domain | Code | Description |
|--------|------|-------------|
| Series Submission & Vetting | **MF1** | Platform bootstrap and editorial gatekeeping |
| Internal Studio Pipeline | **MF2** | Multi-layer delegation and crew management |
| Visual QA & Publishing | **MF3** | Coordinate-based quality assurance and edge deployment |
| Asset Optimization | **MF5** | Asynchronous queue asset optimization |
| Cache Invalidation | **MF8** | High-concurrency cache invalidation edge publishing |

---

## 2. Actors & Permissions

The system defines six distinct actor types with strict role-based access control (RBAC). Role elevation (e.g., `Reader → Mangaka`) is performed **atomically** by the System Handler upon series approval.

| Actor | Access Level | Core Rights | Restrictions |
|-------|-------------|-------------|--------------|
| **Reader / Creator_Draft** | Default / Open Registration | Read public chapters; draft & submit series proposal for vetting | Cannot access studio tools until role is elevated |
| **Mangaka** | Elevated after series approval | Manage Studio Workspace; create chapters; invite crew; review layers; trigger QA submission | Cannot approve own submissions; cannot QA own chapters |
| **Assistant** | Invited by Mangaka | View assigned page tasks; download base layers & resources; upload transparent `.png` artwork layers | Read-only on unassigned tasks; no access to submission or QA flows |
| **Tantou Editor** | Assigned by publishing house | Place canvas-anchored Bug Pins; approve or reject chapters; monitor studio production progress | Cannot modify submissions; no access to studio task assignment |
| **Editorial Board** | Platform admin-level | Evaluate new series (MF1); assign distribution cadences; schedule releases; import vote data; cancel series | Cannot create or edit chapter content |
| **System Handler** | Automated backend daemon | Image mutations; layer compositing; token validation; event routing; cache eviction; archival storage | Non-human actor; no direct user interaction |

> **Note:** User registration defaults to the `Reader` role. `Mangaka` elevation is gated exclusively through the MF1 series approval pipeline.

### 2.1 Account Provisioning Workflow (Without HR)
*Since the system does not have an HR module, account creation and provisioning are tied to operational actions and task-based trust relationships:*

*   **Step 1: Core Initialization (Admin & Editorial Board)**
    *   The system starts with a single seed **Admin** account.
    *   When the Editorial Board is established, the Admin directly registers accounts for **Editorial Board** members. This is based on direct administrative trust.
*   **Step 2: Operational Onboarding (Tantou Editor)**
    *   When a new Editor joins the team, the **Editorial Board** sends an internal request (or uses the system UI) to the Admin.
    *   The **Admin** creates the **Tantou Editor** account and sends the credentials.
    *   The **Editorial Board** then assigns this Editor to manage specific manga series or chapters.
*   **Step 3: Creative Role Elevation & Crew Management (Mangaka & Assistant)**
    *   **Mangaka Provisioning (Two-Stage Vetting)**: Creators self-register as **Readers**, then apply by submitting a Series Proposal. Upon passing the two-stage vetting process (Tantou Editor recommend -> Editorial Board approve), the system automatically elevates the user's role to **Mangaka** and creates the **MangaSeries** workspace.
    *   **Assistant Provisioning**: To hire helpers, a **Mangaka** goes to their Studio workspace, enters the helper's email, and clicks "Invite Assistant". The system generates a secure invitation token. Upon clicking, the helper is registered with the **Assistant** role and automatically joined to the manga chapter's team with restricted permissions.

---

## 3. Core Business Workflows

### MF1 — Series Submission & Vetting Workflow (Two-Stage Vetting)
*Purpose: Project gatekeeping and role elevation from Reader to Mangaka via Editor recommend and Board approval.*

- **Step 1** `[Reader]` **Create Series Submission** → Creator registers as a Reader, starts a new draft proposal with metadata (Title, Description, Genre, Cover Art) and uploads primary manuscript preview attachments. `Status = Draft`.
- **Step 2** `[Reader]` **Submit Proposal** → Commits the draft to the platform. System updates status to `Pending` and locks user modifications.
- **Step 3** `[Tantou Editor]` **Stage 1 Vetting (Editor Review)** → Tantou Editor (assigned by system or Admin based on genre) evaluates the proposal (plot, art style). Verdict options:
  - **REJECT** → `Status = Rejected`, logs feedback, notify creator.
  - **REQUEST REVISION** → `Status = RevisionRequired`, feedback logs appended, revert edit permissions — loop to Step 1.
  - **RECOMMEND TO BOARD** → `Status = RecommendedToBoard`, logs recommendation comments, forwards proposal to Editorial Board for final decision.
- **Step 4** `[Editorial Board]` **Stage 2 Vetting (Board Final Review)** → Editorial Board performs a deeper audit of the recommended proposal. Verdict options:
  - **REJECT** → `Status = Rejected`, notify creator, terminate thread.
  - **REQUEST REVISION** → `Status = RevisionRequired`, feedback logs appended — loop to Step 1.
  - **APPROVE** → proceed to Step 5.
- **Step 5** `[System Handler]` **Atomic Process Approval & Role Elevation** → Executes an atomic DB transaction: mutates `SubmissionStatus` to `Approved`, instantiates the `MangaSeries` record (`Status = Active`), and **elevates `User.Role` from `Reader` to `Mangaka`**. Opens full access to Studio Workspace and Chapter creation (MF2).

---

### MF2 — Manga Production Workflow
*Purpose: Internal studio pipeline managing chapter creation, crew invitation, layer task assignment, and review.*

- **Step 1** `[Mangaka]` **Create Chapter & Invite/Setup Team** → Generates a new chapter under an approved Active series. Defines `TotalPages`.
  - **Scenario A (Existing Assistant):** System binds user directly to `ChapterTeams`.
  - **Scenario B (New Invitation):** System logs a secure time-bound token in `AssistantInvitations` and dispatches an email link that force-creates an `Assistant` account upon click.
- **Step 2** `[Mangaka]` **Activate Page Task** → Provisions a specific page marker to `Incomplete` state, setting `AssignedAssistantId`, signaling the designated assistant via WebSocket.
- **Step 3** `[System Handler]` **Route Task Notification** → Pushes real-time SignalR WebSocket alerts to the targeted assistant. Falls back to SMTP email if offline.
- **Step 4** `[Assistant]` **Upload Artwork Layer** → Submits a transparent working layer (`.png`). Task auto-advances to `Reviewing`. MF5 processes asset optimization asynchronously.
- **Step 5** `[Mangaka]` **Review Submitted Layer** → Audits the delivery layer inside the studio overlay canvas panel.
- **Step 6** `[Mangaka]` **Is Layer Accepted?** [Decision]
  - **YES** → proceed to Step 8.
  - **NO** → proceed to Step 7.
- **Step 7** `[System Handler]` **Process Revision Alert** → Logs rejection note to `ArtworkLayers.RejectionNote`, flips `TaskStatus` to `RevisionAlert`, alerts assistant. Loop back to Step 4.
- **Step 8** `[System Handler]` **Generate Preview Page** → Locks the asset row, runs MF5 background optimization, stacks active layers into a flat preview `.webp` image. Stores in `PreviewPages`.
- **Step 9** `[System Handler]` **Are All Chapter Pages Completed?** [Decision]
  - Evaluates: `TotalApprovedPages == TotalConfiguredPages`.
  - **NO** → auto-increment to next page, unlock it (loop to Step 2).
  - **YES** → proceed to Step 10.
- **Step 10** `[Mangaka]` **Submit Chapter for Editorial QA** → Locks local write vectors, mutates `ChapterStatus` to `ReadyForQA`, triggers MF3 editorial gate.

---

### MF3 — QA & Publishing Flow
*Purpose: Editorial vetting, bug resolution, and automated edge deployment.*

- **Step 1** `[Tantou Editor]` **Read & Check Chapter** → Loads flat composite `PreviewPages` via the secure editorial viewer panel.
- **Step 2** `[Tantou Editor]` **Detect Visual / Content Issues?** [Decision]
  - **NO** → proceed to Step 5.
  - **YES** → proceed to Step 3.
- **Step 3** `[Tantou Editor]` **Pin Bug Locations & Send Feedback Batch** → Anchors `BugPins` with percent-based coordinates (X, Y: 0.00–100.00), `IssueType` classification (`Visual`/`Content`/`Text`/`Layout`), and description notes. Compresses all pins into a single `BatchToken` delivery, forcing `ChapterStatus` to `Rejected`.
- **Step 4** `[Mangaka]` **Fix Bugs at Pins & Resubmit** → Inspects pin coordinates, assigns fix tasks to relevant assistants, updates assets, and invokes Resubmit Chapter. Loop back to Step 1.
- **Step 5** `[Tantou Editor]` **Approve Chapter** → Flushes unresolved pins to `Resolved` (sets `ResolvedAt`), marks entity as `Approved`.
- **Step 6** `[Editorial Board]` **Select Issue Type & Schedule** → Configures distribution tier (`Weekly`/`Monthly`/`Special`) and defines `ScheduledPublishAt` timestamp. Stored on `Chapters`.
- **Step 7** `[System Handler]` **Automated Publish & Cache Invalidation** → Background worker monitors schedule triggers. Swaps `ChapterStatus` to `Published`, maps optimized files to `ProductionFileUrl`, creates `PublicationRecords` entry, flushes Redis cache (MF8). Sets `Chapter.PublishedAt`.
- **Step 8** `[System Handler]` **Archive Production Layers** → Asynchronously routes raw high-overhead studio layers to low-cost archival cold storage. Sets `Chapter.Status` to `Archived`.

---

## 4. CRUD Feature Matrix

### 4.1 User & Authentication

| Entity / Feature | CRUD | Actor | Priority | Notes |
|-----------------|------|-------|----------|-------|
| Register account | C | All actors | **Must Have** | Role selected at registration; Mangaka role only via MF1 elevation |
| Login / Logout | R | All actors | **Must Have** | JWT issued; refresh token stored in `RefreshTokens` table |
| View / Edit profile | R, U | All actors | Should Have | `FullName`, `AvatarUrl`, bio |
| Refresh access token | R | System | **Must Have** | Validate `RefreshTokens.IsRevoked` before issuing new JWT |
| Revoke token on logout | U | All actors | **Must Have** | Set `RefreshTokens.IsRevoked = 1` on logout or role change |

### 4.2 Series Submission (MF1)

| Entity / Feature | CRUD | Actor | Priority | Notes |
|-----------------|------|-------|----------|-------|
| Create submission | C | Mangaka / Creator_Draft | **Must Have** | `Status = Draft` on create |
| Upload manuscript file | C, U | Mangaka | **Must Have** | PDF/image stored on S3/R2 |
| List own submissions | R | Mangaka | **Must Have** | Filter by status |
| View submission detail | R | Mangaka, Board | **Must Have** | Manuscript, author info, status, feedback |
| Submit proposal | U | Mangaka | **Must Have** | DRAFT → PENDING; triggers lock & route |
| Re-upload after revision | U | Mangaka | **Must Have** | New file upload; status REVISION_REQUIRED → PENDING |
| List submission queue | R | Editorial Board | **Must Have** | Only PENDING / UNDER_REVIEW visible |
| Approve submission | U | Editorial Board | **Must Have** | Status → APPROVED; records `ReviewedByUserId` + `ReviewedAt`; triggers Series creation + role elevation |
| Request revision | U | Editorial Board | **Must Have** | Status → REVISION_REQUIRED; `FeedbackMessage` required |
| Reject submission | U | Editorial Board | **Must Have** | Status → REJECTED; `FeedbackMessage` required |

### 4.3 Series Management

| Entity / Feature | CRUD | Actor | Priority | Notes |
|-----------------|------|-------|----------|-------|
| Create series (auto) | C | System Handler | **Must Have** | Triggered on `SubmissionApproved` event; `Status = Active` |
| List own series | R | Mangaka | **Must Have** | Filter Active / Cancelled |
| View series detail | R | Mangaka, Editor, Board | **Must Have** | Info, chapters, ranking stats |
| Update publish schedule | U | Editorial Board | Should Have | Weekly / Monthly / Special |
| Cancel series | U | Editorial Board | Should Have | Status → CANCELLED; notify Mangaka |
| View ranking board | R | All actors | Should Have | Sorted by VoteCount per period |

### 4.4 Chapter & Page (MF2)

| Entity / Feature | CRUD | Actor | Priority | Notes |
|-----------------|------|-------|----------|-------|
| Create chapter | C | Mangaka | **Must Have** | Only when `Series.Status = Active`; sets `TotalPages` |
| List chapters | R | Mangaka, Editor | **Must Have** | Filter by status; progress % |
| Upload base page layer | C | Mangaka | **Must Have** | Creates `PageTask` record; `Status = Pending` |
| List pages in chapter | R | Mangaka, Editor | **Must Have** | Include merge status per page |
| View merged page preview | R | Mangaka, Editor | **Must Have** | Load `PreviewPages.CompositeFileUrl` |
| Submit chapter for QA | U | Mangaka | **Must Have** | Only when all pages Approved; `Status → ReadyForQA` |
| Resubmit after QA fix | U | Mangaka | **Must Have** | `Status → ReadyForQA` again |
| View chapter progress | R | Mangaka, Editor | Should Have | % pages merged vs total |
| Delete unassigned page | D | Mangaka | Nice to Have | Only if `PageTask` has no `ArtworkLayer` |

### 4.5 Task Management (MF2)

| Entity / Feature | CRUD | Actor | Priority | Notes |
|-----------------|------|-------|----------|-------|
| Assign task to assistant | C | Mangaka | **Must Have** | Sets `AssignedAssistantId`, `LayerType`, activates `PageTask → Incomplete` |
| Invite new assistant | C | Mangaka | **Must Have** | Creates `AssistantInvitations` token; sends email |
| Accept invitation | U | New user (link) | **Must Have** | Creates Assistant account; binds `ChapterTeams` |
| View assigned tasks | R | Assistant | **Must Have** | Filter by status, chapter; download base layer |
| View created tasks | R | Mangaka | **Must Have** | Tracking all tasks per chapter |
| Submit artwork layer | U | Assistant | **Must Have** | Upload `.png`; `TaskStatus → Reviewing`; MF5 optimizes async |
| Accept layer | U | Mangaka | **Must Have** | `TaskStatus → Approved`; triggers merge check for page |
| Reject layer | U | Mangaka | **Must Have** | `TaskStatus → RevisionAlert`; `RejectionNote` saved; notify assistant |
| View assistant earnings | R | Assistant | Should Have | ACCEPTED tasks × rate per month |
| Cancel unstarted task | D | Mangaka | Nice to Have | Only if `TaskStatus = Pending` |

### 4.6 QA Session & Bug Pins (MF3)

| Entity / Feature | CRUD | Actor | Priority | Notes |
|-----------------|------|-------|----------|-------|
| Start QA session | C | System / Editor | **Must Have** | Auto-created when `Chapter.Status = ReadyForQA` |
| Read chapter for QA | R | Tantou Editor | **Must Have** | Page-by-page composite viewer |
| Create bug pin | C | Tantou Editor | **Must Have** | Coordinate (X,Y) 0–100%, `IssueType`, `NoteMessage` |
| View / Edit own bug pin | R, U | Tantou Editor | **Must Have** | Edit before batch is sent |
| Delete bug pin | D | Tantou Editor | **Must Have** | Only before batch is sent |
| Send feedback batch | U | Tantou Editor | **Must Have** | Groups pins under `BatchToken`; `Chapter.Status → Rejected`; notify Mangaka |
| View bug pins (Mangaka) | R | Mangaka | **Must Have** | See coordinates + notes on each page; assign fix tasks |
| Approve chapter | U | Tantou Editor | **Must Have** | All pins → `Resolved`; `Chapter.Status → Approved`; triggers Step 6 |

### 4.7 Publishing & Ranking

| Entity / Feature | CRUD | Actor | Priority | Notes |
|-----------------|------|-------|----------|-------|
| Select issue type & schedule | U | Editorial Board | **Must Have** | Sets `IssueType` + `ScheduledPublishAt` on `Chapter` |
| Auto-publish chapter | C | System Handler | **Must Have** | Creates `PublicationRecords`; sets `Chapter.PublishedAt`; Redis flush |
| View publication history | R | Mangaka, Editor, Board | Should Have | Published date, issue type, public URL |
| Import reader vote data | C | Editorial Board | Should Have | CSV or manual entry per period; 1 row per series per period |
| View ranking board | R | All actors | Should Have | Aggregated after each import; sort by votes |
| Cancel / change schedule | U | Editorial Board | Should Have | Update `ScheduledPublishAt` or cancel publication |

---

## 5. Database Architecture (SQL Server — v2 Revised)

All primary keys use `UNIQUEIDENTIFIER` (GUID) for compatibility with C# Clean Architecture / CQRS. `SystemAuditLogs` uses `BIGINT IDENTITY` for high-insert performance.

### 5.1 Schema Changes vs v1 — 11 Fixes Applied

| Table | Fix # | Column(s) Added | Reason | Severity |
|-------|-------|----------------|--------|----------|
| Users | FIX-1 | `FullName NVARCHAR(200)`, `AvatarUrl NVARCHAR(2048)` | Required for profile display | Minor |
| SeriesSubmissions | FIX-2 | `ReviewedByUserId FK → Users`, `ReviewedAt DATETIME2` | Audit trail: which Board member approved/rejected and when | Important |
| MangaSeries | FIX-3 | `Status NVARCHAR(50) DEFAULT Active + CHECK`, `CoverImageUrl`, `Genre` | **CRITICAL**: Board cannot cancel series without Status column | Critical |
| Chapters | FIX-4 | `AssignedEditorId FK → Users`, `PublishedAt DATETIME2` | Tantou Editor assignment per chapter; actual publish timestamp | Important |
| AssistantInvitations | FIX-5 | `AssignedRole NVARCHAR(100) + CHECK` | Role must be set at invite time to auto-populate ChapterTeams on accept | Important |
| ChapterTeams | FIX-6 | `CreatedAt DATETIME2`, `InvitedByUserId FK → Users` | Audit trail for team membership | Minor |
| PageTasks | FIX-7 | `AssignedAssistantId FK → Users (NOT NULL capable)`, `CreatedAt` | **CRITICAL**: Without this, Assistant task list has no data; entire MF2 broken | Critical |
| ArtworkLayers | FIX-8 | `RejectionNote NVARCHAR(MAX)`, `SubmittedAt DATETIME2`, `ReviewedAt DATETIME2` | Assistant needs rejection note; timestamps for deadline tracking | Important |
| PreviewPages | FIX-9 | `IsPublished BIT DEFAULT 0` | Differentiate internal review file from publicly deployed CDN file | Minor |
| BugPins | FIX-10 | `ChapterId FK → Chapters`, `IssueType NVARCHAR(50) + CHECK`, `ResolvedAt DATETIME2` | **CRITICAL**: Without ChapterId, loading pins requires double JOIN | Critical |
| Notifications | FIX-11 | `RelatedEntityType NVARCHAR(50) + CHECK` | UI cannot build correct deep link without knowing entity type | Important |
| SystemAuditLogs | Minor | `EntityType NVARCHAR(50)`, `EntityId UNIQUEIDENTIFIER` | Enables querying audit log by specific object | Minor |

### 5.2 New Tables Added

| Table | Columns | Purpose | Priority |
|-------|---------|---------|---------|
| **RefreshTokens** | `Id`, `UserId`, `Token`, `ExpiresAt`, `IsRevoked`, `RevokedAt` | JWT lifecycle: invalidate tokens on logout and on role elevation (`Reader → Mangaka`) | Important |
| **QASessions** | `Id`, `ChapterId`, `EditorId`, `Status`, `IsApproved`, `ApprovedAt`, `CompletedAt` | Tracks QA review sessions per chapter; auto-created when `Chapter.Status = ReadyForQA` | **Must Have** |
| **VoteData** | `Id`, `SeriesId`, `VotePeriod`, `VoteCount`, `ImportedBy`, `ImportedAt` | Stores raw reader vote counts imported by Editorial Board after each release period | Should Have |
| **RankingSnapshots** | `Id`, `SeriesId`, `VotePeriod`, `Rank`, `TotalVotes` | Aggregated ranking per period; used for the ranking board displayed to all actors | Should Have |
| **PublicationRecords** | `Id`, `ChapterId`, `SeriesId`, `IssueType`, `PublicationUrl`, `CacheKey`, `PublishedAt` | Immutable log of each publish event; captures actual CDN URL and Redis cache key for MF8 | Should Have |

### 5.3 Core Tables Summary

- **Users** — Identity and authentication. Roles: `Mangaka`, `Assistant`, `TantouEditor`, `EditorialBoard`, `Reader`. Soft-delete enabled.
- **RefreshTokens** — JWT lifecycle management. Track `IsRevoked` for logout and role-change invalidation.
- **SeriesSubmissions** — Tracks proposals. Statuses: `Pending`, `UnderReview`, `RecommendedToBoard`, `RevisionRequired`, `Approved`, `Rejected`. Soft-delete enabled.
- **MangaSeries** — Approved series master data. Statuses: `Active`, `Hiatus`, `Cancelled`. Soft-delete enabled.
- **Chapters** — Chapter state with statuses: `Draft`, `ReadyForQA`, `Rejected`, `Approved`, `Published`, `Archived`. Issue types: `Weekly`, `Monthly`, `Special`. Soft-delete enabled.
- **AssistantInvitations** — Tokenized invitation lifecycle with role assignment.
- **ChapterTeams** — Crew membership per chapter with roles: `LineArt`, `Background`, `Coloring`, `VFX`.
- **PageTasks** — Per-page workflow states: `Pending`, `Incomplete`, `Reviewing`, `Approved`.
- **ArtworkLayers** — Asset history ledger with versioning (`Version`, `IsCurrentVersion`). Layer types: `LineArt`, `Background`, `Coloring`, `Text`.
- **PreviewPages** — Flat composite viewport for internal review (`CompositeFileUrl`) and CDN deployment (`ProductionFileUrl`).
- **BugPins** — QA markers with coordinate constraints (0.00–100.00%), statuses: `Open`, `InFixing`, `Resolved`. Grouped by `BatchToken`.
- **QASessions** — QA review session per chapter. Statuses: `InProgress`, `Completed`. Tracks `IsApproved` and `ApprovedAt`.
- **PublicationRecords** — Immutable publish event log with CDN URL, `SeriesId` (denormalized), and Redis cache key.
- **VoteData** — Raw reader vote data per series per period.
- **RankingSnapshots** — Aggregated ranking board snapshots.
- **Notifications** — Push metadata with event types and deep-link entity references.
- **SystemAuditLogs** — High-performance audit engine (`BIGINT IDENTITY`).

### 5.4 Soft Delete Configuration (EF Core)

Entities with soft delete must have global query filters registered in `DbContext`:

```csharp
builder.Entity<Chapter>().HasQueryFilter(x => !x.IsDeleted);
builder.Entity<MangaSeries>().HasQueryFilter(x => !x.IsDeleted);
builder.Entity<SeriesSubmission>().HasQueryFilter(x => !x.IsDeleted);
builder.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
```

---

## 6. Supporting Sub-flows

### MF5 — Asset Upload & Optimization Flow
Processes incoming multi-layered graphical byte streams **asynchronously** via Hangfire message broker. Utilizes `SixLabors.ImageSharp` for lossy-to-lossless format conversions into compressed `.webp` structures before hosting on decoupled S3/R2 object storage. Stores both `FileUrlOriginal` and `FileUrlOptimized` in `ArtworkLayers`.

### MF6 — Real-time Notification Flow
Hosts active server-to-client pipelines using `Microsoft.AspNetCore.SignalR` WebSockets to project instant workflow triggers to operational user viewports. Fallback routines handle transient offline conditions via asynchronous SMTP email queues. Notification type stored in `Notifications.NotifyType` with `RelatedEntityType` for correct UI deep linking.

### MF7 — Version Control & History Log Flow (Audit Trail)
Enforces an immutable historical baseline in `ArtworkLayers`; file overrides generate an incremental trace counter (`Version = Version + 1`) while archiving past references under an active binary flag (`IsCurrentVersion = 0`). Transactional mutations emit records into `SystemAuditLogs` with `EntityType` and `EntityId` for targeted log queries.

### MF8 — Web Cache Invalidation Flow
Intercepts volatile high-concurrency read scenarios by mapping customer-facing endpoints into a distributed Redis network. Automated release events in `PublicationRecords` capture the Redis `CacheKey`. Publish workers execute key eviction tags to maintain consistency with zero execution delays.

---

## 7. Architecture & Implementation Directives

| Concern | Technology / Pattern |
|---------|---------------------|
| Architecture | Strict Clean Architecture, Microservices (per-service database) |
| Language / Runtime | C# ASP.NET Core Web API on **.NET 9** |
| CQRS | **MediatR** with command/query segregation |
| API Gateway | **YARP** reverse proxy (single client entry point) |
| Messaging | **MassTransit + RabbitMQ** for integration events |
| Database | **SQL Server** with `UNIQUEIDENTIFIER` PKs; `BIGINT IDENTITY` for audit logs |
| ORM | **Entity Framework Core 9** with per-service `DbContext` |
| Real-time | `Microsoft.AspNetCore.SignalR` WebSocket push notifications |
| Asset pipeline | **Hangfire** + `SixLabors.ImageSharp` for async `.png → .webp` conversion |
| Object storage | **S3-compatible** (AWS S3 / Cloudflare R2) |
| Cache layer | **Redis** for distributed read cache + invalidation on publish |
| Auth | **JWT Bearer** + Refresh Token rotation (`RefreshTokens` table) |
| Validation | **FluentValidation** via MediatR pipeline behavior |

### Global Soft Delete Rule
All entities exposing soft-delete metadata (`IsDeleted`, `DeletedAt`) **MUST** contain data provider global query filters within the `DbContext` pipeline configuration (see Section 5.4).

---

## 8. Solution Structure (Microservices)

```
MangaERP/
├── src/
│   ├── BuildingBlocks/
│   │   ├── MangaERP.BuildingBlocks.Domain/        ← Base entities, aggregate root, value objects
│   │   ├── MangaERP.BuildingBlocks.Application/   ← CQRS interfaces, behaviors, Result<T>
│   │   ├── MangaERP.BuildingBlocks.Infrastructure/ ← EventBus, BaseDbContext, S3 service
│   │   └── MangaERP.BuildingBlocks.Contracts/     ← Integration events, shared DTOs
│   ├── ApiGateway/
│   │   └── MangaERP.ApiGateway/                   ← YARP reverse proxy, JWT middleware
│   ├── Services/
│   │   ├── Identity/MangaERP.Identity/            ← Auth, JWT, refresh tokens
│   │   ├── Submission/MangaERP.Submission/        ← MF1: series submission vetting
│   │   ├── Series/MangaERP.Series/                ← Series management post-approval
│   │   ├── Chapter/MangaERP.Chapter/              ← MF2: chapter + pages
│   │   ├── Task/MangaERP.Task/                    ← MF2: layer assignment
│   │   ├── QA/MangaERP.QA/                        ← MF3: bug pins + approve
│   │   ├── Publishing/MangaERP.Publishing/        ← MF3: schedule + auto-publish
│   │   └── Ranking/MangaERP.Ranking/              ← Vote data + ranking board
│   ├── Infrastructure/
│   │   ├── Notification/MangaERP.Notification/    ← SignalR + SMTP fallback
│   │   ├── Asset/MangaERP.Asset/                  ← ImageSharp png→webp optimizer
│   │   └── BackgroundJobs/MangaERP.BackgroundJobs/ ← Hangfire scheduled jobs
│   └── Hubs/
│       └── MangaERP.SignalR/                       ← Standalone SignalR hub process
├── tests/
│   ├── UnitTests/
│   ├── IntegrationTests/
│   └── ArchTests/
├── docker/
│   ├── docker-compose.yml
│   └── docker-compose.override.yml
└── docs/
    ├── adr/
    └── api/openapi/
```