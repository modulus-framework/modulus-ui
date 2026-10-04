using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Ui.Abstractions.State;

namespace Modulus.Samples.Shared;

/// <summary>One user's value under one key (<see cref="IUiStateStore"/>): a row of the UiState table.</summary>
public sealed class UiStateEntry
{
    public string User { get; set; } = "";

    public string Key { get; set; } = "";

    public string Value { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>The UiState table, keyed by user and key. Any EF Core provider works; the samples use SQLite.</summary>
public sealed class UiStateDbContext(DbContextOptions<UiStateDbContext> options) : DbContext(options)
{
    public DbSet<UiStateEntry> UiState => Set<UiStateEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<UiStateEntry>(e =>
    {
        e.ToTable("UiState");
        e.HasKey(x => new { x.User, x.Key });
        e.Property(x => x.User).HasMaxLength(200);
        e.Property(x => x.Key).HasMaxLength(400);
    });
}

/// <summary>
/// An <see cref="IUiStateStore"/> in a database (one row per user and key), so saved views outlive a restart and are
/// shared by every server. The same limits as <see cref="MemoryUiStateStore"/> apply; an anonymous user keeps nothing.
/// </summary>
public sealed class EfUiStateStore(IDbContextFactory<UiStateDbContext> factory, TimeProvider? clock = null) : IUiStateStore
{
    public async ValueTask<string?> GetAsync(string user, string key, CancellationToken cancellationToken = default)
    {
        if (user.Length == 0) return null;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.UiState.AsNoTracking().Where(e => e.User == user && e.Key == key).Select(e => e.Value).FirstOrDefaultAsync(cancellationToken);
    }

    public async ValueTask SetAsync(string user, string key, string? value, CancellationToken cancellationToken = default)
    {
        if (user.Length == 0) return;
        if (value?.Length > MemoryUiStateStore.MaxValueLength)
            throw new ArgumentException($"UI state values are limited to {MemoryUiStateStore.MaxValueLength} characters.", nameof(value));

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entry = await db.UiState.FindAsync([user, key], cancellationToken);
        if (value is null)
        {
            if (entry is null) return;
            db.UiState.Remove(entry);
        }
        else if (entry is null)
        {
            if (await db.UiState.CountAsync(e => e.User == user, cancellationToken) >= MemoryUiStateStore.MaxKeysPerUser)
                throw new InvalidOperationException($"A user keeps at most {MemoryUiStateStore.MaxKeysPerUser} UI state keys.");
            db.UiState.Add(new UiStateEntry { User = user, Key = key, Value = value, UpdatedAt = Now });
        }
        else
        {
            entry.Value = value;
            entry.UpdatedAt = Now;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private DateTimeOffset Now => (clock ?? TimeProvider.System).GetUtcNow();
}

/// <summary>The samples' UI state: saved views in a SQLite file (<c>App_Data/ui-state.db</c>) instead of memory.</summary>
public static class SampleUiState
{
    /// <summary>
    /// Registers <see cref="EfUiStateStore"/> on SQLite under <paramref name="contentRoot"/>; call it before AddModulusMvc
    /// or AddModulusBlazor, whose in-memory default then stands aside.
    /// </summary>
    public static IServiceCollection AddSampleUiState(this IServiceCollection services, string contentRoot)
    {
        var folder = Path.Combine(contentRoot, "App_Data");
        Directory.CreateDirectory(folder);
        services.AddDbContextFactory<UiStateDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(folder, "ui-state.db")}"));
        services.AddSingleton<IUiStateStore, EfUiStateStore>();
        return services;
    }

    /// <summary>Creates the table on first run (a real app would use a migration).</summary>
    public static void EnsureUiStateDatabase(this IServiceProvider services)
    {
        using var db = services.GetRequiredService<IDbContextFactory<UiStateDbContext>>().CreateDbContext();
        db.Database.EnsureCreated();
    }
}
