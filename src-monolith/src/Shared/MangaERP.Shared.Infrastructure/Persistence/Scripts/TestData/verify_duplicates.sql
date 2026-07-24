-- ==============================================================================
-- TEST ONLY — DO NOT RUN ON PRODUCTION.
-- Read-only verification query to count active duplicate PersonalEmail entries.
-- ==============================================================================
SELECT LOWER(TRIM("PersonalEmail")) as email, COUNT(*)
FROM "Users"
WHERE "IsDeleted" = false AND "PersonalEmail" IS NOT NULL AND TRIM("PersonalEmail") <> ''
GROUP BY LOWER(TRIM("PersonalEmail"))
HAVING COUNT(*) > 1;
