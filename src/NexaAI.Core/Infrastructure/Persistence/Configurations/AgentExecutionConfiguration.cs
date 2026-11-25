using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaAI.Core.Domain.Entities;

namespace NexaAI.Core.Infrastructure.Persistence.Configurations;

public class AgentExecutionConfiguration : IEntityTypeConfiguration<AgentExecution>
{
    public void Configure(EntityTypeBuilder<AgentExecution> builder)
    {
        builder.ToTable("agent_executions");

        builder.HasKey(ae => ae.Id);

        builder.Property(ae => ae.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(ae => ae.UserId);

        builder.Property(ae => ae.AgentName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ae => ae.Input)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(ae => ae.Output)
            .HasColumnType("jsonb");

        builder.Property(ae => ae.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(ExecutionStatus.Running);

        builder.Property(ae => ae.ErrorMessage)
            .HasColumnType("text");

        builder.Property(ae => ae.ExecutionTimeMs);

        builder.Property(ae => ae.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        builder.Property(ae => ae.CompletedAt);

        builder.HasIndex(ae => ae.UserId)
            .HasDatabaseName("ix_agent_executions_user_id");

        builder.HasIndex(ae => ae.Status)
            .HasDatabaseName("ix_agent_executions_status");

        builder.HasIndex(ae => ae.CreatedAt)
            .HasDatabaseName("ix_agent_executions_created_at");

        builder.HasMany(ae => ae.ToolApprovals)
            .WithOne(ta => ta.Execution!)
            .HasForeignKey(ta => ta.ExecutionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ToolApprovalConfiguration : IEntityTypeConfiguration<ToolApproval>
{
    public void Configure(EntityTypeBuilder<ToolApproval> builder)
    {
        builder.ToTable("tool_approvals");

        builder.HasKey(ta => ta.Id);

        builder.Property(ta => ta.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(ta => ta.ExecutionId)
            .IsRequired();

        builder.Property(ta => ta.ToolName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ta => ta.Parameters)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(ta => ta.RequestedBy);

        builder.Property(ta => ta.ApprovedBy);

        builder.Property(ta => ta.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(ApprovalStatus.Pending);

        builder.Property(ta => ta.Reason)
            .HasColumnType("text");

        builder.Property(ta => ta.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        builder.Property(ta => ta.DecidedAt);

        builder.HasIndex(ta => ta.ExecutionId)
            .HasDatabaseName("ix_tool_approvals_execution_id");

        builder.HasIndex(ta => ta.Status)
            .HasDatabaseName("ix_tool_approvals_status");
    }
}