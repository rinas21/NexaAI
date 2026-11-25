using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace NexaAI.Core.Domain.Entities;

public class EvaluationRun
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    public string Name { get; private set; } = string.Empty;

    [MaxLength(100)]
    public string? DatasetVersion { get; private set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument Config { get; private set; } = JsonDocument.Parse("{}");

    [Required]
    [MaxLength(50)]
    public string Status { get; private set; } = EvaluationStatus.Running;

    public DateTimeOffset StartedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; private set; }

    // Navigation properties
    public ICollection<EvaluationResult> Results { get; private set; } = new List<EvaluationResult>();

    // Private constructor for EF Core
    private EvaluationRun() { }

    public static EvaluationRun Create(string name, JsonDocument config, string? datasetVersion = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        return new EvaluationRun
        {
            Name = name.Trim(),
            Config = config,
            DatasetVersion = datasetVersion?.Trim(),
            Status = EvaluationStatus.Running
        };
    }

    public void Complete()
    {
        Status = EvaluationStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail()
    {
        Status = EvaluationStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}

public class EvaluationResult
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Required]
    public Guid RunId { get; private set; }

    [Required]
    [MaxLength(100)]
    public string QuestionId { get; private set; } = string.Empty;

    [Required]
    public string Question { get; private set; } = string.Empty;

    public string? ExpectedAnswer { get; private set; }

    public string? ActualAnswer { get; private set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument RetrievedChunks { get; private set; } = JsonDocument.Parse("[]");

    [Column(TypeName = "jsonb")]
    public JsonDocument Metrics { get; private set; } = JsonDocument.Parse("{}");

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public EvaluationRun? Run { get; private set; }

    // Private constructor for EF Core
    private EvaluationResult() { }

    public static EvaluationResult Create(
        Guid runId,
        string questionId,
        string question,
        string? expectedAnswer,
        string? actualAnswer,
        JsonDocument retrievedChunks,
        JsonDocument metrics)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("Run ID is required", nameof(runId));

        if (string.IsNullOrWhiteSpace(questionId))
            throw new ArgumentException("Question ID is required", nameof(questionId));

        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question is required", nameof(question));

        return new EvaluationResult
        {
            RunId = runId,
            QuestionId = questionId,
            Question = question,
            ExpectedAnswer = expectedAnswer,
            ActualAnswer = actualAnswer,
            RetrievedChunks = retrievedChunks,
            Metrics = metrics
        };
    }
}

public static class EvaluationStatus
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";

    public static bool IsValid(string status) =>
        status == Running || status == Completed || status == Failed;
}