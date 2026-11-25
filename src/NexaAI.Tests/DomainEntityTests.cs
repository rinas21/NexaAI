using System.Text.Json;
using FluentAssertions;
using NexaAI.Core.Domain.Entities;
using Xunit;

namespace NexaAI.Tests;

/// <summary>
/// Unit tests for domain entity invariants and permission logic.
/// </summary>
public class DomainEntityTests
{
    [Fact]
    public void User_Create_ShouldNormalizeEmailAndAssignUserRole()
    {
        var user = User.Create("  Jane.Doe@Example.COM ", "hash", "Jane Doe");

        user.Email.Should().Be("jane.doe@example.com");
        user.Role.Should().Be(UserRole.User);
        user.IsActive.Should().BeTrue();
        user.FullName.Should().Be("Jane Doe");
    }

    [Fact]
    public void User_SetRole_ShouldRejectInvalidRole()
    {
        var user = User.Create("a@b.com", "hash");

        var act = () => user.SetRole("SuperAdmin");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void User_IsAdmin_ShouldReturnTrueForAdminRole()
    {
        var admin = User.Create("admin@b.com", "hash", role: UserRole.Admin);

        admin.IsAdmin().Should().BeTrue();
    }

    [Fact]
    public void Document_Create_ShouldSetProcessingStatus()
    {
        var doc = Document.Create("Policy", "policy.pdf", "application/pdf", 1024, "/uploads/policy.pdf", Guid.NewGuid());

        doc.Status.Should().Be(DocumentStatus.Processing);
        doc.AccessLevel.Should().Be(DocumentAccessLevel.Private);
    }

    [Fact]
    public void Document_CanAccess_ShouldDenyPrivateDocumentToOtherUsers()
    {
        var owner = User.Create("owner@b.com", "hash");
        var other = User.Create("other@b.com", "hash");
        var doc = Document.Create("Private", "p.pdf", "application/pdf", 100, "/p.pdf", owner.Id);

        doc.CanAccess(owner).Should().BeTrue();
        doc.CanAccess(other).Should().BeFalse();
    }

    [Fact]
    public void Document_CanAccess_ShouldAllowAdminToAccessPrivateDocument()
    {
        var owner = User.Create("owner@b.com", "hash");
        var admin = User.Create("admin@b.com", "hash", role: UserRole.Admin);
        var doc = Document.Create("Private", "p.pdf", "application/pdf", 100, "/p.pdf", owner.Id);

        doc.CanAccess(admin).Should().BeTrue();
    }

    [Fact]
    public void Document_CanAccess_ShouldAllowAnonymousAccessToPublicDocument()
    {
        var doc = Document.Create("Public", "p.pdf", "application/pdf", 100, "/p.pdf", accessLevel: DocumentAccessLevel.Public);

        doc.CanAccess(null).Should().BeTrue();
    }

    [Fact]
    public void DocumentChunk_Create_ShouldRejectNegativeChunkIndex()
    {
        var act = () => DocumentChunk.Create(Guid.NewGuid(), -1, "text", 10);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RefreshToken_IsValid_ShouldReturnFalseWhenRevoked()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", DateTimeOffset.UtcNow.AddDays(1));

        token.IsValid.Should().BeTrue();
        token.Revoke();
        token.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AgentExecution_Complete_ShouldSetStatusAndDuration()
    {
        var execution = AgentExecution.Create("agent", JsonDocument.Parse("{\"input\":1}"));

        execution.Complete(JsonDocument.Parse("{\"output\":1}"), 250);

        execution.Status.Should().Be(ExecutionStatus.Completed);
        execution.ExecutionTimeMs.Should().Be(250);
        execution.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void ToolApproval_Approve_ShouldRecordDecider()
    {
        var approval = ToolApproval.Create(Guid.NewGuid(), "database_query", JsonDocument.Parse("{}"));
        var adminId = Guid.NewGuid();

        approval.Approve(adminId, "Verified");

        approval.Status.Should().Be(ApprovalStatus.Approved);
        approval.ApprovedBy.Should().Be(adminId);
        approval.Reason.Should().Be("Verified");
    }
}