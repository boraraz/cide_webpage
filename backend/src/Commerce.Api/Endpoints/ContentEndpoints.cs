using System.Security.Claims;
using Commerce.Api.Auth;
using Commerce.Api.Catalog;
using Commerce.Application.Content;
using Commerce.Domain.Catalog;
using Commerce.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Commerce.Api.Endpoints;

public static class ContentEndpoints
{
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/content/homepage", async (ContentService content, HttpResponse response, CancellationToken cancellationToken) =>
        {
            response.Headers.CacheControl = "public, max-age=30";
            return Results.Ok(await content.PublicAsync(cancellationToken));
        }).WithTags("Content").WithName("GetPublicHomepage").Produces<PublicHomepageResponse>();

        app.MapGet("/api/v1/content/media/{id:guid}", async (Guid id, ContentService content, CancellationToken cancellationToken) =>
        {
            var stored = await content.OpenAsync(id, publishedOnly: true, cancellationToken);
            return stored is null ? Results.NotFound() : MediaHttp.File(stored, published: true);
        }).WithTags("Content").WithName("GetPublicContentMedia");

        var admin = app.MapGroup("/api/v1/admin/content").WithTags("Admin content").RequireAuthorization(RoleNames.Admin);
        admin.MapGet("/homepage", async (ContentService content, CancellationToken cancellationToken) =>
            Results.Ok(await content.GetDraftAsync(cancellationToken)))
            .WithName("GetHomepageDraft").Produces<HomepageDraftResponse>();
        admin.MapPut("/homepage", SaveAsync).AddEndpointFilter<RequireAntiforgery>().WithName("SaveHomepageDraft")
            .Produces<HomepageDraftResponse>().ProducesValidationProblem();
        admin.MapPost("/homepage/publish", PublishAsync).AddEndpointFilter<RequireAntiforgery>().WithName("PublishHomepage")
            .Produces<HomepageDraftResponse>().ProducesValidationProblem();
        admin.MapGet("/homepage/preview", async (ContentService content, CancellationToken cancellationToken) =>
            Results.Ok(await content.PreviewAsync(cancellationToken)))
            .WithName("PreviewHomepage").Produces<HomepagePreviewResponse>();
        admin.MapGet("/homepage/revisions", async (ContentService content, CancellationToken cancellationToken) =>
            Results.Ok(await content.RevisionsAsync(cancellationToken)))
            .WithName("ListHomepageRevisions").Produces<RevisionSummary[]>();
        admin.MapPost("/homepage/revisions/{id:guid}/restore", RestoreAsync).AddEndpointFilter<RequireAntiforgery>().WithName("RestoreHomepageRevision")
            .Produces<HomepageDraftResponse>().ProducesValidationProblem();
        admin.MapPost("/media", UploadAsync)
            .AddEndpointFilter<RequireAntiforgery>()
            .RequireRateLimiting(CatalogRateLimits.MediaUpload)
            .DisableAntiforgery()
            .WithName("UploadContentMedia")
            .Produces<ContentMediaResponse>(StatusCodes.Status201Created);
        admin.MapGet("/media/{id:guid}", async (Guid id, ContentService content, CancellationToken cancellationToken) =>
        {
            var stored = await content.OpenAsync(id, publishedOnly: false, cancellationToken);
            return stored is null ? Results.NotFound() : MediaHttp.File(stored, published: false);
        }).WithName("GetAdminContentMedia");
        return app;
    }

    private static async Task<IResult> SaveAsync(HomepageWriteRequest request, ContentService content, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        CatalogHttp.From(
            await content.SaveDraftAsync(request.Configuration, request.ExpectedVersion, Actor(user), cancellationToken),
            Results.Ok);

    private static async Task<IResult> PublishAsync(HomepageVersionRequest request, ContentService content, CancellationToken cancellationToken) =>
        CatalogHttp.From(await content.PublishAsync(request.ExpectedVersion, cancellationToken), Results.Ok);

    private static async Task<IResult> RestoreAsync(Guid id, HomepageVersionRequest request, ContentService content, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        CatalogHttp.From(await content.RestoreAsync(id, request.ExpectedVersion, Actor(user), cancellationToken), Results.Ok);

    private static async Task<IResult> UploadAsync(
        [FromForm] IFormFile file,
        [FromForm] string altText,
        ContentService content,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (file.Length > ImageSignature.MaximumBytes)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Upload a JPEG, PNG or WebP image up to 5 MB."] });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return CatalogHttp.From(
            await content.UploadAsync(buffer.ToArray(), altText, Actor(user), cancellationToken),
            value => Results.Created($"/api/v1/admin/content/media/{value.Id}", value));
    }

    private static Guid Actor(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

internal sealed record HomepageWriteRequest(string? Configuration, int ExpectedVersion);

internal sealed record HomepageVersionRequest(int ExpectedVersion);
