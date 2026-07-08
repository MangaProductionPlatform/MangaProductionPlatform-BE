# MangaERP - FE Integration Guide

> Cap nhat: 2026-07-08  
> Muc tieu: huong dan FE tich hop voi BE hien tai theo dung contract, dung role, dung thu tu uu tien.

---

## 1. Uu tien bat buoc truoc khi lam man hinh

### 1.1. Auth / Cookie contract

BE dang tra:

- `accessToken`, `role`, `userId` trong response body cua `POST /api/v1/auth/login`.
- `refreshToken` nam trong **httpOnly cookie**, khong tra ve body.

FE can sua:

- Khong doc `refreshToken` tu response body `/login`.
- Bo `refreshToken` khoi `CurrentUser` type/local session.
- Tat ca request can cookie phai co:

```ts
credentials: "include"
```

Flow dung:

| Viec | API | Ghi chu |
|---|---|---|
| Login | `POST /api/v1/auth/login` | Body chi can email/password; response co access token, cookie tu set |
| Refresh access token | `POST /api/v1/auth/refresh` | Khong gui refresh token trong body; browser gui cookie |
| Logout | `POST /api/v1/auth/logout` | BE xoa cookie + blacklist access token neu co |
| Activate account | `POST /api/v1/auth/activate` | Dung token tu email |
| Forgot password | `POST /api/v1/auth/forgot-password` | Gui OTP |
| Reset password | `POST /api/v1/auth/reset-password` | Doi mat khau bang OTP |

### 1.2. Chapter contract

FE **khong duoc gui** `assignedEditorId` khi tao/sua chapter.

Dung payload:

```ts
type CreateChapterPayload = {
  seriesId: string;
  title: string;
  chapterNumber: number;
  totalPages: number;
  coverImageUrl?: string | null;
};

type UpdateChapterPayload = {
  title: string;
  chapterNumber: number;
  totalPages: number;
  coverImageUrl?: string | null;
};

type PatchChapterPayload = {
  title?: string | null;
  chapterNumber?: number | null;
  totalPages?: number | null;
  coverImageUrl?: string | null;
};
```

BE se tu giu hoac gan Tantou Editor tu `ManagingTantouId` cua Mangaka.

### 1.3. Notifications method contract

Dung method:

| Viec | API |
|---|---|
| List notifications | `GET /api/v1/notifications` |
| Unread count | `GET /api/v1/notifications/unread-count` |
| Mark one as read | `PATCH /api/v1/notifications/{id}/read` |
| Mark all as read | `PATCH /api/v1/notifications/read-all` |
| Delete one | `DELETE /api/v1/notifications/{id}` |
| Delete read/all supported by BE | `DELETE /api/v1/notifications` |

Khong dung `POST /notifications/{id}/read`.

---

## 2. Thu tu tich hop khuyen nghi

1. Auth client contract: `credentials: "include"`, bo `refreshToken` khoi FE state.
2. Notifications dung chung cho moi role.
3. Profile/settings dung chung.
4. Admin dashboard + roles + workflow stats.
5. Mangaka: submission, series, studio, chapter/task.
6. Assistant: invitation, tasks, income, comments, layer history.
7. Tantou Editor: series/chapter queue, QA queue, QA canvas.
8. EditorialBoard/EditorInChief: voting, reports, cancellation review.
9. SAM/Segmentation task workflow.
10. Defer: Ranking/Public va Publishing calendar cho den khi BE Bach hoan thien API.

---

## 3. API dung chung moi role

### Profile

| Man hinh | API | Ghi chu |
|---|---|---|
| Profile page | `GET /api/v1/users/me` | Lay thong tin user dang login |
| Edit profile | `PUT /api/v1/users/profile` | PenName, drawing softwares, bank account |
| Avatar | `PUT /api/v1/users/me/avatar` | Cap nhat avatar URL |
| Change password | `PUT /api/v1/users/me/change-password` | Doi mat khau chu dong |

### Media

| Viec | API |
|---|---|
| Upload file | `POST /api/v1/media/upload` |

### Notifications

Nen lam component dung chung:

- Notification page.
- Navbar/sidebar unread badge.
- Mark read/read all/delete.
- SignalR hub: `/hubs/notifications` neu FE co thoi gian tich hop realtime.

---

## 4. Admin UI

### Da co BE API

| Man hinh FE | API |
|---|---|
| Admin dashboard | `GET /api/v1/admin/dashboard` |
| Workflow monitoring | `GET /api/v1/admin/workflow-stats` |
| Roles reference | `GET /api/v1/admin/roles` |
| List accounts | `GET /api/v1/admin/accounts` |
| Account detail | `GET /api/v1/admin/accounts/{userId}` |
| Provision account | `POST /api/v1/admin/accounts/provision` |
| Update account | `PUT /api/v1/admin/accounts/{userId}` |
| Update role | `PATCH /api/v1/admin/accounts/{userId}/role` |
| Update status | `PATCH /api/v1/admin/accounts/{userId}/status` |
| Resend activation | `POST /api/v1/admin/accounts/{userId}/resend-activation` |
| Delete account | `DELETE /api/v1/admin/accounts/{userId}` |
| SAM config | `PATCH /api/v1/admin/sam-config` |

### FE can lam

- Thay `EmptyBackendState` o Admin dashboard, roles, workflow monitoring, notifications.
- Dong bo error/loading/toast cho user management.
- An hoac de backlog cac man: AI/storage/system settings neu chua co scope.

---

## 5. Mangaka UI

### Submission / Series Proposal

| Man hinh | API |
|---|---|
| Create draft | `POST /api/v1/submissions/draft` |
| Update metadata | `PUT /api/v1/submissions/{id}/metadata` |
| Update manuscript | `PUT /api/v1/submissions/{id}/manuscript` |
| Submit | `POST /api/v1/submissions/{id}/submit` |
| Resubmit | `POST /api/v1/submissions/{id}/resubmit` |
| My submissions | `GET /api/v1/submissions/my` |
| Submission detail | `GET /api/v1/submissions/{id}` |
| Delete draft | `DELETE /api/v1/submissions/{id}` |
| Feedback pins | `GET /api/v1/submissions/{id}/feedback-pins` |
| Feedback history | `GET /api/v1/submissions/{id}/feedback-pins/history` |

### Series lifecycle

| Viec | API |
|---|---|
| My series | `GET /api/v1/series/my` |
| Series detail | `GET /api/v1/series/{id}` |
| Update series | `PUT /api/v1/series/{id}` |
| Set hiatus | `POST /api/v1/series/{id}/set-hiatus` |
| Reactivate | `POST /api/v1/series/{id}/reactivate` |
| Request cancellation | `POST /api/v1/series/{id}/request-cancellation` |

### Studio membership

| Viec | API |
|---|---|
| Invite assistant | `POST /api/v1/studios/{seriesId}/invitations` |
| List invitations | `GET /api/v1/studios/{seriesId}/invitations` |
| List members | `GET /api/v1/studios/{seriesId}/members` |
| Cancel invitation | `POST /api/v1/studios/invitations/{invitationId}/cancel` |
| Remove assistant | `DELETE /api/v1/studios/{seriesId}/members/{assistantId}` |

### Chapter / Task production

| Viec | API | Ghi chu |
|---|---|---|
| Create chapter | `POST /api/v1/chapters` | Khong gui `assignedEditorId` |
| Chapters by series | `GET /api/v1/chapters/series/{seriesId}` | |
| Chapter detail | `GET /api/v1/chapters/{chapterId}` | |
| Chapter pages | `GET /api/v1/chapters/{chapterId}/pages` | |
| Update chapter | `PUT /api/v1/chapters/{chapterId}` | Khong gui `assignedEditorId` |
| Patch chapter | `PATCH /api/v1/chapters/{chapterId}` | Khong gui `assignedEditorId` |
| Delete chapter | `DELETE /api/v1/chapters/{chapterId}` | |
| Add base page | `POST /api/v1/chapters/{chapterId}/pages` | |
| Assign page task | `POST /api/v1/chapters/{chapterId}/pages/activate` | |
| Bulk assign | `POST /api/v1/chapters/{chapterId}/pages/bulk-activate` | |
| Reassign task | `PUT /api/v1/chapters/{chapterId}/pages/{pageNumber}/reassign` | |
| Update deadline | `PATCH /api/v1/tasks/{pageTaskId}/deadline` | |
| Studio task board | `GET /api/v1/studios/{seriesId}/tasks/board` | |
| Recommend assistants | `GET /api/v1/chapters/{chapterId}/recommend-assistants` | |
| Tasks by chapter | `GET /api/v1/tasks/chapter/{chapterId}` | |
| Review layer | `POST /api/v1/tasks/{pageTaskId}/review` | |
| Bulk review layers | `POST /api/v1/tasks/bulk-review` | |
| Task comments | `GET/POST /api/v1/tasks/{pageTaskId}/comments` | |
| Layer versions | `GET /api/v1/tasks/{pageTaskId}/layers/{layerType}/versions` | |
| Rollback layer | `POST /api/v1/tasks/{pageTaskId}/layers/{layerType}/rollback` | |
| Submit for QA | `POST /api/v1/chapters/{chapterId}/submit-for-qa` | |

### SAM / Region mask

| Viec | API |
|---|---|
| Get SAM embedding | `POST /api/segmentation/embedding` |
| Predict mask | `POST /api/segmentation/predict` |
| Set page region | `POST /api/v1/chapters/{chapterId}/pages/region` |
| Create segmentation task | `POST /api/segmentation/tasks` |

---

## 6. Assistant UI

### Invitations

| Viec | API |
|---|---|
| Pending invitations | `GET /api/v1/studios/invitations/pending` |
| Accept | `POST /api/v1/studios/invitations/{invitationId}/accept` |
| Decline | `POST /api/v1/studios/invitations/{invitationId}/decline` |

### Task workflow

| Viec | API |
|---|---|
| Assigned tasks | `GET /api/v1/tasks/assigned` |
| Task detail | `GET /api/v1/tasks/{pageTaskId}` |
| Submit layer | `POST /api/v1/tasks/{pageTaskId}/layers` |
| Task comments | `GET/POST /api/v1/tasks/{pageTaskId}/comments` |
| Layer versions | `GET /api/v1/tasks/{pageTaskId}/layers/{layerType}/versions` |
| Income | `GET /api/v1/assistant/tasks/income` |
| Mark QA pin fixed | `POST /api/v1/qa/pins/{pinId}/fixed` |

### Segmentation tasks

| Viec | API |
|---|---|
| My segmentation tasks | `GET /api/segmentation/tasks/mine` |
| Update status | `PATCH /api/segmentation/tasks/{id}/status` |

---

## 7. Tantou Editor UI

### Series / Chapter monitoring

| Man hinh | API |
|---|---|
| Managed series | `GET /api/v1/series` |
| Series detail | `GET /api/v1/series/{id}` |
| Chapters by series | `GET /api/v1/chapters/series/{seriesId}` |
| My chapter queue | `GET /api/v1/chapters/my-queue` |
| Task detail | `GET /api/v1/tasks/{pageTaskId}` |
| Layer history | `GET /api/v1/tasks/layers/history` |
| Studio task board | `GET /api/v1/studios/{seriesId}/tasks/board` |
| Task comments | `GET/POST /api/v1/tasks/{pageTaskId}/comments` |

### QA Canvas / Review

| Viec | API |
|---|---|
| QA queue | `GET /api/v1/qa/queue` |
| QA session | `GET /api/v1/qa/chapters/{chapterId}/session` |
| QA pins | `GET /api/v1/qa/chapters/{chapterId}/pins` |
| Add bug pin | `POST /api/v1/qa/chapters/{chapterId}/pins` |
| Send feedback batch | `POST /api/v1/qa/chapters/{chapterId}/send-feedback` |
| Resolve pin | `POST /api/v1/qa/pins/{pinId}/resolve` |
| Approve chapter | `POST /api/v1/qa/chapters/{chapterId}/approve` |

### Dang doi BE Bach

- QA history/timeline.
- Reopen QA.
- Edit/delete QA pin.
- Publishing queue cua Tantou.

---

## 8. EditorialBoard / EditorInChief UI

### Board dashboard / reports

| Man hinh | API |
|---|---|
| Board reports | `GET /api/v1/board/reports` |
| Performance reports | `GET /api/v1/board/performance-reports` |

### Voting center

| Viec | API | Role |
|---|---|---|
| Submission queue | `GET /api/v1/submissions/queue` | EB, EiC |
| Submission detail | `GET /api/v1/submissions/{id}` | EB, EiC |
| Vote history | `GET /api/v1/submissions/{id}/votes` | EB, EiC |
| Cast vote | `POST /api/v1/submissions/{id}/vote` | EditorialBoard |
| Resolve conflict | `POST /api/v1/submissions/{id}/resolve-conflict` | EditorInChief |
| Request revision | `POST /api/v1/submissions/{id}/request-revision` | EB, EiC |

### Cancellation review

| Viec | API |
|---|---|
| Cancellation queue | `GET /api/v1/series/cancellation-queue` |
| Approve cancellation | `POST /api/v1/series/{id}/approve-cancellation` |
| Reject cancellation | `POST /api/v1/series/{id}/reject-cancellation` |

### Publishing commands da co

| Viec | API |
|---|---|
| Schedule publish | `POST /api/v1/publishing/schedule` |
| Update schedule | `PATCH /api/v1/publishing/chapters/{chapterId}/schedule` |
| Cancel schedule | `DELETE /api/v1/publishing/chapters/{chapterId}/schedule` |
| Publish now | `POST /api/v1/publishing/publish` |
| Series publishing history | `GET /api/v1/publishing/series/{seriesId}/history` |

### Dang doi BE Bach

- `GET /api/v1/publishing/chapters/ready`
- `GET /api/v1/publishing/schedule`
- `GET /api/v1/publishing/chapters/{id}`
- `GET /api/v1/publishing/chapters/my-queue`

---

## 9. Public / Reader / Ranking

Chua nen uu tien tich hop API that vi BE Ranking/Public browsing chua hoan thien.

Co the tam thoi:

- Giu mock data.
- Hoac an menu neu demo noi bo chi tap trung workflow san xuat.

Dang doi BE:

- Ranking controller/API.
- Ranking calculation job.
- Public manga listing/discover/trending/genres.
- Public series/chapter reader endpoints.

---

## 10. Checklist truoc khi FE bao hoan thanh

- [ ] Tat ca request dung `credentials: "include"`.
- [ ] FE khong luu/doc `refreshToken` tu JS.
- [ ] FE khong gui `assignedEditorId` trong chapter create/update/patch.
- [ ] Notification page va unread badge dung API that.
- [ ] Cac trang khong con `EmptyBackendState` neu BE da co API.
- [ ] Error 401/403 duoc hien thi ro, khong silent fail.
- [ ] Role guard tren router dung voi role BE: `Admin`, `Mangaka`, `Assistant`, `TantouEditor`, `EditorialBoard`, `EditorInChief`.
- [ ] Board UI khong goi `/admin/dashboard`.
- [ ] QA route dung chapter id: `/api/v1/qa/chapters/{chapterId}/...`.
- [ ] Ranking/Publishing calendar duoc danh dau "waiting BE" neu chua co endpoint read.

