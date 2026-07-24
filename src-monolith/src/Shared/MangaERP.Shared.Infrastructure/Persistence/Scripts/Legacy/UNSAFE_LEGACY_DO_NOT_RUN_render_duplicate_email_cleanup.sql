-- ==============================================================================
-- [CRITICAL WARNING] UNSAFE LEGACY SCRIPT - DO NOT RUN ON STAGING OR PRODUCTION!
-- WARNING: This is an unverified legacy script with all-or-nothing transactional risks.
-- If validation fails, all audit logs and changes are rolled back without trace.
-- DO NOT EXECUTE ON STAGING OR PRODUCTION ENVIRONMENTS.
-- Use safe_render_personal_email_merge_v2.sql instead after independent dry-run audit.
-- ==============================================================================
-- SCRIPT CHUẨN HÓA VÀ GỘP (MERGE) DỮ LIỆU TRÙNG PersonalEmail TRÊN RENDER POSTGRESQL (LEGACY)
-- Ngày thực hiện: 24/07/2026
-- ==============================================================================

BEGIN TRANSACTION;

-- 1. Bảng tạm chứa thông tin gộp tài khoản (Survivor vs Duplicate)
CREATE TEMP TABLE TempUserMergeMapping AS
WITH RankedUsers AS (
    SELECT
        "Id",
        LOWER(TRIM("PersonalEmail")) AS norm_email,
        ROW_NUMBER() OVER (
            PARTITION BY LOWER(TRIM("PersonalEmail"))
            ORDER BY
                CASE WHEN "AccountStatus" = 'Active' THEN 1 ELSE 2 END,
                "CreatedAt" ASC
        ) AS rn
    FROM "Users"
    WHERE "PersonalEmail" IS NOT NULL AND TRIM("PersonalEmail") <> ''
),
Survivors AS (
    SELECT "Id" AS survivor_id, norm_email
    FROM RankedUsers WHERE rn = 1
),
Duplicates AS (
    SELECT "Id" AS duplicate_id, norm_email
    FROM RankedUsers WHERE rn > 1
)
SELECT
    d.duplicate_id,
    s.survivor_id,
    d.norm_email
FROM Duplicates d
JOIN Survivors s ON d.norm_email = s.norm_email;

-- Log thông tin các tài khoản bị trùng
DO $$
DECLARE
    merge_count INT;
BEGIN
    SELECT COUNT(*) INTO merge_count FROM TempUserMergeMapping;
    RAISE NOTICE 'Phát hiện % tài khoản trùng PersonalEmail cần gộp.', merge_count;
END $$;

-- 2. Cập nhật các bảng tham chiếu (Foreign Keys) chuyển về survivor_id
UPDATE "SeriesSubmissions" s
SET "SubmitterId" = m.survivor_id
FROM TempUserMergeMapping m
WHERE s."SubmitterId" = m.duplicate_id;

UPDATE "SeriesSubmissions" s
SET "AssignedEditorId" = m.survivor_id
FROM TempUserMergeMapping m
WHERE s."AssignedEditorId" = m.duplicate_id;

UPDATE "MangaSeries" ms
SET "AuthorId" = m.survivor_id
FROM TempUserMergeMapping m
WHERE ms."AuthorId" = m.duplicate_id;

UPDATE "Chapters" c
SET "AssignedEditorId" = m.survivor_id
FROM TempUserMergeMapping m
WHERE c."AssignedEditorId" = m.duplicate_id;

UPDATE "StudioInvitations" si
SET "AssistantUserId" = m.survivor_id
FROM TempUserMergeMapping m
WHERE si."AssistantUserId" = m.duplicate_id;

UPDATE "StudioInvitations" si
SET "InviterMangakaId" = m.survivor_id
FROM TempUserMergeMapping m
WHERE si."InviterMangakaId" = m.duplicate_id;

UPDATE "MangakaAssistantCollaborations" mac
SET "MangakaId" = m.survivor_id
FROM TempUserMergeMapping m
WHERE mac."MangakaId" = m.duplicate_id;

UPDATE "MangakaAssistantCollaborations" mac
SET "AssistantId" = m.survivor_id
FROM TempUserMergeMapping m
WHERE mac."AssistantId" = m.duplicate_id;

UPDATE "Notifications" n
SET "ReceiverId" = m.survivor_id
FROM TempUserMergeMapping m
WHERE n."ReceiverId" = m.duplicate_id;

-- 3. Đánh dấu vô hiệu hóa hoặc đổi PersonalEmail tài khoản duplicate để không vi phạm unique constraint
UPDATE "Users" u
SET
    "PersonalEmail" = u."PersonalEmail" || '_merged_' || substring(u."Id"::text from 1 for 8),
    "AccountStatus" = 'Disabled',
    "IsDeleted" = true,
    "UpdatedAt" = NOW()
FROM TempUserMergeMapping m
WHERE u."Id" = m.duplicate_id;

-- 4. Chuẩn hóa PersonalEmail trên các tài khoản Survivor còn lại
UPDATE "Users"
SET "PersonalEmail" = LOWER(TRIM("PersonalEmail"))
WHERE "PersonalEmail" IS NOT NULL AND TRIM("PersonalEmail") <> '';

-- 5. Validation kiểm tra cuối cùng: Không còn duplicate PersonalEmail active
DO $$
DECLARE
    remaining_duplicates INT;
BEGIN
    SELECT COUNT(*) INTO remaining_duplicates
    FROM (
        SELECT LOWER(TRIM("PersonalEmail"))
        FROM "Users"
        WHERE "PersonalEmail" IS NOT NULL AND TRIM("PersonalEmail") <> '' AND "IsDeleted" = false
        GROUP BY LOWER(TRIM("PersonalEmail"))
        HAVING COUNT(*) > 1
    ) dup;

    IF remaining_duplicates > 0 THEN
        RAISE EXCEPTION 'Vẫn còn % nhóm PersonalEmail bị trùng sau khi gộp!', remaining_duplicates;
    ELSE
        RAISE NOTICE 'Đã dọn dẹp và chuẩn hóa PersonalEmail thành công. Transaction sẵn sàng COMMIT.';
    END IF;
END $$;

COMMIT TRANSACTION;
