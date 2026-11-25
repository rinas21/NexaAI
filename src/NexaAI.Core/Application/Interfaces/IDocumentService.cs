using NexaAI.Core.Application.DTOs;

namespace NexaAI.Core.Application.Interfaces;

public interface IDocumentService
{
    Task<DocumentResponse> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        DocumentUploadRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentListResponse>> ListAsync(
        Guid userId,
        string? status = null,
        string? accessLevel = null,
        CancellationToken cancellationToken = default);

    Task<DocumentResponse?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunkResponse>> GetChunksAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DocumentSearchResponse> SearchAsync(
        DocumentSearchRequest request,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task ReprocessAsync(Guid id, CancellationToken cancellationToken = default);
}