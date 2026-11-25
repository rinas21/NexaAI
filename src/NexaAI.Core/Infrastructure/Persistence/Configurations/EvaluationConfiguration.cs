using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaAI.Core.Domain.Entities;

namespace NexaAI.Core.Infrastructure.Persistence.Configurations;

public class EvaluationRunConfiguration : IEntityTypeConfiguration<EvaluationRun>
{
    public void Configure(EntityTypeBuilder<EvaluationRun> builder)
    {
        builder.ToTable("evaluation_runs");

        builder.HasKey(er => er.Id);

        builder.Property(er => er.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(er => er.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(er => er.DatasetVersion)
            .HasMaxLength(100);

        builder.Property(er => er.Config)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(er => er.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(EvaluationStatus.Running);

        builder.Property(er => er.StartedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        builder.Property(er => er.CompletedAt);

        builder.HasIndex(er => er.Status)
            .HasDatabaseName("ix_evaluation_runs_status");

        builder.HasMany(er => er.Results)
            .WithOne(r => r.Run!)
            .HasForeignKey(r => r.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EvaluationResultConfiguration : IEntityTypeConfiguration<EvaluationResult>
{
    public void Configure(EntityTypeBuilder<EvaluationResult> builder)
    {
        builder.ToTable("evaluation_results");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.RunId)
            .IsRequired();

        builder.Property(r => r.QuestionId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Question)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(r => r.ExpectedAnswer)
            .HasColumnType("text");

        builder.Property(r => r.ActualAnswer)
            .HasColumnType("text");

        builder.Property(r => r.RetrievedChunks)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(r => r.Metrics)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        builder.HasIndex(r => r.RunId)
            .HasDatabaseName("ix_evaluation_results_run_id");
    }
}