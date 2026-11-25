using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace NexaAI.Core.Domain.Entities;

public class AgentExecution
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid? UserId { get; private set; }

    [Required]
    [MaxLength(100)]
    public string AgentName { get; private set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public JsonDocument Input { get; private set; } = JsonDocument.Parse("{}");

    [Column(TypeName = "jsonb")]
    public JsonDocument? Output { get; private set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; private set; } = ExecutionStatus.Running;

    public string? ErrorMessage { get; private set; }

    public int? ExecutionTimeMs { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; private set; }

    // Navigation properties
    public User? User { get; private set; }
    public ICollection<ToolApproval> ToolApprovals { get; private set; } = new List<ToolApproval>();

    // Private constructor for EF Core
    private AgentExecution() { }

    public static AgentExecution Create(
        string agentName,
        JsonDocument input,
        Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Agent name is required", nameof(agentName));

        return new AgentExecution
        {
            AgentName = agentName,
            Input = input,
            UserId = userId,
            Status = ExecutionStatus.Running
        };
    }

    public void Complete(JsonDocument output, int executionTimeMs)
    {
        Output = output;
        Status = ExecutionStatus.Completed;
        ExecutionTimeMs = executionTimeMs;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorMessage, int executionTimeMs)
    {
        ErrorMessage = errorMessage;
        Status = ExecutionStatus.Failed;
        ExecutionTimeMs = executionTimeMs;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void SetPendingApproval()
    {
        Status = ExecutionStatus.PendingApproval;
    }

    public void SetRunning()
    {
        Status = ExecutionStatus.Running;
    }
}

public class ToolApproval
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Required]
    public Guid ExecutionId { get; private set; }

    [Required]
    [MaxLength(100)]
    public string ToolName { get; private set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public JsonDocument Parameters { get; private set; } = JsonDocument.Parse("{}");

    public Guid? RequestedBy { get; private set; }

    public Guid? ApprovedBy { get; private set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; private set; } = ApprovalStatus.Pending;

    public string? Reason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DecidedAt { get; private set; }

    // Navigation properties
    public AgentExecution? Execution { get; private set; }
    public User? RequestedByUser { get; private set; }
    public User? ApprovedByUser { get; private set; }

    // Private constructor for EF Core
    private ToolApproval() { }

    public static ToolApproval Create(
        Guid executionId,
        string toolName,
        JsonDocument parameters,
        Guid? requestedBy = null)
    {
        if (executionId == Guid.Empty)
            throw new ArgumentException("Execution ID is required", nameof(executionId));

        if (string.IsNullOrWhiteSpace(toolName))
            throw new ArgumentException("Tool name is required", nameof(toolName));

        return new ToolApproval
        {
            ExecutionId = executionId,
            ToolName = toolName,
            Parameters = parameters,
            RequestedBy = requestedBy,
            Status = ApprovalStatus.Pending
        };
    }

    public void Approve(Guid approvedBy, string? reason = null)
    {
        ApprovedBy = approvedBy;
        Status = ApprovalStatus.Approved;
        Reason = reason;
        DecidedAt = DateTimeOffset.UtcNow;
    }

    public void Reject(Guid rejectedBy, string? reason = null)
    {
        ApprovedBy = rejectedBy;
        Status = ApprovalStatus.Rejected;
        Reason = reason;
        DecidedAt = DateTimeOffset.UtcNow;
    }
}

public static class ExecutionStatus
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string PendingApproval = "PendingApproval";

    public static bool IsValid(string status) =>
        status == Running || status == Completed || status == Failed || status == PendingApproval;
}

public static class ApprovalStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static bool IsValid(string status) =>
        status == Pending || status == Approved || status == Rejected;
}