using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Threadia.BuildingBlocks.Events;
using Threadia.Modules.Attachments.Application;
using Threadia.Modules.Attachments.Infrastructure;
using Threadia.Modules.Attachments.PublicApi;

namespace Threadia.Modules.Attachments;

public static class AttachmentsModule
{
    public static IServiceCollection AddAttachmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres が設定されていません。");

        services.AddDbContext<AttachmentsDbContext>(options => options.UseNpgsql(
            connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", AttachmentsDbContext.Schema)));

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        var storageProvider = configuration[$"{StorageOptions.SectionName}:Provider"] ?? "S3";
        if (string.Equals(storageProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<InMemoryObjectStorage>();
            services.AddSingleton<IObjectStorage>(sp => sp.GetRequiredService<InMemoryObjectStorage>());
        }
        else
        {
            services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        }

        services.AddScoped<AttachmentService>();
        services.AddScoped<IMessageAttachments, MessageAttachments>();
        services.AddIntegrationEventConsumer<MessageDeletedAttachmentCleanupConsumer>();

        return services;
    }
}
