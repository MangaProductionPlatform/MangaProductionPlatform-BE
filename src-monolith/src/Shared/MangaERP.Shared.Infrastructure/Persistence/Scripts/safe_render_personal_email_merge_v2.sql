-- ==============================================================================
-- SAFE PRODUCTION PERSONAL EMAIL MERGE & DRY-RUN SCRIPT (V2)
-- System: Manga Production & Publishing Platform
-- Target: Render PostgreSQL Database
-- Status: DRY-RUN AUDIT & TRANSACTIONAL MERGE STRATEGY
-- WARNING: DO NOT RUN DIRECTLY ON PRODUCTION WITHOUT PRE-PROD STAGING DRY-RUN
-- ==============================================================================

-- STEP 1: VERIFY MIGRATION HISTORY & ACTUAL TABLE SCHEMA
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId" DESC
LIMIT 10;

-- STEP 2: DYNAMICALLY DISCOVER ALL FOREIGN KEYS REFERENCING "Users"("Id")
SELECT
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name,
    tc.constraint_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
  ON tc.constraint_name = kcu.constraint_name
  AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
  ON ccu.constraint_name = tc.constraint_name
  AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND ccu.table_name = 'Users'
  AND ccu.column_name = 'Id';

-- STEP 3: DRY-RUN REPORT — DETECT DUPLICATE PersonalEmail
SELECT
    LOWER(TRIM("PersonalEmail")) AS normalized_email,
    COUNT(*) AS duplicate_count,
    ARRAY_AGG("Id") AS user_ids,
    ARRAY_AGG("Username") AS usernames,
    ARRAY_AGG("Role") AS roles,
    ARRAY_AGG("AccountStatus") AS statuses,
    ARRAY_AGG("CreatedAt") AS created_timestamps
FROM "Users"
WHERE "PersonalEmail" IS NOT NULL
  AND TRIM("PersonalEmail") <> ''
  AND "IsDeleted" = false
GROUP BY LOWER(TRIM("PersonalEmail"))
HAVING COUNT(*) > 1;

-- STEP 4: PERMANENT AUDIT TABLE CREATION (Transactional Safety)
CREATE TABLE IF NOT EXISTS "UserMergeAuditLogs" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "NormalizedEmail" VARCHAR(255) NOT NULL,
    "SurvivorUserId" UUID NOT NULL,
    "DuplicateUserId" UUID NOT NULL,
    "MergedAt" TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    "Reason" TEXT NOT NULL,
    "UpdatedReferencesJson" TEXT NULL
);

-- STEP 5: SAFE TRANSACTIONAL MERGE (EXECUTE ONLY IN STAGING / APPROVED ENVIRONMENT)
BEGIN TRANSACTION;

-- Create temporary mapping table for active session
CREATE TEMP TABLE TempUserMergeMapping ON COMMIT DROP AS
WITH RankedUsers AS (
    SELECT
        "Id",
        "Username",
        "Role",
        LOWER(TRIM("PersonalEmail")) AS norm_email,
        ROW_NUMBER() OVER (
            PARTITION BY LOWER(TRIM("PersonalEmail"))
            ORDER BY
                CASE WHEN "AccountStatus" = 'Active' THEN 1 ELSE 2 END,
                "CreatedAt" ASC
        ) AS rn
    FROM "Users"
    WHERE "PersonalEmail" IS NOT NULL
      AND TRIM("PersonalEmail") <> ''
      AND "IsDeleted" = false
),
Survivors AS (
    SELECT "Id" AS survivor_id, "Role" AS survivor_role, norm_email
    FROM RankedUsers WHERE rn = 1
),
Duplicates AS (
    SELECT "Id" AS duplicate_id, "Role" AS duplicate_role, norm_email
    FROM RankedUsers WHERE rn > 1
)
SELECT
    d.duplicate_id,
    s.survivor_id,
    d.norm_email
FROM Duplicates d
JOIN Survivors s ON d.norm_email = s.norm_email
WHERE d.duplicate_role = s.survivor_role;

-- Audit insertion for manual review cases (role mismatch)
INSERT INTO "UserMergeAuditLogs" ("NormalizedEmail", "SurvivorUserId", "DuplicateUserId", "Reason")
SELECT
    d.norm_email,
    s.survivor_id,
    d.duplicate_id,
    'ManualReviewRequired: Role conflict between survivor (' || s.survivor_role::text || ') and duplicate (' || d.duplicate_role::text || ')'
FROM (
    SELECT "Id" AS duplicate_id, "Role" AS duplicate_role, LOWER(TRIM("PersonalEmail")) AS norm_email
    FROM (
        SELECT "Id", "Role", "PersonalEmail", ROW_NUMBER() OVER (PARTITION BY LOWER(TRIM("PersonalEmail")) ORDER BY CASE WHEN "AccountStatus" = 'Active' THEN 1 ELSE 2 END, "CreatedAt" ASC) AS rn
        FROM "Users" WHERE "PersonalEmail" IS NOT NULL AND TRIM("PersonalEmail") <> '' AND "IsDeleted" = false
    ) u WHERE rn > 1
) d
JOIN (
    SELECT "Id" AS survivor_id, "Role" AS survivor_role, LOWER(TRIM("PersonalEmail")) AS norm_email
    FROM (
        SELECT "Id", "Role", "PersonalEmail", ROW_NUMBER() OVER (PARTITION BY LOWER(TRIM("PersonalEmail")) ORDER BY CASE WHEN "AccountStatus" = 'Active' THEN 1 ELSE 2 END, "CreatedAt" ASC) AS rn
        FROM "Users" WHERE "PersonalEmail" IS NOT NULL AND TRIM("PersonalEmail") <> '' AND "IsDeleted" = false
    ) u WHERE rn = 1
) s ON d.norm_email = s.norm_email
WHERE d.duplicate_role <> s.survivor_role;

-- Audit insertion for auto-merged safe cases
INSERT INTO "UserMergeAuditLogs" ("NormalizedEmail", "SurvivorUserId", "DuplicateUserId", "Reason")
SELECT
    norm_email,
    survivor_id,
    duplicate_id,
    'Safe automated duplicate PersonalEmail consolidation for Render PostgreSQL EF Core migration'
FROM TempUserMergeMapping;

-- Safely reassign Foreign Key references
UPDATE "SeriesSubmissions" s SET "SubmitterId" = m.survivor_id FROM TempUserMergeMapping m WHERE s."SubmitterId" = m.duplicate_id;
UPDATE "SeriesSubmissions" s SET "AssignedEditorId" = m.survivor_id FROM TempUserMergeMapping m WHERE s."AssignedEditorId" = m.duplicate_id;
UPDATE "MangaSeries" ms SET "AuthorId" = m.survivor_id FROM TempUserMergeMapping m WHERE ms."AuthorId" = m.duplicate_id;
UPDATE "Chapters" c SET "AssignedEditorId" = m.survivor_id FROM TempUserMergeMapping m WHERE c."AssignedEditorId" = m.duplicate_id;
UPDATE "StudioInvitations" si SET "AssistantUserId" = m.survivor_id FROM TempUserMergeMapping m WHERE si."AssistantUserId" = m.duplicate_id;
UPDATE "StudioInvitations" si SET "InviterMangakaId" = m.survivor_id FROM TempUserMergeMapping m WHERE si."InviterMangakaId" = m.duplicate_id;
UPDATE "MangakaAssistantCollaborations" mac SET "MangakaId" = m.survivor_id FROM TempUserMergeMapping m WHERE mac."MangakaId" = m.duplicate_id;
UPDATE "MangakaAssistantCollaborations" mac SET "AssistantId" = m.survivor_id FROM TempUserMergeMapping m WHERE mac."AssistantId" = m.duplicate_id;
UPDATE "Notifications" n SET "ReceiverId" = m.survivor_id FROM TempUserMergeMapping m WHERE n."ReceiverId" = m.duplicate_id;

-- Handle UserRoles composite unique key conflicts (delete duplicate role associations if survivor already has that role)
DELETE FROM "UserRoles" ur
USING TempUserMergeMapping m
WHERE ur."UserId" = m.duplicate_id
  AND EXISTS (
      SELECT 1 FROM "UserRoles" s
      WHERE s."UserId" = m.survivor_id AND s."RoleId" = ur."RoleId"
  );
UPDATE "UserRoles" ur SET "UserId" = m.survivor_id FROM TempUserMergeMapping m WHERE ur."UserId" = m.duplicate_id;

-- Update ManagingTantouId self-references avoiding self-loop
UPDATE "Users" u SET "ManagingTantouId" = m.survivor_id FROM TempUserMergeMapping m WHERE u."ManagingTantouId" = m.duplicate_id AND u."Id" <> m.survivor_id;

-- Soft-delete duplicate users and suffix email to avoid unique index violation
UPDATE "Users" u
SET
    "PersonalEmail" = u."PersonalEmail" || '_merged_' || substring(u."Id"::text from 1 for 8),
    "AccountStatus" = 'Disabled',
    "IsDeleted" = true
FROM TempUserMergeMapping m
WHERE u."Id" = m.duplicate_id;

-- Normalize survivor PersonalEmail entries
UPDATE "Users"
SET "PersonalEmail" = LOWER(TRIM("PersonalEmail"))
WHERE "PersonalEmail" IS NOT NULL AND TRIM("PersonalEmail") <> '' AND "IsDeleted" = false;

-- STEP 6: PRE-COMMIT VALIDATION
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
        RAISE EXCEPTION 'Pre-commit validation failed: % duplicate PersonalEmail groups remain!', remaining_duplicates;
    ELSE
        RAISE NOTICE 'Pre-commit validation PASSED: No duplicate PersonalEmail entries found. Safe to COMMIT.';
    END IF;
END $$;

COMMIT TRANSACTION;
