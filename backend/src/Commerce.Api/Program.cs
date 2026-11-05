using Commerce.Api.Auth;
using Commerce.Api.Cors;
using Commerce.Api.Endpoints;
using Commerce.Api.Errors;
using Commerce.Application;
using Commerce.Infrastructure;
using Microsoft.AspNetCore.HttpLogging;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

builder.Services.AddOpenApi();
builder.Services.AddFrontendCors();
builder.Services.AddSessionAuthentication(builder.Environment);
builder.Services.AddAuthRateLimits();

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseHttpLogging();
app.UseRateLimiter();
app.UseCors(CorsSetup.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapAdminAuthEndpoints();

app.Run();
