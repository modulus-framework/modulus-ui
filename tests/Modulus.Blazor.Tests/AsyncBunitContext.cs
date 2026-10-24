using Bunit;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>
/// The base of every component test class. xUnit awaits <see cref="IAsyncLifetime.DisposeAsync"/> before the synchronous dispose, and bUnit's
/// asynchronous path awaits component teardown. So components finish tearing down before AntDesign's shared DOM listener store is disposed.
/// Disposing synchronously raced that store ("Collection was modified"), so every test class derives from this instead of BunitContext.
/// </summary>
public abstract class AsyncBunitContext : BunitContext, IAsyncLifetime
{
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();
}
