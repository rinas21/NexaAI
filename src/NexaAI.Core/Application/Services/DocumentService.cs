using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexaAI.Core.Application.Common;
using NexaAI.Core.Application.Configuration;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;
using NexaAI.Core.Domain.Entities;
using NexaAI.Core.Infrastructure.Persistence;
using pgvector;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace NexaAI.Core.Application.Services;

/// <summary>
/// Document ingestion (PDF/DOCX/text), chunking, embedding persistence and permission-aware search.
/// </summary>
public sealed class DocumentService(
    AppDbContext db,
    IAIServiceClient aiClient,
    IOptions<StorageOptions> storageOptions,
    IOptions<ChunkingOptions> chunkingOptions,
    ILogger<DocumentService> logger) : IDocumentService
{
    private static readonly Dictionary<string, string> ExtensionContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown"
    };

    /// <inheritdoc />
    public async Task<DocumentResponse> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        DocumentUploadRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!ExtensionContentTypes.TryGetValue(extension, out var normalizedContentType))
        {
            throw new ArgumentException($"Unsupported file type '{extension}'. Allowed: {string.Join(", ", ExtensionContentTypes.Keys)}", nameof(fileName));
        }

        if (!DocumentAccessLevel.IsValid(request.AccessLevel))
        {
            throw new ArgumentException($"Invalid access level: {request.AccessLevel}", nameof(request));
        }

        var uploadRoot = Path.GetFullPath(storageOptions.Value.UploadPath);
        Directory.CreateDirectory(uploadRoot);
        var relativePath = Path.Combine(Guid.NewGuid().ToString("N"), Path.GetFileName(fileName));
        var fullPath = Path.Combine(uploadRoot, RelativeToLocal(relativePath));

        var fileSize = await CopyWithLimitAsync(fileStream, fullPath, storageOptions.Value.MaxFileSizeMB, cancellationToken);

        var user = await GetActiveUserAsync(userId, cancellationToken);
        var document = Document.Create(
            request.Title,
            Path.GetFileName(fileName),
            string.IsNullOrWhiteSpace(contentType) ? normalizedContentType : contentType,
            fileSize,
            relativePath,
            userId,
            request.AccessLevel);

        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        await ProcessAsync(document, fullPath, cancellationToken);

        logger.LogInformation("Uploaded document {DocumentId} ({Bytes} bytes)", document.Id, fileSize);
        return ToResponse(document);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentListResponse>> ListAsync(
        Guid userId,
        string? status = null,
        string? accessLevel = null,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        var documents = await db.Documents
            .Include(d => d.Owner)
            .Where(d => d.OwnerId == userId || d.AccessLevel != DocumentAccessLevel.Private || user.IsAdmin())
            .Where(d => status == null || d.Status == status)
            .Where(d => accessLevel == null || d.AccessLevel == accessLevel)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var chunkCounts = await db.DocumentChunks
            .GroupBy(c => c.DocumentId)
            .Select(g => new { DocumentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DocumentId, x => x.Count, cancellationToken);

        return documents
            .Where(d => d.CanAccess(user))
            .Select(d => new DocumentListResponse(
                d.Id, d.Title, d.OriginalFilename, d.ContentType, d.FileSize,
                d.AccessLevel, d.Status, chunkCounts.GetValueOrDefault(d.Id), d.CreatedAt))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DocumentResponse?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.Include(d => d.Owner).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        await EnsureAccessAsync(document, userId, cancellationToken);
        return ToResponse(document);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.Include(d => d.Owner).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null)
        {
            return false;
        }

        await EnsureAccessAsync(document, userId, cancellationToken);

        var relativePath = document.StoragePath;
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);

        TryDeleteFile(ResolveStoragePath(relativePath));
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentChunkResponse>> GetChunksAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
                       ?? throw new NotFoundException("Document not found.");
        await EnsureAccessAsync(document, userId, cancellationToken);

        var chunks = await db.DocumentChunks
            .Where(c => c.DocumentId == id)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);

        return chunks.Select(c => new DocumentChunkResponse(
            c.Id, c.ChunkIndex, c.Content, c.TokenCount, c.Metadata, null)).ToList();
    }

    /// <inheritdoc />
    public async Task<DocumentSearchResponse> SearchAsync(
        DocumentSearchRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var user = await GetActiveUserAsync(userId, cancellationToken);
        var candidates = await db.Documents
            .Where(d => d.Status == DocumentStatus.Ready)
            .ToListAsync(cancellationToken);

        var accessible = candidates.Where(d => d.CanAccess(user)).ToList();
        if (request.AccessLevel is not null)
        {
            accessible.RemoveAll(d => d.AccessLevel != request.AccessLevel);
        }
        if (request.OwnerId is not null)
        {
            accessible.RemoveAll(d => d.OwnerId != request.OwnerId);
        }

        if (accessible.Count == 0)
        {
            return new DocumentSearchResponse([], 0, DateTimeOffset.UtcNow - started);
        }

        var allowedIds = accessible.Select(d => d.Id).ToHashSet();
        var filter = JsonHelpers.ToDocument(new
        {
            document_ids = allowedIds.ToList(),
            access_levels = accessible.Select(d => d.AccessLevel).Distinct().ToList()
        });

        IReadOnlyList<DocumentChunkResponse> results;
        try
        {
            var response = await aiClient.SearchDocumentsAsync(
                request.Query, request.TopK, request.SimilarityThreshold, filter.RootElement.GetRawText(), cancellationToken);
            results = ParseSearchResults(response, allowedIds);
        }
        catch (AIServiceException ex)
        {
            logger.LogWarning(ex, "AI search unavailable; falling back to database keyword search");
            results = await KeywordSearchAsync(request.Query, request.TopK, allowedIds, cancellationToken);
        }

        // Enforce the threshold locally as well, so a misbehaving AI service cannot bypass it.
        var filtered = results.Where(r => r.SimilarityScore >= request.SimilarityThreshold).ToList();
        return new DocumentSearchResponse(filtered, filtered.Count, DateTimeOffset.UtcNow - started);
    }

    /// <inheritdoc />
    public async Task ReprocessAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await db.Documents.Include(d => d.Owner).FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
                       ?? throw new NotFoundException("Document not found.");

        var existingChunks = await db.DocumentChunks.Where(c => c.DocumentId == id).ToListAsync(cancellationToken);
        db.DocumentChunks.RemoveRange(existingChunks);
        document.UpdateStatus(DocumentStatus.Processing);
        await db.SaveChangesAsync(cancellationToken);

        var path = ResolveStoragePath(document.StoragePath);
        if (!File.Exists(path))
        {
            document.UpdateStatus(DocumentStatus.Failed, JsonHelpers.ToDocument(new { error = "Stored file is missing" }));
            await db.SaveChangesAsync(cancellationToken);
            throw new NotFoundException("The stored document file is missing; it cannot be reprocessed.");
        }

        await ProcessAsync(document, path, cancellationToken);
    }

    private async Task ProcessAsync(Document document, string path, CancellationToken cancellationToken)
    {
        try
        {
            var text = ExtractText(path);
            if (string.IsNullOrWhiteSpace(text))
            {
                document.UpdateStatus(DocumentStatus.Failed, JsonHelpers.ToDocument(new { error = "No extractable text found" }));
                await db.SaveChangesAsync(cancellationToken);
                logger.LogWarning("Document {DocumentId} contained no extractable text", document.Id);
                return;
            }

            var chunks = TextChunker.Split(text, chunkingOptions.Value.ChunkSize, chunkingOptions.Value.ChunkOverlap);
            for (var index = 0; index < chunks.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var embeddingJson = await aiClient.GenerateEmbeddingAsync(chunks[index], cancellationToken);
                var vector = JsonHelpers.TryReadVector(embeddingJson.RootElement);

                if (vector is { Length: 768 })
                {
                    db.DocumentChunks.Add(DocumentChunk.Create(document.Id, index, chunks[index], EstimateTokens(chunks[index]), new Vector(vector)));
                }
                else
                {
                    logger.LogWarning("Embedding for document {DocumentId} chunk {Index} was missing or not 768-dimensional", document.Id, index);
                    db.DocumentChunks.Add(DocumentChunk.Create(document.Id, index, chunks[index], EstimateTokens(chunks[index])));
                }
            }

            document.UpdateStatus(DocumentStatus.Ready, JsonHelpers.ToDocument(new { chunk_count = chunks.Count }));
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to process document {DocumentId}", document.Id);
            document.UpdateStatus(DocumentStatus.Failed, JsonHelpers.ToDocument(new { error = ex.Message }));
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static async Task<long> CopyWithLimitAsync(Stream source, string path, int maxSizeMB, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var maxBytes = (long)Math.Max(1, maxSizeMB) * 1024L * 1024L;
        long total = 0;
        var buffer = new byte[81920];

        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new ArgumentException($"File exceeds the maximum allowed size of {maxSizeMB} MB.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            throw new ArgumentException("Uploaded file is empty.");
        }

        return total;
    }

    private static string ExtractText(string path)
    {
        var extension = Path.GetExtension(path);
        var text = new StringBuilder();

        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(path);
            foreach (var page in pdf.GetPages())
            {
                text.AppendLine(page.Text);
            }
        }
        else if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            using var word = WordprocessingDocument.Open(path, false);
            var body = word.MainDocumentPart?.Document.Body;
            if (body is not null)
            {
                foreach (var paragraph in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                {
                    text.AppendLine(string.Concat(paragraph.Descendants<WordText>().Select(t => t.Text)));
                }
            }
        }
        else
        {
            text.Append(File.ReadAllText(path));
        }

        return text.ToString();
    }

    private IReadOnlyList<DocumentChunkResponse> ParseSearchResults(JsonDocument response, HashSet<Guid> allowedIds)
    {
        var root = response.RootElement;
        var items = root.ValueKind == JsonValueKind.Array ? root : JsonHelpers.GetArray(root, "results", "chunks", "data", "items");
        var results = new List<DocumentChunkResponse>();

        foreach (var item in items.EnumerateArray())
        {
            var documentId = ParseGuid(JsonHelpers.GetString(item, "document_id", "doc_id", "documentId"));
            var chunkId = ParseGuid(JsonHelpers.GetString(item, "chunk_id", "id", "chunkId"));

            if (documentId is null && chunkId is not null)
            {
                documentId = db.DocumentChunks
                    .Where(c => c.Id == chunkId.Value)
                    .Select(c => (Guid?)c.DocumentId)
                    .FirstOrDefault();
            }

            if (documentId is null || !allowedIds.Contains(documentId.Value))
            {
                continue;
            }

            var content = JsonHelpers.GetString(item, "content", "text", "chunk") ?? string.Empty;

            results.Add(new DocumentChunkResponse(
                chunkId ?? Guid.Empty,
                JsonHelpers.GetInt(item, "chunk_index", "index") ?? 0,
                content,
                JsonHelpers.GetInt(item, "token_count", "tokens") ?? EstimateTokens(content),
                JsonHelpers.EmptyObject(),
                JsonHelpers.GetDouble(item, "similarity_score", "similarity", "score") ?? 0));
        }

        return results;
    }

    private async Task<IReadOnlyList<DocumentChunkResponse>> KeywordSearchAsync(
        string query,
        int topK,
        HashSet<Guid> allowedIds,
        CancellationToken cancellationToken)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 2)
            .Take(10)
            .ToList();
        if (terms.Count == 0)
        {
            return [];
        }

        var ids = allowedIds.ToList();
        // A bounded candidate set keeps the fallback predictable; the actual matching is
        // case-insensitive and happens in memory so it works for any Npgsql version.
        var candidates = await db.DocumentChunks
            .Where(c => ids.Contains(c.DocumentId))
            .OrderByDescending(c => c.CreatedAt)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(c => terms.Any(t => c.Content.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new DocumentChunkResponse(
                c.Id, c.ChunkIndex, c.Content, c.TokenCount, c.Metadata,
                terms.Count(t => c.Content.Contains(t, StringComparison.OrdinalIgnoreCase)) / (double)terms.Count))
            .OrderByDescending(r => r.SimilarityScore)
            .Take(Math.Max(1, topK))
            .ToList();
    }

    private async Task EnsureAccessAsync(Document document, Guid userId, CancellationToken cancellationToken)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);
        if (!document.CanAccess(user))
        {
            throw new ForbiddenException("You do not have access to this document.");
        }
    }

    private async Task<User> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.FindAsync([userId], cancellationToken)
        ?? throw new ForbiddenException("The current user is not active.");

    private static DocumentResponse ToResponse(Document document) => new(
        document.Id,
        document.Title,
        document.OriginalFilename,
        document.ContentType,
        document.FileSize,
        document.OwnerId,
        document.Owner?.FullName ?? document.Owner?.Email ?? "System",
        document.AccessLevel,
        document.Status,
        document.Metadata,
        document.CreatedAt,
        document.UpdatedAt);

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var parsed) ? parsed : null;

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    private static string RelativeToLocal(string path) => path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private string ResolveStoragePath(string storedPath)
    {
        var root = Path.GetFullPath(storageOptions.Value.UploadPath);
        var combined = Path.GetFullPath(Path.Combine(root, RelativeToLocal(storedPath)));
        return combined.StartsWith(root, StringComparison.Ordinal)
            ? combined
            : throw new InvalidOperationException("Invalid document storage path.");
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            if (!string.IsNullOrEmpty(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (IOException ex)
        {
            // Cleanup is best-effort and must not fail an otherwise successful delete.
            logger.LogWarning(ex, "Could not remove stored file {Path}", path);
        }
    }
}

/// <summary>Splits text into overlapping chunks at paragraph/sentence boundaries.</summary>
internal static class TextChunker
{
    public static IReadOnlyList<string> Split(string text, int chunkSize, int overlap)
    {
        chunkSize = Math.Clamp(chunkSize, 200, 8000);
        overlap = Math.Clamp(overlap, 0, chunkSize / 2);

        var paragraphs = text
            .Replace("\r\n", "\n")
            .Split(['\n', '\f'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            var pieces = SplitLongParagraph(paragraph, chunkSize);
            foreach (var piece in pieces)
            {
                if (current.Length == 0)
                {
                    current.Append(piece);
                }
                else if (current.Length + piece.Length + 2 <= chunkSize)
                {
                    current.Append("\n\n").Append(piece);
                }
                else
                {
                    chunks.Add(current.ToString());
                    var tail = current.Length > overlap ? current.ToString()[^overlap..] : string.Empty;
                    current.Clear();
                    current.Append(tail);
                    if (tail.Length > 0)
                    {
                        current.Append('\n');
                    }
                    current.Append(piece);
                }
            }
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }

    private static IEnumerable<string> SplitLongParagraph(string paragraph, int chunkSize)
    {
        if (paragraph.Length <= chunkSize)
        {
            yield return paragraph;
            yield break;
        }

        var start = 0;
        while (start < paragraph.Length)
        {
            var length = Math.Min(chunkSize, paragraph.Length - start);
            if (start + length < paragraph.Length)
            {
                var window = paragraph.AsSpan(start, length);
                var split = window.LastIndexOfAny([' ', '.', ';', ',']);
                if (split > chunkSize / 2)
                {
                    length = split + 1;
                }
            }

            yield return paragraph.Substring(start, length).Trim();
            start += length;
        }
    }
}
