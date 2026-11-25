using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;

namespace NexaAI.Api.Controllers;

/// <summary>Document upload, management, chunk inspection and permission-aware search.</summary>
[Route("api/documents")]
public sealed class DocumentsController(IDocumentService documentService) : ApiControllerBase
{
    /// <summary>Uploads a PDF, DOCX or text document and queues it for processing.</summary>
    [HttpPost]
    [Authorize]
    [RequestSizeLimit(60L * 1024 * 1024)]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<DocumentResponse>> Upload(
        [FromForm] IFormFile file,
        [FromForm] DocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var document = await documentService.UploadAsync(
                stream, file.FileName, file.ContentType, request, CurrentUserId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = document.Id }, document);
        }
        catch (Exception ex)
        {
            return Failure<DocumentResponse>(ex);
        }
    }

    /// <summary>Lists documents visible to the current user.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentListResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DocumentListResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] string? accessLevel,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await documentService.ListAsync(CurrentUserId, status, accessLevel, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<IReadOnlyList<DocumentListResponse>>(ex);
        }
    }

    /// <summary>Runs a semantic search with the caller's document permissions enforced.</summary>
    [HttpPost("search")]
    [Authorize]
    [ProducesResponseType(typeof(DocumentSearchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DocumentSearchResponse>> Search(
        [FromBody] DocumentSearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await documentService.SearchAsync(request, CurrentUserId, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<DocumentSearchResponse>(ex);
        }
    }

    /// <summary>Returns document metadata by id.</summary>
    [HttpGet("{id:guid}", Name = "GetDocument")]
    [Authorize]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DocumentResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var document = await documentService.GetByIdAsync(id, CurrentUserId, cancellationToken);
            return document is null ? NotFound() : Ok(document);
        }
        catch (Exception ex)
        {
            return Failure<DocumentResponse>(ex);
        }
    }

    /// <summary>Deletes a document and its stored file.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return await documentService.DeleteAsync(id, CurrentUserId, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>Lists the chunks stored for a document.</summary>
    [HttpGet("{id:guid}/chunks")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentChunkResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DocumentChunkResponse>>> Chunks(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await documentService.GetChunksAsync(id, CurrentUserId, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<IReadOnlyList<DocumentChunkResponse>>(ex);
        }
    }

    /// <summary>Re-runs text extraction and embedding generation (administrators only).</summary>
    [HttpPost("{id:guid}/reprocess")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DocumentResponse>> Reprocess(
        Guid id,
        [FromBody] ReprocessDocumentRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            await documentService.ReprocessAsync(id, cancellationToken);
            var document = await documentService.GetByIdAsync(id, CurrentUserId, cancellationToken);
            return document is null ? NotFound() : Ok(document);
        }
        catch (Exception ex)
        {
            return Failure<DocumentResponse>(ex);
        }
    }
}
