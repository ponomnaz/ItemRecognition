BEGIN;

-- UUID + CITEXT
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS citext;

-- Enums
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'ai_stage') THEN
CREATE TYPE ai_stage AS ENUM ('MAIN_OBJECTS', 'MATERIALS');
END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'request_status') THEN
CREATE TYPE request_status AS ENUM (
      'CREATED',
      'MAIN_DETECTED',
      'CONFIRMED',
      'MATERIALS_DETECTED',
      'FAILED'
    );
END IF;
END $$;

-- Requests
CREATE TABLE IF NOT EXISTS recognition_requests (
                                                    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at        timestamptz NOT NULL DEFAULT now(),
    updated_at        timestamptz NOT NULL DEFAULT now(),

    status            request_status NOT NULL DEFAULT 'CREATED',

    -- required for task
    image_url         text NOT NULL,

    -- for anonymized export + dedup
    image_hash        char(64),

    -- where downloaded image is stored (local path / s3 key / blob key)
    image_storage_key text,

    CONSTRAINT ck_image_url_nonempty CHECK (length(trim(image_url)) > 0),
    CONSTRAINT ck_image_hash_format CHECK (
                                              image_hash IS NULL OR image_hash ~ '^[0-9a-fA-F]{64}$'
                                          )
    );

CREATE INDEX IF NOT EXISTS ix_requests_created_at ON recognition_requests (created_at DESC);
CREATE INDEX IF NOT EXISTS ix_requests_status ON recognition_requests (status);
CREATE INDEX IF NOT EXISTS ix_requests_image_hash ON recognition_requests (image_hash);

-- AI calls log (everything needed for prompt iteration + audit)
CREATE TABLE IF NOT EXISTS ai_calls (
                                        id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    request_id       uuid NOT NULL REFERENCES recognition_requests(id) ON DELETE CASCADE,

    stage           ai_stage NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),

    provider        text NOT NULL,   -- e.g. gigachat/openai/azure-openai/local
    model           text NOT NULL,   -- model name
    prompt_version  text NOT NULL,   -- e.g. main.v1 / mat.v1
    prompt_text     text NOT NULL,   -- exact prompt used

-- payload you sent to AI (e.g. confirmed items list on MATERIALS stage)
    request_payload jsonb NOT NULL DEFAULT '{}'::jsonb,

    -- raw AI response (JSON only)
    response_json   jsonb NOT NULL,

    is_success      boolean NOT NULL DEFAULT true,
    error_message   text,

    duration_ms     integer NOT NULL DEFAULT 0,

    CONSTRAINT ck_provider_nonempty CHECK (length(trim(provider)) > 0),
    CONSTRAINT ck_model_nonempty CHECK (length(trim(model)) > 0),
    CONSTRAINT ck_prompt_version_nonempty CHECK (length(trim(prompt_version)) > 0),
    CONSTRAINT ck_prompt_text_nonempty CHECK (length(trim(prompt_text)) > 0),
    CONSTRAINT ck_duration_ms CHECK (duration_ms >= 0)
    );

CREATE INDEX IF NOT EXISTS ix_ai_calls_req_stage_time ON ai_calls (request_id, stage, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_ai_calls_stage_time ON ai_calls (stage, created_at DESC);

-- Predicted objects (step 1 output)
CREATE TABLE IF NOT EXISTS predicted_objects (
                                                 id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    request_id   uuid NOT NULL REFERENCES recognition_requests(id) ON DELETE CASCADE,
    ai_call_id   uuid NOT NULL REFERENCES ai_calls(id) ON DELETE CASCADE,

    created_at  timestamptz NOT NULL DEFAULT now(),

    name        text NOT NULL,
    is_primary  boolean NOT NULL DEFAULT false,
    confidence  real,
    rank        integer NOT NULL,

    CONSTRAINT ck_pred_name_nonempty CHECK (length(trim(name)) > 0),
    CONSTRAINT ck_pred_rank CHECK (rank >= 1),
    CONSTRAINT ck_pred_confidence CHECK (confidence IS NULL OR (confidence >= 0.0 AND confidence <= 1.0))
    );

-- stable ordering per request
CREATE UNIQUE INDEX IF NOT EXISTS ux_predicted_req_rank ON predicted_objects (request_id, rank);
CREATE INDEX IF NOT EXISTS ix_predicted_req_primary ON predicted_objects (request_id, is_primary);
CREATE INDEX IF NOT EXISTS ix_predicted_name_lower ON predicted_objects ((lower(name)));

-- Confirmed objects (user input, step 2 input)
CREATE TABLE IF NOT EXISTS confirmed_objects (
                                                 id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    request_id   uuid NOT NULL REFERENCES recognition_requests(id) ON DELETE CASCADE,
    created_at  timestamptz NOT NULL DEFAULT now(),
    name        text NOT NULL,

    CONSTRAINT ck_conf_name_nonempty CHECK (length(trim(name)) > 0)
    );

-- no duplicates (case-insensitive) per request
CREATE UNIQUE INDEX IF NOT EXISTS ux_confirmed_req_name_lower
    ON confirmed_objects (request_id, (lower(name)));

-- Materials dictionary
CREATE TABLE IF NOT EXISTS materials (
                                         id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at  timestamptz NOT NULL DEFAULT now(),
    name        citext NOT NULL,

    CONSTRAINT ck_material_name_nonempty CHECK (length(trim(name::text)) > 0)
    );

CREATE UNIQUE INDEX IF NOT EXISTS ux_materials_name ON materials (name);

-- Many-to-many: confirmed object -> materials
CREATE TABLE IF NOT EXISTS confirmed_object_materials (
                                                          confirmed_object_id  uuid NOT NULL REFERENCES confirmed_objects(id) ON DELETE CASCADE,
    material_id          uuid NOT NULL REFERENCES materials(id) ON DELETE RESTRICT,
    created_at           timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (confirmed_object_id, material_id)
    );

CREATE INDEX IF NOT EXISTS ix_com_material_id ON confirmed_object_materials (material_id);

-- updated_at trigger
CREATE OR REPLACE FUNCTION set_updated_at()
RETURNS trigger AS $$
BEGIN
  NEW.updated_at = now();
RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_trigger WHERE tgname = 'trg_requests_set_updated_at'
  ) THEN
CREATE TRIGGER trg_requests_set_updated_at
    BEFORE UPDATE ON recognition_requests
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
END IF;
END $$;

COMMIT;
