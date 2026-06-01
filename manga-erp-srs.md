# System Requirement Specification (SRS) & Architecture: Manga Production & Publishing ERP

## 1. System Overview
This system is a specialized mini-ERP solution designed for modern comic/manga production studios (especially optimized for Webtoon and digital-first workflows). It manages the entire lifecycle of a manga chapter, from internal multi-layer artwork delegation (MF2) to editorial quality assurance with localized pinning (MF3), and automated scheduled production-ready publishing (MF8).

---

## 2. Actors & Permissions
* **Mangaka (Lead Author):** The owner of the Manga Series. Permissions include creating chapters, setting up page counts, assigning assistants to specific technical roles, reviewing/moderating internal submissions, and submitting the final draft for editorial review.
* **Assistant (Studio Crew):** Specialized artists (e.g., Line-art, Background, Background/Props coloring). Permissions include viewing assigned page tasks and uploading/overriding specific artwork layer files.
* **Tantou Editor (Assigned Editor):** The publishing house representative responsible for quality, localization, and compliance. Permissions include placing visual feedback via coordinate-based bug pins and executing final chapter approval.
* **Editorial Board (Publishing Management):** High-level management responsible for distribution strategies. Permissions include configuring release issue types (Weekly, Monthly, Special) and scheduling deployment timestamps.
* **System Handler (Automated Backend):** The automated C#/.NET engine executing background file optimizations, composite preview rendering, real-time WebSocket messaging, cache invalidation, and cold storage archiving.

---

## 3. Core Business Workflows

### MF2: MANGA PRODUCTION WORKFLOW (Internal Studio Pipeline)
* **Step 1 (Mangaka):** *Create Chapter & Setup Team* $\rightarrow$ Initializes a new chapter entity and maps specific assistants to persistent production roles (`LineArt`, `Background`, `Coloring`, `VFX`) for this execution scope.
* **Step 2 (Mangaka):** *Activate Page Task* $\rightarrow$ Triggers the active state for a pending page placeholder. Production runs in a sequential/rolling conveyor-belt fashion rather than releasing all pages simultaneously.
* **Step 3 (System Handler):** *Route Task Notification* $\rightarrow$ Dispatches a real-time system event targeting the designated assistant assigned to that page/role.
* **Step 4 (Assistant):** *Upload Artwork Layer* $\rightarrow$ Uploads the localized work-in-progress transparent `.png` asset layer. System shifts the `PageTask` status to `Reviewing`.
* **Step 5 (Mangaka):** *Review Submitted Layer* $\rightarrow$ Evaluates the assistant's layer delivery via the studio's workspace dashboard overlay viewer.
* **Step 6 [Decision] (Mangaka):** *Is Layer Accepted?*
    * **NO:** $\rightarrow$ Proceed to **Step 7**.
    * **YES:** $\rightarrow$ Proceed to **Step 8**.
* **Step 7 (System Handler):** *Process Revision Alert* $\rightarrow$ Flags the specific layer status to `RevisionAlert` and prompts the assistant with change requests (**Loop back to Step 4**).
* **Step 8 (System Handler):** *Generate Preview Page* $\rightarrow$ Locks the approved layer, fires the async image optimizer (MF5), and automatically merges all approved `.png` layers into a unified composite preview file (`.webp`).
* **Step 9 [Decision] (System Handler):** *Are All Chapter Pages Completed?*
    * System evaluates state logic: `TotalApprovedPages` == `TotalConfiguredPages`.
    * **NO:** $\rightarrow$ Set the next chronological page to `Incomplete` and trigger notification (**Loop back to Step 2**).
    * **YES:** $\rightarrow$ Proceed to **Step 10**.
* **Step 10 (Mangaka):** *Submit Chapter for Editorial QA* $\rightarrow$ Mangaka hits the "Submit" action. System converts `ChapterStatus` to `ReadyForQA`, locking studio modification rights and moving the active context to MF3.

### MF3: QA & PUBLISHING FLOW (Editorial & Release Pipeline)
* **Step 1 (Tantou Editor):** *Read & Check Chapter* $\rightarrow$ Editor renders the packaged chapter preview draft via an exclusive admin viewport to audit graphics, scripts, and content compliance.
* **Step 2 [Decision] (Tantou Editor):** *Detect Visual / Content Issues?*
    * **NO:** $\rightarrow$ Proceed to **Step 5**.
    * **YES:** $\rightarrow$ Proceed to **Step 3**.
* **Step 3 (Tantou Editor):** *Pin Bug Locations & Send Feedback Batch* $\rightarrow$ Clicks directly onto the visual canvas coordinates to register a `BugPin` with commentary. Executes "Send Feedback Batch" to compress all annotations into a single network notification payloads, shifting `ChapterStatus` to `Rejected`.
* **Step 4 (Mangaka):** *Fix Bugs at Pins & Resubmit* $\rightarrow$ Reviews the coordinate markers, overrides the broken layers (or delegates via MF2 tools), and hits "Resubmit Chapter" (**Loop back to Step 1** for re-evaluation).
* **Step 5 (Tantou Editor):** *Approve Chapter* $\rightarrow$ Closes out resolved pins (`Status = Resolved`) and flags the chapter as `Approved`.
* **Step 6 (Editorial Board):** *Select Issue Type* $\rightarrow$ Sets release categories (`Weekly`, `Monthly`, `Special`) and defines the automated `ScheduledPublishAt` cron/datetime trigger.
* **Step 7 (System Handler):** *Automated Publish & Cache Invalidation* $\rightarrow$ Upon scheduled timestamp reach, a worker daemon executes the deployment, switches state to `Published`, and invalidates public-facing Redis caches.
* **Step 8 (System Handler):** *Archive Production Layers* $\rightarrow$ An offline background job compresses the original multi-layered `.png` production assets and transfers them to a secure cold storage instance to free hot server storage capacity.

---

## 4. Supporting Sub-flows
* **MF5: Asset Upload & Optimization Flow:** Intercepts raw `.png` multi-layered streams, pushes them onto an internal messaging queue (e.g., Hangfire/Channels), utilizes `ImageSharp` for lossless compression/conversion to modern web formats (`.webp`/`.avif`), and multi-uploads to secure paths on AWS S3 / Cloudflare R2.
* **MF6: Real-time Notification Flow:** Employs `Microsoft.AspNetCore.SignalR` WebSockets to maintain persistent full-duplex pipelines, instantly rendering critical action toasts (`TaskAssignment`, `RevisionAlert`, `BugFeedback`) into active screens. Falls back to background email SMTP delivery if actors are offline.
* **MF7: Version Control & History Log Flow (Audit Trail):** Mitigates data loss by ensuring no files are overridden directly. Mutates past records to `IsCurrentVersion = 0`, incrementing `Version = Version + 1`. All transactions capture actor metadata inside a persistent `SystemAuditLogs` system.
* **MF8: Web Cache Invalidation Flow:** Handles high-concurrency read scenarios by wrapping public endpoints inside distributed memory storage (Redis). Triggers aggressive single-key or tag-based cache eviction during MF3 Step 7 execution.

---

## 5. Database Architecture Blueprint (SQL Server)
Configured with `UNIQUEIDENTIFIER` (Guid) keys for native compatibility with C# Clean Architecture/CQRS entities.

* **Users:** Identity, Authentication mapping, and platform `Role`.
* **MangaSeries:** Series master data linked to a primary Mangaka ID.
* **Chapters:** Contains state control flags (`Status`), metrics (`TotalPages`), and release timing metadata.
* **PageTasks:** Granular index controlling rolling execution (`PageNumber`, `TaskStatus`: `Pending`/`Incomplete`/`Reviewing`/`Approved`).
* **ChapterTeams:** Permissions cross-reference mapping which assistant holds which technical role inside a given chapter.
* **ArtworkLayers:** Tracking assets (`LayerType`, `FileUrlOriginal`, `FileUrlOptimized`, `Version`, `IsCurrentVersion`).
* **PreviewPages:** Flat composite URL endpoints consumed by public readers and internal reviewers.
* **BugPins:** Stores localized canvas placement metadata (`CoordinateX`, `CoordinateY` as percentage values), descriptions, and grouping tokens (`BatchToken`).
* **Notifications:** Real-time push payloads and read/unread trackers.
* **SystemAuditLogs:** Security and performance monitoring tracking history.
