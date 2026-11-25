using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaAI.Core.Domain.Entities;

namespace NexaAI.Core.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.UserId)
            .IsRequired();

        builder.Property(c => c.Title)
            .HasMaxLength(500);

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        builder.HasIndex(c => c.UserId)
            .HasDatabaseName("ix_conversations_user_id");

        builder.HasIndex(c => c.UpdatedAt)
            .HasDatabaseName("ix_conversations_updated_at");

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation!)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(m => m.ConversationId)
            .IsRequired();

        builder.Property(m => m.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(m => m.Citations)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(m => m.ToolCalls)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.Property(m => m.Metadata)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        builder.HasIndex(m => m.ConversationId)
            .HasDatabaseName("ix_messages_conversation_id");

        builder.HasIndex(m => m.CreatedAt)
            .HasDatabaseName("ix_messages_created_at");
    }
}