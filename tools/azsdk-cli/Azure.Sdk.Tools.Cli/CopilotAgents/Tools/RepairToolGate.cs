// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.CopilotAgents.Tools;

/// <summary>
/// Drains mutation tools before host validation and rejects calls outside active repair turns.
/// </summary>
public sealed class RepairToolGate(CancellationToken invocationToken) : IAsyncDisposable
{
    private const int Paused = 0;
    private const int Active = 1;
    private const int Terminal = 2;
    private readonly SemaphoreSlim _mutation = new(1, 1);
    private int _state = Paused;
    private int _disposed;

    public bool AllowsWrites => Volatile.Read(ref _state) == Active && !invocationToken.IsCancellationRequested;

    public void Open()
    {
        invocationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _state, Active, Paused) != Paused)
        {
            throw new InvalidOperationException("Cannot start a mutation turn in this repair session state.");
        }
    }

    public async ValueTask<IDisposable> EnterAsync(CancellationToken ct)
    {
        invocationToken.ThrowIfCancellationRequested();
        ct.ThrowIfCancellationRequested();
        if (!AllowsWrites)
        {
            throw new InvalidOperationException("Mutation tools are unavailable outside an active repair turn.");
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(invocationToken, ct);
        await _mutation.WaitAsync(linked.Token).ConfigureAwait(false);
        if (!AllowsWrites)
        {
            _mutation.Release();
            throw new InvalidOperationException("Mutation tools are unavailable outside an active repair turn.");
        }
        return new MutationLease(_mutation);
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        Interlocked.CompareExchange(ref _state, Paused, Active);
        // A path check alone cannot prevent a write already in progress from racing validation.
        await _mutation.WaitAsync(ct).ConfigureAwait(false);
        _mutation.Release();
    }

    public void Complete() => Interlocked.Exchange(ref _state, Terminal);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) { return; }
        Complete();
        await _mutation.WaitAsync().ConfigureAwait(false);
        _mutation.Release();
        _mutation.Dispose();
    }

    private sealed class MutationLease(SemaphoreSlim mutation) : IDisposable
    {
        private SemaphoreSlim? _mutation = mutation;

        public void Dispose() => Interlocked.Exchange(ref _mutation, null)?.Release();
    }
}
