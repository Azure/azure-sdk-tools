// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.CopilotAgents.Tools;

namespace Azure.Sdk.Tools.Cli.Tests.CopilotAgents.Tools;

[TestFixture]
public class RepairToolGateTests
{
    [Test]
    public async Task GateIsClosedUntilAnActiveTurnAndCanReopenOnlyBeforeCompletion()
    {
        await using var gate = new RepairToolGate(CancellationToken.None);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await gate.EnterAsync(CancellationToken.None));
        gate.Open();
        using (await gate.EnterAsync(CancellationToken.None)) { }
        await gate.CloseAsync(CancellationToken.None);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await gate.EnterAsync(CancellationToken.None));
        gate.Open();
        using (await gate.EnterAsync(CancellationToken.None)) { }
        await gate.CloseAsync(CancellationToken.None);
        gate.Complete();
        Assert.Throws<InvalidOperationException>(gate.Open);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await gate.EnterAsync(CancellationToken.None));
    }

    [Test]
    public async Task ClosingDrainsActiveMutationAndRejectsAlreadyQueuedMutations()
    {
        await using var gate = new RepairToolGate(CancellationToken.None);
        gate.Open();
        var active = await gate.EnterAsync(CancellationToken.None);
        var queued = gate.EnterAsync(CancellationToken.None).AsTask();
        var closing = gate.CloseAsync(CancellationToken.None);
        try
        {
            Assert.That(gate.AllowsWrites, Is.False);
            Assert.That(closing.IsCompleted, Is.False, "Validation cannot start while a mutation lease is held.");
            Assert.That(queued.IsCompleted, Is.False);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await gate.EnterAsync(CancellationToken.None));
        }
        finally
        {
            active.Dispose();
        }
        Assert.ThrowsAsync<InvalidOperationException>(async () => await queued.WaitAsync(TimeSpan.FromSeconds(5)));
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task DisposalDrainsActiveMutationAndPermanentlyRejectsNewCalls()
    {
        var gate = new RepairToolGate(CancellationToken.None);
        gate.Open();
        var active = await gate.EnterAsync(CancellationToken.None);
        var disposing = gate.DisposeAsync().AsTask();
        try
        {
            Assert.That(disposing.IsCompleted, Is.False);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await gate.EnterAsync(CancellationToken.None));
        }
        finally
        {
            active.Dispose();
        }
        await disposing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Throws<InvalidOperationException>(gate.Open);
        Assert.ThrowsAsync<InvalidOperationException>(async () => await gate.EnterAsync(CancellationToken.None));
        await gate.DisposeAsync();
    }

    [Test]
    public async Task CancelledQueuedMutationDoesNotLeakTheGate()
    {
        await using var gate = new RepairToolGate(CancellationToken.None);
        gate.Open();
        var active = await gate.EnterAsync(CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        var queued = gate.EnterAsync(cancelled.Token).AsTask();
        cancelled.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await queued);
        active.Dispose();
        await gate.CloseAsync(CancellationToken.None);
        gate.Open();
        using (await gate.EnterAsync(CancellationToken.None)) { }
    }

    [Test]
    public async Task InvocationCancellationDisablesMutationRegardlessOfTheToolToken()
    {
        using var cancellation = new CancellationTokenSource();
        await using var gate = new RepairToolGate(cancellation.Token);
        gate.Open();
        cancellation.Cancel();
        Assert.That(gate.AllowsWrites, Is.False);
        Assert.CatchAsync<OperationCanceledException>(async () => await gate.EnterAsync(CancellationToken.None));
    }

    [Test]
    public async Task InvocationCancellationAlsoCancelsQueuedToolAcquisition()
    {
        using var cancellation = new CancellationTokenSource();
        await using var gate = new RepairToolGate(cancellation.Token);
        gate.Open();
        var active = await gate.EnterAsync(CancellationToken.None);
        try
        {
            var queued = gate.EnterAsync(CancellationToken.None).AsTask();
            cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await queued.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            active.Dispose();
        }
    }
}
