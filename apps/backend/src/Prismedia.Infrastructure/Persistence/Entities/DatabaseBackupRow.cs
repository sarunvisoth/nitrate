using Prismedia.Domain.Entities;

namespace Prismedia.Infrastructure.Persistence.Entities;

public sealed class DatabaseBackupRow {
    public Guid Id { get; set; }

    public string BackupPath { get; set; } = string.Empty;

    public DatabaseBackupStatus Status { get; set; } = DatabaseBackupStatus.Running;

    public bool IsManual { get; set; }

    public long? SizeBytes { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
}
