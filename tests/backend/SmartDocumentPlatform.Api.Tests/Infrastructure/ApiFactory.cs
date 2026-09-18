using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartDocumentPlatform.Api.Data;
using SmartDocumentPlatform.Api.Services;

namespace SmartDocumentPlatform.Api.Tests.Infrastructure;

public sealed class ApiFactory(bool simulateCvFailure = false, long? maximumUploadBytes = null) : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"smart-documents-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=localhost;Database=tests;Username=tests;Password=tests");
        if (maximumUploadBytes is not null)
        {
            builder.UseSetting("Uploads:MaxBytes", maximumUploadBytes.Value.ToString());
        }
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage, InMemoryDocumentStorage>();
            services.RemoveAll<IComputerVisionClient>();
            if (simulateCvFailure)
            {
                services.AddSingleton<IComputerVisionClient, FailingComputerVisionClient>();
            }
            else
            {
                services.AddSingleton<IComputerVisionClient, FakeComputerVisionClient>();
            }
        });
    }
}
