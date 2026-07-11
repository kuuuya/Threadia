using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Threadia.Modules.Identity.Application;
using Threadia.Modules.Identity.Infrastructure;
using Threadia.Modules.Identity.PublicApi;

namespace Threadia.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres が設定されていません。");

        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(
            connectionString,
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema)));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<AuthService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        return services;
    }
}
