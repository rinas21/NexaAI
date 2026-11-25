-- Idempotent seed script for an existing NexaAI database.
-- Usage: psql "$DATABASE_URL" -f database/seed/seed.sql
-- The same bootstrap accounts are created by database/init/001_init.sql on a fresh volume.

INSERT INTO users (id, email, password_hash, full_name, role, is_active)
VALUES
    ('11111111-1111-4111-8111-111111111111', 'admin@nexai.local',
     '$2a$10$auQTxdtpXhDXYZ8p2ETuEOwMdj0V6IMrhGB/bSY3gAb3ogiSl//vK',
     'Nexa Administrator', 'Admin', TRUE),
    ('22222222-2222-4222-8222-222222222222', 'user@nexai.local',
     '$2a$10$TgmXItdeEGwhSh1XnTzm7ORSu/5CKdLeMLAXyGKeLy/goXf7SoRXy',
     'Nexa User', 'User', TRUE)
ON CONFLICT (id) DO UPDATE
SET email = EXCLUDED.email,
    password_hash = EXCLUDED.password_hash,
    full_name = EXCLUDED.full_name,
    role = EXCLUDED.role,
    is_active = TRUE,
    updated_at = CURRENT_TIMESTAMP AT TIME ZONE 'UTC';
