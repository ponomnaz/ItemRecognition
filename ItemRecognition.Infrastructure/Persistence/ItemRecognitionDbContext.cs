using ItemRecognition.Domain.Entities;
using ItemRecognition.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItemRecognition.Infrastructure.Persistence;

public sealed class ItemRecognitionDbContext(DbContextOptions<ItemRecognitionDbContext> options) : DbContext(options)
{
    public DbSet<RecognitionRequest> RecognitionRequests => Set<RecognitionRequest>();
    public DbSet<AiCall> AiCalls => Set<AiCall>();
    public DbSet<PredictedObject> PredictedObjects => Set<PredictedObject>();
    public DbSet<ConfirmedObject> ConfirmedObjects => Set<ConfirmedObject>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<ConfirmedObjectMaterial> ConfirmedObjectMaterials => Set<ConfirmedObjectMaterial>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.HasPostgresEnum<AiStage>("ai_stage");
        modelBuilder.HasPostgresEnum<RequestStatus>("request_status");

        ConfigureRecognitionRequests(modelBuilder.Entity<RecognitionRequest>());
        ConfigureAiCalls(modelBuilder.Entity<AiCall>());
        ConfigurePredictedObjects(modelBuilder.Entity<PredictedObject>());
        ConfigureConfirmedObjects(modelBuilder.Entity<ConfirmedObject>());
        ConfigureMaterials(modelBuilder.Entity<Material>());
        ConfigureConfirmedObjectMaterials(modelBuilder.Entity<ConfirmedObjectMaterial>());
    }

    private static void ConfigureRecognitionRequests(EntityTypeBuilder<RecognitionRequest> entity)
    {
        entity.ToTable(
            "recognition_requests",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_image_url_nonempty", "length(trim(image_url)) > 0");
                tableBuilder.HasCheckConstraint(
                    "ck_image_hash_format",
                    "image_hash IS NULL OR image_hash ~ '^[0-9a-fA-F]{64}$'");
                tableBuilder.HasCheckConstraint(
                    "ck_image_hash_required",
                    "status IN ('CREATED', 'FAILED') OR image_hash IS NOT NULL");
            });
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        entity.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        entity.Property(e => e.Status)
            .HasColumnName("status")
            .HasColumnType("request_status")
            .HasDefaultValueSql("'CREATED'");

        entity.Property(e => e.ImageUrl)
            .HasColumnName("image_url")
            .IsRequired();

        entity.Property(e => e.ImageHash)
            .HasColumnName("image_hash")
            .HasColumnType("char(64)")
            .HasMaxLength(64);

        entity.Property(e => e.ImageStorageKey)
            .HasColumnName("image_storage_key");

        entity.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_requests_created_at");

        entity.HasIndex(e => e.Status)
            .HasDatabaseName("ix_requests_status");

        entity.HasIndex(e => e.ImageHash)
            .HasDatabaseName("ix_requests_image_hash");
    }

    private static void ConfigureAiCalls(EntityTypeBuilder<AiCall> entity)
    {
        entity.ToTable(
            "ai_calls",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_provider_nonempty", "length(trim(provider)) > 0");
                tableBuilder.HasCheckConstraint("ck_model_nonempty", "length(trim(model)) > 0");
                tableBuilder.HasCheckConstraint("ck_prompt_version_nonempty", "length(trim(prompt_version)) > 0");
                tableBuilder.HasCheckConstraint("ck_prompt_text_nonempty", "length(trim(prompt_text)) > 0");
                tableBuilder.HasCheckConstraint("ck_duration_ms", "duration_ms >= 0");
            });
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.RequestId)
            .HasColumnName("request_id");

        entity.HasOne(e => e.Request)
            .WithMany(r => r.AiCalls)
            .HasForeignKey(e => e.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(e => e.Stage)
            .HasColumnName("stage")
            .HasColumnType("ai_stage");

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        entity.Property(e => e.Provider)
            .HasColumnName("provider")
            .IsRequired();

        entity.Property(e => e.Model)
            .HasColumnName("model")
            .IsRequired();

        entity.Property(e => e.PromptVersion)
            .HasColumnName("prompt_version")
            .IsRequired();

        entity.Property(e => e.PromptText)
            .HasColumnName("prompt_text")
            .IsRequired();

        entity.Property(e => e.RequestPayload)
            .HasColumnName("request_payload")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        entity.Property(e => e.ResponseJson)
            .HasColumnName("response_json")
            .HasColumnType("jsonb")
            .IsRequired();

        entity.Property(e => e.IsSuccess)
            .HasColumnName("is_success")
            .HasDefaultValue(true);

        entity.Property(e => e.ErrorMessage)
            .HasColumnName("error_message");

        entity.Property(e => e.DurationMs)
            .HasColumnName("duration_ms")
            .HasDefaultValue(0);

        entity.HasIndex(e => new { e.RequestId, e.Stage, e.CreatedAt })
            .HasDatabaseName("ix_ai_calls_req_stage_time");

        entity.HasIndex(e => new { e.Stage, e.CreatedAt })
            .HasDatabaseName("ix_ai_calls_stage_time");
    }

    private static void ConfigurePredictedObjects(EntityTypeBuilder<PredictedObject> entity)
    {
        entity.ToTable(
            "predicted_objects",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_pred_name_nonempty", "length(trim(name)) > 0");
                tableBuilder.HasCheckConstraint("ck_pred_rank", "rank >= 1");
                tableBuilder.HasCheckConstraint(
                    "ck_pred_confidence",
                    "confidence IS NULL OR (confidence >= 0.0 AND confidence <= 1.0)");
            });
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.RequestId)
            .HasColumnName("request_id");

        entity.Property(e => e.AiCallId)
            .HasColumnName("ai_call_id");

        entity.HasOne(e => e.Request)
            .WithMany(r => r.PredictedObjects)
            .HasForeignKey(e => e.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.AiCall)
            .WithMany(a => a.PredictedObjects)
            .HasForeignKey(e => e.AiCallId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        entity.Property(e => e.Name)
            .HasColumnName("name")
            .IsRequired();

        entity.Property(e => e.IsPrimary)
            .HasColumnName("is_primary")
            .HasDefaultValue(false);

        entity.Property(e => e.Confidence)
            .HasColumnName("confidence");

        entity.Property(e => e.Rank)
            .HasColumnName("rank")
            .IsRequired();

        entity.HasIndex(e => new { e.AiCallId, e.Rank })
            .IsUnique()
            .HasDatabaseName("ux_predicted_call_rank");

        entity.HasIndex(e => new { e.RequestId, e.IsPrimary })
            .HasDatabaseName("ix_predicted_req_primary");

        // DB uses a functional index on lower(name); kept as metadata reference.
        entity.HasIndex(e => e.Name)
            .HasDatabaseName("ix_predicted_name_lower");
    }

    private static void ConfigureConfirmedObjects(EntityTypeBuilder<ConfirmedObject> entity)
    {
        entity.ToTable(
            "confirmed_objects",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_conf_name_nonempty", "length(trim(name)) > 0");
            });
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.RequestId)
            .HasColumnName("request_id");

        entity.Property(e => e.AiCallId)
            .HasColumnName("ai_call_id");

        entity.HasOne(e => e.Request)
            .WithMany(r => r.ConfirmedObjects)
            .HasForeignKey(e => e.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.AiCall)
            .WithMany(a => a.ConfirmedObjects)
            .HasForeignKey(e => e.AiCallId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        entity.Property(e => e.Name)
            .HasColumnName("name")
            .IsRequired();

        // DB uses a case-insensitive unique index via lower(name); approximated here.
        entity.HasIndex(e => new { e.RequestId, e.Name })
            .IsUnique()
            .HasDatabaseName("ux_confirmed_req_name_lower");

        entity.HasIndex(e => e.AiCallId)
            .HasDatabaseName("ix_confirmed_ai_call_id");
    }

    private static void ConfigureMaterials(EntityTypeBuilder<Material> entity)
    {
        entity.ToTable(
            "materials",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("ck_material_name_nonempty", "length(trim(name::text)) > 0");
            });
        entity.HasKey(e => e.Id);

        entity.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        entity.Property(e => e.Name)
            .HasColumnName("name")
            .HasColumnType("citext")
            .IsRequired();

        entity.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName("ux_materials_name");
    }

    private static void ConfigureConfirmedObjectMaterials(EntityTypeBuilder<ConfirmedObjectMaterial> entity)
    {
        entity.ToTable("confirmed_object_materials");
        entity.HasKey(e => new { e.ConfirmedObjectId, e.MaterialId });

        entity.Property(e => e.ConfirmedObjectId)
            .HasColumnName("confirmed_object_id");

        entity.Property(e => e.MaterialId)
            .HasColumnName("material_id");

        entity.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        entity.HasOne(e => e.ConfirmedObject)
            .WithMany(o => o.Materials)
            .HasForeignKey(e => e.ConfirmedObjectId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Material)
            .WithMany(m => m.ConfirmedObjectMaterials)
            .HasForeignKey(e => e.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.MaterialId)
            .HasDatabaseName("ix_com_material_id");
    }
}
