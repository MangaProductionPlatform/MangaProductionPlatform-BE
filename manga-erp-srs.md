# System Requirement Specification (SRS) & Architecture: Manga Production & Publishing ERP
**Version 3.0 — B2B ERP Architecture Update**
SU26SWP05 | ThinhDP2 Team

> **Changelog v3.0 (2026-06-11):** Redesigned from Microservices → **Modular Monolith**. Removed `Reader` role — system is now a **closed B2B ERP** with Admin-provisioned accounts only. Updated MF1: Mangaka (pre-provisioned) creates proposals; no role elevation step. Added implementation status tracking per section.

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

The system is a **closed B2B ERP** — there is no public self-registration. All accounts are provisioned by the Admin via the provisioning API. Role-based access control (RBAC) is enforced via JWT claims.

| # | Actor | Role Code | Enum Value | Core Rights | Restrictions |
|---|-------|-----------|------------|-------------|--------------|
| — | **Admin** | `adm` | `0` | Full system access; provision all accounts; seed by system at startup | Cannot be provisioned via API; only 1 exists (seeded) |
| 1 | **Editorial Board** | `eb` | `1` | Evaluate series (MF1); assign schedules; cancel series; import votes | Cannot create/edit chapter content |
| 2 | **Tantou Editor** | `tt` | `2` | Place Bug Pins; approve/reject chapters; monitor studio progress | Cannot modify submissions; no studio task assignment |
| 3 | **Mangaka** | `mgk` | `3` | Create series proposals (MF1); manage Studio Workspace; create chapters; review layers; trigger QA | Cannot approve own submissions; cannot QA own chapters |
| 4 | **Assistant** | `ast` | `4` | View assigned page tasks; upload `.png` artwork layers | Read-only on unassigned tasks; no submission/QA access |
| — | **System Handler** | — | — | Image mutations; token validation; event routing; cache eviction | Non-human actor |

> **Note (v3.0):** `Reader` role has been **removed**. This system is an internal ERP — all users are provisioned by Admin. `Mangaka` accounts are created directly by Admin via `POST /api/v1/admin/accounts/provision`. There is no public registration endpoint.

### 2.1 Account Provisioning Workflow
*This system uses a centralized Admin-Led provisioning model. There is no self-registration. All accounts are created by the Admin via the provisioning API.*

*   **Step 1: System Bootstrap**
    *   At first startup, the `DbSeeder` automatically creates the single **Admin** account (`sysadmin.adm@company.com`) using the password set in the `ADMIN_PASSWORD` environment variable.
*   **Step 2: Admin provisions all staff accounts**
    *   Admin inputs: `fullName`, `personalEmail`, `role` (1=EditorialBoard, 2=TantouEditor, 3=Mangaka, 4=Assistant)
    *   System auto-generates a corporate username: `[firstName][lastInitials].[roleCode]@company.com` (e.g. `anhnv.mgk@company.com`)
    *   System dispatches a **secure JWT invitation email** (24h expiry) to `personalEmail`
    *   Account status = `PendingActivation`
*   **Step 3: Staff activates their account**
    *   Staff clicks the link in the email → navigates to frontend `/activate?token=...`
    *   Frontend calls `POST /api/v1/auth/activate` with the token and chosen password
    *   Account status → `Active`; user can now log in with corporate username

### 2.2 Implemented API Endpoints — Account Provisioning

> ✅ **Status: IMPLEMENTED & TESTED** (as of 2026-06-11)

#### 2.2.1 Admin-Led Provisioning Flow
| Step | Actor | API | Request Body | Response |
|------|-------|-----|-------------|----------|
| 1 | Admin | `POST /api/v1/auth/login` | `{email, password}` | `{accessToken, refreshToken}` |
| 2 | Admin | `POST /api/v1/admin/accounts/provision` *(Bearer token)* | `{fullName, personalEmail, role}` | `{userId, username, personalEmail, role}` |
| 3 | Staff | `POST /api/v1/auth/activate` | `{token, newPassword}` | `{message: "Account activated"}` |

#### 2.2.2 Admin Account Management
| Step | Actor | API | Notes |
|------|-------|-----|-------|
| View all accounts | Admin | `GET /api/v1/admin/accounts` | Filter by `?role=&status=` |
| View one account | Admin | `GET /api/v1/admin/accounts/{id}` | |

#### 2.2.3 Username Generation Rule
```
Input:  "Nguyễn Văn Anh"
Output: anhnv.[roleCode]@company.com

Role codes: eb (Editorial Board) | tt (Tantou Editor) | mgk (Mangaka) | ast (Assistant)
Collision:  anhnv.mgk → anhnv.mgk1 → anhnv.mgk2 ...
```

#### 2.2.4 Assistant Provisioning (via Mangaka — MF2 scope, not yet implemented)
*   **Option 1 (Decentralized):** Mangaka calls `POST /api/v1/studio/assistants` from Studio team page → creates account + binds `ChapterTeams`
*   **Option 2 (Centralized):** Mangaka submits request `POST /api/v1/admin/assistant-requests` → Admin approves → account created

---

## 3. Core Business Workflows

### MF1 — Series Submission & Vetting Workflow (Two-Stage Vetting)
*Purpose: Project gatekeeping. Mangaka (pre-provisioned by Admin) submits a series proposal through a two-stage editorial review. On approval, a MangaSeries record is created and linked to the submitting Mangaka.*

> ⚠️ **v3.0 Change:** `Reader` role removed. Submitter is always a **Mangaka** (already provisioned). There is **no role elevation** step — Mangaka role is assigned at account creation by Admin.

> 🔲 **Status: PLANNED — not yet implemented**

- **Step 1** `[Mangaka]` **Create Draft Proposal** → Mangaka starts a new series draft with metadata: `Title`, `Description`, `Genre`, `CoverImageUrl`. `Status = Draft`. Manuscript URL provided when submitting.
- **Step 2** `[Mangaka]` **Submit Proposal** → Commits the draft. System updates `Status = Pending` and locks Mangaka from further edits.
- **Step 3** `[Tantou Editor]` **Stage 1 Vetting (Editor Review)** → Editor evaluates the proposal. Verdict options:
  - **REJECT** → `Status = Rejected`, logs feedback, notify Mangaka.
  - **REQUEST REVISION** → `Status = RevisionRequired`, feedback appended, edit lock lifted — loop to Step 1.
  - **RECOMMEND TO BOARD** → `Status = RecommendedToBoard`, recommendation logged.
- **Step 4** `[Editorial Board]` **Stage 2 Vetting (Board Final Review)** → Board audits the recommended proposal. Verdict options:
  - **REJECT** → `Status = Rejected`, notify Mangaka.
  - **REQUEST REVISION** → `Status = RevisionRequired` — loop to Step 1.
  - **APPROVE** → proceed to Step 5.
- **Step 5** `[System Handler]` **Atomic Approval** → Single DB transaction: `SubmissionStatus = Approved`, creates `MangaSeries` record (`Status = Active`) linked to the Mangaka. Opens Studio Workspace and Chapter creation (MF2). *(No role elevation — Mangaka was already Mangaka.)*

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

> ✅ **Status: IMPLEMENTED** (Identity module complete)

| Entity / Feature | CRUD | Actor | Priority | Status | Notes |
|-----------------|------|-------|----------|--------|-------|
| Admin provision account | C | Admin | **Must Have** | ✅ Done | `POST /api/v1/admin/accounts/provision`; auto-generates username; sends email |
| Activate account (set password) | U | Staff (email link) | **Must Have** | ✅ Done | `POST /api/v1/auth/activate`; JWT token from email |
| Login | R | All actors | **Must Have** | ✅ Done | `POST /api/v1/auth/login`; returns JWT + refresh token |
| List / View accounts | R | Admin | **Must Have** | ✅ Done | `GET /api/v1/admin/accounts` with role/status filters |
| Refresh access token | R | System | **Must Have** | 🔲 Planned | Validate `RefreshTokens.IsRevoked` before issuing new JWT |
| Revoke token on logout | U | All actors | **Must Have** | 🔲 Planned | `POST /api/v1/auth/logout`; set `IsRevoked = true` |
| View / Edit profile | R, U | All actors | Should Have | 🔲 Planned | `FullName`, `AvatarUrl` |

### 4.2 Series Submission (MF1)

> 🔲 **Status: PLANNED** — Domain entity `SeriesSubmission.cs` exists; Application/API layer not yet implemented.

| Entity / Feature | CRUD | Actor | Priority | Notes |
|-----------------|------|-------|----------|-------|
| Create draft submission | C | **Mangaka** | **Must Have** | `Status = Draft` on create |
| Upload manuscript URL | U | Mangaka | **Must Have** | Client uploads to S3/Cloudinary; passes URL to backend |
| Submit proposal | U | Mangaka | **Must Have** | `Draft → Pending`; triggers lock & route to editor |
| List own submissions | R | Mangaka | **Must Have** | Filter by status |
| View submission detail | R | Mangaka, TantouEditor, EditorialBoard | **Must Have** | Manuscript URL, status, feedback |
| Re-upload after revision | U | Mangaka | **Must Have** | New file URL; `RevisionRequired → Pending` |
| List submission queue | R | TantouEditor, EditorialBoard | **Must Have** | Editor: Pending/UnderReview; Board: RecommendedToBoard |
| Recommend to Board | U | TantouEditor | **Must Have** | `Status → RecommendedToBoard` |
| Approve submission | U | EditorialBoard | **Must Have** | `Status → Approved`; atomic Series creation (no role elevation) |
| Request revision | U | TantouEditor, EditorialBoard | **Must Have** | `Status → RevisionRequired`; `FeedbackMessage` required |
| Reject submission | U | TantouEditor, EditorialBoard | **Must Have** | `Status → Rejected`; `FeedbackMessage` required |

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

> ⚠️ **v3.0 Change:** Architecture migrated from **Microservices → Modular Monolith**. Single deployable, single database, modules as class libraries.

| Concern | Technology / Pattern |
|---------|---------------------|
| Architecture | **Modular Monolith** — 1 ASP.NET Core Web API host + 8 module class libraries |
| Language / Runtime | C# ASP.NET Core Web API on **.NET 9** |
| CQRS | **MediatR** with command/query segregation per module |
| Database | **Single SQL Server** — `AppDbContext` shared across all modules |
| ORM | **Entity Framework Core 9** — single migration history in `Shared.Infrastructure` |
| Auth | **JWT Bearer** + Refresh Token rotation (`RefreshTokens` table) |
| Validation | **FluentValidation** via MediatR pipeline behavior |
| Real-time | `Microsoft.AspNetCore.SignalR` WebSocket push notifications *(planned)* |
| Asset pipeline | **Hangfire** + `SixLabors.ImageSharp` for async `.png → .webp` *(planned)* |
| Object storage | **S3-compatible** (AWS S3 / Cloudflare R2) *(planned)* |
| Cache layer | **Redis** for distributed read cache *(planned)* |
| Email | **MailKit** SMTP — Gmail App Password (dev) / Brevo (prod) |
| Container | **Docker** single container; orchestrated with `docker-compose` locally |
| Deployment | **Railway** (cloud PaaS via GitHub Docker deploy) |

### Global Soft Delete Rule
All entities with `ISoftDeletable` (`IsDeleted`, `DeletedAt`) **MUST** have EF Core global query filters in `AppDbContext`. Soft-delete is intercepted in `SaveChangesAsync()` — hard deletes are converted automatically.

---

## 8. Solution Structure (Modular Monolith — `src-monolith/`)

> ✅ **Scaffold complete.** All projects created, dependencies wired, `InitialCreate` migration applied.

```
src-monolith/
├── MangaERP.sln
├── Dockerfile                          ← Single multi-stage Docker build (Railway-ready)
├── docker-compose.yml                  ← API + SQL Server + Maildev (local dev)
├── .env                                ← Local secrets (NOT committed to Git)
├── .env.example                        ← Template safe to commit
├── .gitignore / .dockerignore
└── src/
    ├── MangaERP.Api/                   ← [HOST] Program.cs, appsettings.json, Controllers
    │
    ├── Shared/
    │   ├── MangaERP.Shared.Domain/     ← AggregateRoot, Entity, ISoftDeletable, Exceptions
    │   ├── MangaERP.Shared.Application/← IDbContextProvider (anti-circular-dep), MediatR base
    │   └── MangaERP.Shared.Infrastructure/ ← AppDbContext, EF Configs, Migrations, DbSeeder
    │
    └── Modules/
        ├── MangaERP.Identity/          ← ✅ COMPLETE: Domain, Commands, Queries, Repos, Services, Controllers
        ├── MangaERP.Submission/        ← ✅ COMPLETE: Domain, Commands, Queries, Repos, Controller (MF1)
        ├── MangaERP.Series/            ← ✅ COMPLETE: Domain, Repos, Atomic integration (MF1)
        ├── MangaERP.Chapter/           ← 🔲 Domain entities done; Application/API pending (MF2)
        ├── MangaERP.Task/              ← 🔲 Domain entities done; Application/API pending (MF2)
        ├── MangaERP.QA/                ← 🔲 Domain entities done; Application/API pending (MF3)
        ├── MangaERP.Publishing/        ← 🔲 Domain entities done; Application/API pending (MF3)
        └── MangaERP.Ranking/           ← 🔲 Domain entities done; Application/API pending
```

### Module Status Legend
| Symbol | Meaning |
|--------|---------|
| ✅ | Fully implemented and tested |
| 🔲 | Planned / Domain exists / Not yet built |
| ⚠️ | Partially implemented or needs revision |

---

## 9. Implementation Progress Tracker

> Last updated: **2026-06-11**. Update this section every sprint.

### 9.1 Infrastructure & DevOps
| Item | Status | Notes |
|------|--------|-------|
| Modular Monolith scaffold (11 projects) | ✅ Done | All projects created, solution wired |
| EF Core `InitialCreate` migration | ✅ Done | Full schema: 17 tables across all modules |
| Docker single-container build | ✅ Done | Multi-stage Dockerfile, non-root user |
| `docker-compose.yml` (API + SQL + Maildev) | ✅ Done | Health checks, depends_on, restart policy |
| `.env` / `.env.example` secrets separation | ✅ Done | All secrets in `.env`, safe for Railway |
| `.gitignore` / `.dockerignore` | ✅ Done | Secrets excluded from Git and Docker build context |
| Railway deployment readiness | ✅ Done | Dynamic PORT, CORS config-driven, Swagger toggle |
| DbSeeder (Admin auto-seed) | ✅ Done | Reads password from `ADMIN_PASSWORD` env var; throws if missing |

### 9.2 Identity Module (MangaERP.Identity)
| Feature | API Endpoint | Status |
|---------|-------------|--------|
| Admin provision account | `POST /api/v1/admin/accounts/provision` | ✅ Done |
| Auto-generate username (Vietnamese support) | Internal service | ✅ Done |
| Send invitation email (HTML template) | SMTP via MailKit | ✅ Done |
| Activate account (set password) | `POST /api/v1/auth/activate` | ✅ Done |
| Login (JWT + refresh token) | `POST /api/v1/auth/login` | ✅ Done |
| List accounts | `GET /api/v1/admin/accounts` | ✅ Done |
| FluentValidation (provision + activate) | MediatR pipeline | ✅ Done |
| Logout (revoke token) | `POST /api/v1/auth/logout` | 🔲 Planned |
| Refresh access token | `POST /api/v1/auth/refresh` | 🔲 Planned |
| View/Edit profile | `GET/PUT /api/v1/profile` | 🔲 Planned |

### 9.3 MF1 — Series Submission Module (MangaERP.Submission + MangaERP.Series)
| Feature | Status |
|---------|--------|
| `SeriesSubmission` domain entity (all business methods) | ✅ Done |
| `MangaSeries` domain entity | ✅ Done |
| EF Core configurations for both entities | ✅ Done |
| Application Commands (CreateDraft, Submit, Recommend, Approve, Reject, RequestRevision) | ✅ Done |
| Application Queries (GetMySubmissions, GetQueue, GetDetail) | ✅ Done |
| API Controller (`SubmissionsController`) | ✅ Done |
| Atomic ApproveSubmission (Submission + Series in 1 transaction) | ✅ Done |

### 9.4 MF2 — Chapter & Task Modules
| Feature | Status |
|---------|--------|
| Domain entities (Chapter, PageTask, PreviewPage, ArtworkLayer, etc.) | ✅ Done |
| EF Core configurations | ✅ Done |
| Application layer | 🔲 Planned |
| API Controllers | 🔲 Planned |

### 9.5 MF3 — QA & Publishing Modules
| Feature | Status |
|---------|--------|
| Domain entities (BugPin, QASession, PublicationRecord, Notification) | ✅ Done |
| EF Core configurations | ✅ Done |
| Application layer | 🔲 Planned |
| API Controllers | 🔲 Planned |

### 9.6 Supporting Flows (MF5, MF6, MF7, MF8)
| Flow | Status |
|------|--------|
| MF5: Asset upload & ImageSharp optimization (Hangfire) | 🔲 Planned |
| MF6: Real-time SignalR notifications | 🔲 Planned |
| MF7: Artwork layer version control & audit log | 🔲 Planned |
| MF8: Redis cache invalidation on publish | 🔲 Planned |

---

## 10. Environment Configuration Reference

> Used by team members to set up local dev or configure Railway.

### 10.1 Local Dev (`.env` file — never commit)
| Variable | Description | Example |
|----------|-------------|---------|
| `JWT_KEY` | JWT signing secret (min 32 chars) | `MangaCAP@SuperSecTok...` |
| `SQL_SA_PASSWORD` | SQL Server SA password | `YourStrong@Passw0rd` |
| `ADMIN_PASSWORD` | Seeded admin account password | `Admin@Dev2026!` |
| `SMTP_USERNAME` | Gmail address for sending email | `yourname@gmail.com` |
| `SMTP_PASSWORD` | Gmail App Password (16 chars) | `abcd efgh ijkl mnop` |
| `SMTP_FROM_ADDRESS` | Sender address | `yourname@gmail.com` |
| `SMTP_FROM_NAME` | Sender display name | `MangaC&P Official` |
| `ACTIVATION_BASE_URL` | Frontend activation URL | `http://localhost:3000/activate` |

### 10.2 SMTP Modes
| Mode | Configuration | When to use |
|------|-------------|-------------|
| **Maildev** (default local) | `SMTP_USERNAME=` (empty) → routes to `maildev:1025` | Local dev, view at `http://localhost:1080` |
| **Gmail** (real email) | Fill `SMTP_USERNAME` + `SMTP_PASSWORD` App Password | Demo, staging |
| **Brevo** (production) | `SMTP_HOST=smtp-relay.brevo.com`, Brevo API key | Production Railway deploy |

### 10.3 Default Admin Account (seeded at first startup)
| Field | Value |
|-------|-------|
| Email / Username | `sysadmin.adm@company.com` |
| Password | Value of `ADMIN_PASSWORD` in `.env` |