// Defines persistence initialization for the local database.
using ReachIT.Infrastructure.Persistence;

namespace ReachIT.Application.Contracts;

public interface IDatabaseService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    ReachItDbContext CreateDbContext();
    string DatabasePath { get; }
}
