# Frontend Integration Contract: Single Assistant Assignment & Reassignment Workflow

> **Phase**: Monolith Refactor - Single Active Assistant Model  
> **Status**: APPROVED & IMPLEMENTED  
> **Target FE Audience**: MangaStudioPlatform FE Developers

---

## 1. Executive Summary & Required FE Changes

The Backend workflow has been refactored to enforce **exactly ONE active Assistant per task**. All Primary/Backup/Takeover dual-assignment and backup standby logic has been retired.

### Deprecated Endpoints & Fields to Remove from FE
- **Remove API Endpoint**: `POST /api/v1/tasks/{taskId}/takeover` (returns `410 Gone`).
- **Remove Request Fields**:
  - `PrimaryAssistantId` (use `assistantId` or `newAssistantId` instead).
  - `BackupAssistantId` (removed from runtime contracts).
  - `AssignmentRole` ("Primary", "Backup", "BackupTakeover" -> replaced with single assignment attempt model).
- **Remove Response Fields**:
  - `currentPrimary` / `currentBackup` in assignment history (replaced with `currentAssignment`).

---

## 2. Canonical API Endpoints

| Operation | HTTP Method | Route | Authorization Role |
| :--- | :--- | :--- | :--- |
| **Get Candidates** | `GET` | `/api/v1/tasks/{taskId}/assistant-candidates` | `Mangaka` |
| **Assign Task** | `POST` | `/api/v1/tasks/{taskId}/assignments` | `Mangaka` |
| **Respond Assignment** | `POST` | `/api/v1/tasks/assignments/{attemptId}/respond` | `Assistant` |
| **Cancel Assignment** | `POST` | `/api/v1/tasks/assignments/{attemptId}/cancel` | `Mangaka` |
| **Reassign Task** | `POST` | `/api/v1/tasks/{taskId}/reassign` | `Mangaka` |
| **Get Assignment History** | `GET` | `/api/v1/tasks/{taskId}/assignment-history` | `Mangaka`, `Assistant`, `TantouEditor` |
| **Get Assistant Workload** | `GET` | `/api/v1/assistants/{assistantId}/workload` | `Mangaka`, `Assistant`, `TantouEditor` |

---

## 3. Detailed API Contracts

### 3.1 Get Assistant Candidates
**`GET /api/v1/tasks/{taskId}/assistant-candidates`**

**Response Body (`200 OK`)**:
```json
{
  "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seriesId": "4a285f64-5717-4562-b3fc-2c963f66afa7",
  "maxWorkload": 3,
  "availableAssistants": [
    {
      "assistantId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "displayName": "Alex Tanaka",
      "email": "alex@studio.com",
      "activeTaskCount": 1,
      "pendingAssignmentCount": 0,
      "totalWorkload": 1,
      "maxWorkload": 3,
      "remainingCapacity": 2,
      "hasSeriesAccess": true,
      "isAvailable": true,
      "availabilityCode": "Available",
      "availabilityReason": null
    }
  ],
  "unavailableAssistants": [
    {
      "assistantId": "8d9e6679-7425-40de-944b-e07fc1f90ae8",
      "displayName": "Ken Sato",
      "email": "ken@studio.com",
      "activeTaskCount": 3,
      "pendingAssignmentCount": 0,
      "totalWorkload": 3,
      "maxWorkload": 3,
      "remainingCapacity": 0,
      "hasSeriesAccess": true,
      "isAvailable": false,
      "availabilityCode": "WorkloadLimitReached",
      "availabilityReason": "Assistant has reached the maximum workload limit (3)."
    }
  ]
}
```

---

### 3.2 Initial Assignment
**`POST /api/v1/tasks/{taskId}/assignments`**

**Request Body**:
```json
{
  "assistantId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "description": "Clean up line art and draw background trees",
  "deadline": "2026-08-01T12:00:00Z",
  "durationHours": 48,
  "responseDeadline": "2026-07-29T12:00:00Z"
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
    "status": "PendingAcceptance",
    "assignmentRole": "Direct",
    "assignedAt": "2026-07-27T14:30:00Z",
    "expiresAt": "2026-07-29T12:00:00Z",
    "assignedByUserId": "5a1e6679-7425-40de-944b-e07fc1f90ae1",
    "concurrencyToken": "2b1e6679-7425-40de-944b-e07fc1f90ae2"
  }
}
```

---

### 3.3 Reassign Task
**`POST /api/v1/tasks/{taskId}/reassign`**

**Request Body**:
```json
{
  "newAssistantId": "8d9e6679-7425-40de-944b-e07fc1f90ae8",
  "reason": "Previous assistant fell ill and requested replacement",
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
    "status": "PendingAcceptance",
    "assignmentRole": "Direct",
    "assignedAt": "2026-07-27T14:35:00Z",
    "assignedByUserId": "5a1e6679-7425-40de-944b-e07fc1f90ae1",
    "concurrencyToken": "4b1e6679-7425-40de-944b-e07fc1f90ae5"
  }
}
```

---

### 3.4 Respond Assignment
**`POST /api/v1/tasks/assignments/{attemptId}/respond`**

**Request Body**:
```json
{
  "accept": true,
  "rejectionReason": null,
  "expectedConcurrencyToken": "4b1e6679-7425-40de-944b-e07fc1f90ae5"
}
```

**Response Body (`200 OK`)**:
```json
{
  "id": "0c1e6679-7425-40de-944b-e07fc1f90ae3",
  "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "assistantId": "8d9e6679-7425-40de-944b-e07fc1f90ae8",
  "status": "Accepted",
  "assignmentRole": "Direct",
  "acceptedAt": "2026-07-27T14:36:00Z"
}
```

---

### 3.5 Assignment History
**`GET /api/v1/tasks/{taskId}/assignment-history`**

**Response Body (`200 OK`)**:
```json
{
  "currentAssignment": {
    "id": "0c1e6679-7425-40de-944b-e07fc1f90ae3",
    "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "assistantId": "8d9e6679-7425-40de-944b-e07fc1f90ae8",
    "attemptNumber": 2,
    "status": "Accepted",
    "assignmentRole": "Direct",
    "assignedAt": "2026-07-27T14:35:00Z",
    "acceptedAt": "2026-07-27T14:36:00Z"
  },
  "history": [
    {
      "id": "9b1e6679-7425-40de-944b-e07fc1f90ae9",
      "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "assistantId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "attemptNumber": 1,
      "status": "Superseded",
      "assignmentRole": "Direct",
      "assignedAt": "2026-07-27T14:30:00Z",
      "acceptedAt": "2026-07-27T14:31:00Z",
      "rejectionReason": "Superseded by replacement assignment (Attempt #2)."
    },
    {
      "id": "0c1e6679-7425-40de-944b-e07fc1f90ae3",
      "taskId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "assistantId": "8d9e6679-7425-40de-944b-e07fc1f90ae8",
      "attemptNumber": 2,
      "status": "Accepted",
      "assignmentRole": "Direct",
      "assignedAt": "2026-07-27T14:35:00Z",
      "acceptedAt": "2026-07-27T14:36:00Z"
    }
  ]
}
```

---

## 4. Status Definitions

### Assignment Attempt Statuses
- `PendingAcceptance`: Invitation sent by Mangaka; awaiting response.
- `Accepted`: Accepted by Assistant; candidate is active executor.
- `Rejected`: Rejected by Assistant; task transitions to `ReassignmentRequired`.
- `Expired`: Response deadline passed without response.
- `Cancelled`: Cancelled by Mangaka before acceptance.
- `Superseded`: Reassigned; replaced by a new accepted attempt.

### PageTask Statuses
- `Pending`: Task created, no active assignment invitation.
- `PendingAcceptance`: Assignment invitation sent, awaiting response.
- `Incomplete`: Active task in progress by accepted assistant.
- `ReassignmentRequired`: Previous invitation rejected/expired; awaiting Mangaka reassignment.
- `Reviewing`: Task completed by assistant, submitted for review.
- `RevisionAlert`: Mangaka requested revisions.
- `Approved`: Task approved by Mangaka.
- `Cancelled`: Task soft-deleted via Cancel-and-Recreate.

---

## 5. Work Continuation Rules for FE

1. **AssignedAssistantId Update**: `AssignedAssistantId` remains unchanged (pointing to old executor or null) while a replacement invitation is `PendingAcceptance`. It updates to the new assistant ONLY after the new assistant calls Accept (`POST .../respond`).
2. **WorkStartedAt Preservation**: `Task.WorkStartedAt` is set when accepted for the first time and is NEVER reset during reassignment.
3. **Data Preservation**: Reassign retains `TaskId`, `ChapterId`, `PageNumber`, `TaskType`, `BaseImageUrl`, `ProgressPercent`, progress history, checkpoints, layers, versions, comments, and files.
4. **Write Access**: Only the current accepted executor (`task.AssignedAssistantId == assistantId`) can submit progress, upload files, or complete the task.

---

## 6. Error Code Matrix

| HTTP Status | Error Type / Exception | Cause | FE Handling Recommendation |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | `ArgumentException` | Missing required field (`assistantId`, `reason`, `rejectionReason`). | Display inline field validation error. |
| `403 Forbidden` | `UnauthorizedAccessException` | Non-owner calling assign/reassign or non-assigned user writing task. | Redirect or show access denied warning. |
| `409 Conflict` | `ConflictException` | Task already has active attempt or assistant reached max workload (3). | Prompt user with conflict message or refresh candidate list. |
| `410 Gone` | Deprecated Route | Calling retired `POST /api/v1/tasks/{taskId}/takeover`. | Remove takeover button from UI and redirect to Reassign dialog. |
