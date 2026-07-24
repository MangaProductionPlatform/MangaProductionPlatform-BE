-- ==============================================================================
-- TEST ONLY — DO NOT RUN ON PRODUCTION.
-- Synthetic test data generator for duplicate PersonalEmail unit/integration testing.
-- Contains dummy emails (@mg.com, @manga.com) and mock password hashes only.
-- ==============================================================================
INSERT INTO "Users" ("Id", "Username", "Email", "PersonalEmail", "Role", "AccountStatus", "IsDeleted", "CreatedAt", "PasswordHash")
VALUES
    -- Safe pair (both Mangaka)
    (gen_random_uuid(), 'user_safe_1@mg.com', 'user_safe_1@mg.com', 'safe_dup@manga.com', 'Mangaka', 'Active', false, NOW() - INTERVAL '3 days', 'hash1'),
    (gen_random_uuid(), 'user_safe_2@mg.com', 'user_safe_2@mg.com', ' SAFE_DUP@manga.com ', 'Mangaka', 'Active', false, NOW() - INTERVAL '2 days', 'hash2'),
    -- Conflicting pair (Mangaka vs EditorInChief)
    (gen_random_uuid(), 'user_conflict_1@mg.com', 'user_conflict_1@mg.com', 'conflict_dup@manga.com', 'Mangaka', 'Active', false, NOW() - INTERVAL '3 days', 'hash3'),
    (gen_random_uuid(), 'user_conflict_2@mg.com', 'user_conflict_2@mg.com', ' CONFLICT_DUP@manga.com ', 'EditorInChief', 'Active', false, NOW() - INTERVAL '2 days', 'hash4');
