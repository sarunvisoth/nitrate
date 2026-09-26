using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Prismedia.Application.Backups;

namespace Prismedia.Infrastructure.Database;

/// <summary>
/// Coordinates restore markers during process startup so destructive restores happen
/// before normal request handling and before the worker processes jobs.
/// </summary>
public static class DatabaseRestoreRunner {
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Applies a pending restore marker, if present, before API migrations run.
    /// </summary>
    public static async Task ApplyPendingRestoreAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default) {
        if (!ShouldHandleRestoreMarkers(configuration)) {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var backups = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
        await backups.RunPendingRestoreAsync(cancellationToken);
    }

    /// <summary>
    /// Keeps worker startup idle while an API process is expected to restore the database.
    /// </summary>
    public static async Task WaitForPendingRestoreToClearAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default) {
        if (!ShouldHandleRestoreMarkers(configuration)) {
            return;
        }

        var logger = CreateLogger(services);
        while (!cancellationToken.IsCancellationRequested) {
            await using var scope = services.CreateAsyncScope();
            var backups = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
            if (!await backups.HasPendingRestoreAsync(cancellationToken)) {
                return;
            }

            logger.LogWarning("Database restore is pending; worker startup is waiting.");
            await Task.Delay(CheckInterval, cancellationToken);
        }
    }

    private static bool ShouldHandleRestoreMarkers(IConfiguration configuration) {
        if (AppDomain.CurrentDomain
            .GetAssemblies()
            .Any(assembly => assembly.GetName().Name == "Microsoft.AspNetCore.Mvc.Testing")) {
            return false;
        }

        var configured = configuration["Prismedia:ApplyMigrations"];
        return configured is null || bool.TryParse(configured, out var enabled) && enabled;
    }

    private static ILogger CreateLogger(IServiceProvider services) =>
        services.GetService<ILoggerFactory>()?.CreateLogger("Prismedia.DatabaseRestore")
        ?? NullLogger.Instance;
}
