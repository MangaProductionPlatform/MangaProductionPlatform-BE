# Frontend Integration Contract: Direct Immediate Assistant Task Assignment & Reassignment Workflow

> **Phase**: Monolith Refactor - Direct Immediate Assignment Model  
> **Status**: APPROVED & IMPLEMENTED  
> **Target FE Audience**: MangaStudioPlatform FE Developers

---

## 1. Executive Summary & Core Business Rule

Under the **Direct Immediate Assignment Model**:
1. **Assistant accepts invitation ONCE** when joining the Studio/Series/Collaboration.
2. Once an Assistant is a valid member of a Series (Active Collaboration + Active `SeriesAccessGrant`), **task assignment and reassignment take effect IMMEDIATELY upon Mangaka action**.
3. **No task-level Accept/Reject step**: Task invitations, task rejection, response deadlines, and `PendingAcceptance` badges are completely eliminated.
4. **Immediate Write Access Transfer**: On Reassign, the old Assistant loses write access immediately and the new Assistant gains write access immediately.
5. **Data Preservation**: Reassign preserves `TaskId`, original `WorkStartedAt`, progress %, checkpoints, artwork layers, files, comments, and submission history.

---

## 2. Deprecated Endpoints & Removed Parameters

- **Retired API Endpoints**:
  - `POST /api/v1/tasks/assignments/{attemptId}/respond` (returns `410 Gone`).
  - `POST /api/v1/tasks/{taskId}/takeover` (returns `410 Gone`).
- **Removed Request Parameters**:
  - `responseDeadline` (removed from Assign/Reassign requests).
  - `PrimaryAssistantId` / `BackupAssistantId` (retired; use `assistantId` or `newAssistantId`).

---

## 3. Canonical API Endpoints

| Operation | HTTP Method | Route | Authorization Role | Immediate Effect |
| :--- | :--- | :--- | :--- | :--- |
| **Get Candidates** | `GET` | `/api/v1/tasks/{taskId}/assistant-candidates` | `Mangaka` | Returns eligible active series members |
| **Assign Task** | `POST` | `/api/v1/tasks/{taskId}/assignments` | `Mangaka` | `AssignedAssistantId` set, task `Incomplete`, `WorkStartedAt` set |
| **Reassign Task** | `POST` | `/api/v1/tasks/{taskId}/reassign` | `Mangaka` | `AssignedAssistantId` updated, old superseded, new active |
| **Cancel & Recreate** | `POST` | `/api/v1/tasks/{taskId}/cancel-and-recreate` | `Mangaka` | Soft-deletes old task, creates new unassigned task |
| **Get Assignment History** | `GET` | `/api/v1/tasks/{taskId}/assignment-history` | `Mangaka`, `Assistant`, `TantouEditor` | Returns current assignment + history |
| **Get Assistant Workload** | `GET` | `/api/v1/assistants/{assistantId}/workload` | `Mangaka`, `Assistant`, `TantouEditor` | Returns count of active task responsibilities |

---

## 4. API Request & Response Contracts

### 4.1 Assign Task
**`POST /api/v1/tasks/{taskId}/assignments`**

**Request Body**:
```json
{
  "assistantId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "description": "Clean line art and draw background",
  "deadline": "2026-08-01T12:00:00Z",
  "durationHours": 48
}
```

**Response Body (`200 OK`)**:
```json
{
  "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "attempt": {
    "id": "9b1e6679-7425-40de-944b-e07fc1f90ae9",
    "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "assistantId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "collaborationId": "1c1e6679-7425-40de-944b-e07fc1f90ae0",
    "attemptNumber": 1,
    "status": "Accepted",
    "assignmentRole": "Direct",
    "assignedAt": "2026-07-27T14:30:00Z",
    "acceptedAt": "2026-07-27T14:30:00Z",
    "assignedByUserId": "5a1e6679-7425-40de-944b-e07fc1f90ae1",
    "concurrencyToken": "2b1e6679-7425-40de-944b-e07fc1f90ae2"
  }
}
```

---

### 4.2 Reassign Task
**`POST /api/v1/tasks/{taskId}/reassign`**

**Request Body**:
```json
{
  "newAssistantId": "8d9e6679-7425-40de-944b-e07fc1f90ae8",
  "reason": "Previous assistant fell ill",
  "deadline": "2026-08-03T12:00:00Z",
  "description": "Continue background shading from 45% progress"
}
```

**Response Body (`200 OK`)**:
```json
{
  "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "attempt": {
    "id": "0c1e6679-7425-40de-944b-e07fc1f90ae3",
    "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "assistantId": "8d9e6679-7425-40de-944b-e07fc1f90ae8",
    "collaborationId": "3c1e6679-7425-40de-944b-e07fc1f90ae4",
    "attemptNumber": 2,
    "status": "Accepted",
    "assignmentRole": "Direct",
    "assignedAt": "2026-07-27T14:35:00Z",
    "acceptedAt": "2026-07-27T14:35:00Z",
    "assignedByUserId": "5a1e6679-7425-40de-944b-e07fc1f90ae1",
    "concurrencyToken": "4b1e6679-7425-40de-944b-e07fc1f90ae5"
  }
}
```

---

### 4.3 Cancel and Recreate Task
**`POST /api/v1/tasks/{taskId}/cancel-and-recreate`**

**Request Body**:
```json
{
  "cancellationCategory": "AssistantAbandonedTask",
  "reason": "Assistant stopped responding after 3 days",
  "confirmProgressLoss": true,
  "copyTaskDetails": true
}
```

**Response Body (`200 OK`)**:
```json
{
  "cancelledTaskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "newPageTaskId": "4da85f64-5717-4562-b3fc-2c963f66afa7",
  "status": "Pending",
  "pageNumber": 1,
  "baseImageUrl": "https://example.com/base.png"
}
```

---

## 5. UI/UX Changes Required for FE

1. **Remove Accept/Reject Screens**: Remove all task-level invitation popups, dialogs, or Accept/Reject buttons from Assistant UI.
2. **Remove PendingBadges**: Remove `PendingAcceptance` status badges from Task Management tables.
3. **Remove ResponseDeadline Inputs**: Remove `ResponseDeadline` date pickers from Task Assign forms.
4. **My Tasks Dashboard**: Tasks appear directly in the Assistant's "My Active Tasks" list as soon as Mangaka assigns them.
5. **Immediate UI Refresh**: Upon calling Assign or Reassign, refresh task view immediately. The task displays `Incomplete`/`InProgress` status with the assigned Assistant's avatar.
6. **Informational Notifications**: Notifications (`TaskAssigned`, `TaskReassigned`) are purely informational alerts. Clicking a notification opens the active task view.

---

## 6. Cancel-and-Recreate Candidate Exclusion Rule (UI & Backend Contract)

1. **Assistant Exclusion Rule**:
   - When a task is Cancel-and-Recreated due to an **assistant-related category** (`AssistantAbandonedTask`, `AssistantUnavailable`, `AssistantFailedToStart`, `AssistantRemovedForPerformance`), the previous assistant assigned to that task is excluded from being assigned/reassigned to the newly recreated task.
   - When recreated due to a **task-related category** (`InvalidTaskDefinition`, `WrongBaseImage`, `WrongLayout`, `WrongTaskType`, `WrongPageNumber`, `OtherTaskIssue`), the previous assistant remains eligible.
2. **Candidate API Behavior (`GET /api/v1/tasks/{taskId}/assistant-candidates`)**:
   - Excluded assistant will appear under `unavailableAssistants` list with:
     - `isAvailable`: `false`
     - `availabilityCode`: `"PreviousTaskAssigneeExcluded"`
     - `availabilityReason`: `"This assistant was removed from the previous version of this task."`
3. **FE UI Requirements**:
   - Render the excluded assistant as disabled in the candidate selector dropdown/modal.
   - Display the clear reason tooltip/text: *"This assistant was removed from the previous version of this task."*
   - Do NOT try to infer exclusion logic client-side; always rely on `availabilityCode` from the Candidate API.
4. **Backend Security Enforcement**:
   - Direct Assign (`POST /api/v1/tasks/{taskId}/assignments`) and Direct Reassign (`POST /api/v1/tasks/{taskId}/reassign`) endpoints independently enforce this rule and return `409 Conflict` with message `PREVIOUS_TASK_ASSIGNEE_EXCLUDED: ...` if a manual request is attempted.
