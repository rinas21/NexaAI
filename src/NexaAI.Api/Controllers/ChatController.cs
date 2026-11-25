using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;

namespace NexaAI.Api.Controllers;

/// <summary>Conversation management and RAG chat with source citations.</summary>
[Route("api/chat")]
public sealed class ChatController(IChatService chatService) : ApiControllerBase
{
    /// <summary>Sends a chat message, optionally continuing an existing conversation.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await chatService.SendMessageAsync(request, CurrentUserId, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<ChatResponse>(ex);
        }
    }

    /// <summary>Lists the current user's conversations.</summary>
    [HttpGet("conversations")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationResponse>>> Conversations(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await chatService.GetConversationsAsync(CurrentUserId, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<IReadOnlyList<ConversationResponse>>(ex);
        }
    }

    /// <summary>Creates an empty conversation.</summary>
    [HttpPost("conversations")]
    [Authorize]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<Guid>> CreateConversation(
        [FromBody] CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await chatService.CreateConversationAsync(request, CurrentUserId, cancellationToken);
            return CreatedAtAction(nameof(Conversation), new { id }, id);
        }
        catch (Exception ex)
        {
            return Failure<Guid>(ex);
        }
    }

    /// <summary>Returns a conversation with all of its messages.</summary>
    [HttpGet("conversations/{id:guid}", Name = "GetConversation")]
    [Authorize]
    [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationResponse>> Conversation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var conversation = await chatService.GetConversationAsync(id, CurrentUserId, cancellationToken);
            return conversation is null ? NotFound() : Ok(conversation);
        }
        catch (Exception ex)
        {
            return Failure<ConversationResponse>(ex);
        }
    }

    /// <summary>Deletes a conversation and its messages.</summary>
    [HttpDelete("conversations/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteConversation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return await chatService.DeleteConversationAsync(id, CurrentUserId, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>Adds a message to a conversation and streams the response as Server-Sent Events.</summary>
    [HttpPost("conversations/{id:guid}/messages")]
    [Authorize]
    [Produces("text/event-stream")]
    public async Task<IActionResult> StreamMessage(
        Guid id,
        [FromBody] StreamChatRequest request,
        CancellationToken cancellationToken)
    {
        ChatResponse response;
        try
        {
            response = await chatService.SendMessageAsync(
                new ChatRequest(request.Message, id, false, null), CurrentUserId, cancellationToken);
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }

        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";

        await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(false), 4096, leaveOpen: true)
        {
            AutoFlush = true
        };
        await writer.WriteAsync($"event: message\ndata: {JsonSerializer.Serialize(response)}\n\n");
        await writer.WriteAsync("event: done\ndata: [DONE]\n\n");
        return new EmptyResult();
    }
}
