using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartDocumentPlatform.Api.Dtos;
using SmartDocumentPlatform.Api.Services;

namespace SmartDocumentPlatform.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(IDocumentService documentService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<DocumentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [EnableRateLimiting("Uploads")]
    public async Task<ActionResult<DocumentResponse>> Upload(
        [FromForm] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var document = await documentService.ProcessAsync(request.File, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = document.Id }, document);
    }

    [HttpGet]
    [ProducesResponseType<PagedResponse<DocumentResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<DocumentResponse>>> List(
        [FromQuery, Range(0, int.MaxValue)] int skip = 0,
        [FromQuery, Range(1, 100)] int take = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await documentService.ListAsync(skip, take, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DocumentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentResponse>> Get(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await documentService.GetAsync(id, cancellationToken));

    [HttpGet("{id:guid}/original")]
    [Produces("image/jpeg", "image/png")]
    public async Task<IActionResult> GetOriginal(Guid id, CancellationToken cancellationToken)
    {
        var file = await documentService.OpenImageAsync(id, processed: false, cancellationToken);
        return File(file.Stream, file.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}/processed")]
    [Produces("image/jpeg")]
    public async Task<IActionResult> GetProcessed(Guid id, CancellationToken cancellationToken)
    {
        var file = await documentService.OpenImageAsync(id, processed: true, cancellationToken);
        return File(file.Stream, file.ContentType, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await documentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
