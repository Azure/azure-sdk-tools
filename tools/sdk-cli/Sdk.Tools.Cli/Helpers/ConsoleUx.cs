// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Sdk.Tools.Cli.Helpers;

/// <summary>
/// Console UX helpers for streaming output and animations.
/// Provides spinners, progress indicators, and styled output.
/// </summary>
public static class ConsoleUx
{
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private static readonly object _lock = new();
    
    private static bool _supportsAnsi = !Console.IsOutputRedirected && 
        Environment.GetEnvironmentVariable("NO_COLOR") == null;
    
    // Colors
    public static string Dim(string text) => _supportsAnsi ? $"\x1b[90m{text}\x1b[0m" : text;
    public static string Green(string text) => _supportsAnsi ? $"\x1b[32m{text}\x1b[0m" : text;
    public static string Yellow(string text) => _supportsAnsi ? $"\x1b[33m{text}\x1b[0m" : text;
    public static string Cyan(string text) => _supportsAnsi ? $"\x1b[36m{text}\x1b[0m" : text;
    public static string Bold(string text) => _supportsAnsi ? $"\x1b[1m{text}\x1b[0m" : text;
    public static string Red(string text) => _supportsAnsi ? $"\x1b[31m{text}\x1b[0m" : text;
    
    /// <summary>
    /// Runs an action with a spinner animation.
    /// </summary>
    public static async Task<T> SpinnerAsync<T>(string message, Func<Task<T>> action, CancellationToken ct = default)
    {
        if (!_supportsAnsi)
        {
            Console.WriteLine($"  {message}");
            return await action();
        }
        
        var spinnerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var spinnerTask = RunSpinnerAsync(message, spinnerCts.Token);
        
        try
        {
            var result = await action();
            spinnerCts.Cancel();
            await spinnerTask;
            ClearLine();
            Console.WriteLine($"  {Green("✓")} {message}");
            return result;
        }
        catch
        {
            spinnerCts.Cancel();
            await spinnerTask;
            ClearLine();
            Console.WriteLine($"  {Red("✗")} {message}");
            throw;
        }
    }
    
    /// <summary>
    /// Runs an action with a spinner, no return value.
    /// </summary>
    public static async Task SpinnerAsync(string message, Func<Task> action, CancellationToken ct = default)
    {
        await SpinnerAsync(message, async () => { await action(); return 0; }, ct);
    }
    
    /// <summary>
    /// Creates a live progress context for streaming operations.
    /// </summary>
    public static StreamingProgress StartStreaming(string message)
    {
        return new StreamingProgress(message, _supportsAnsi);
    }
    
    /// <summary>
    /// Writes a success line.
    /// </summary>
    public static void Success(string message) => Console.WriteLine($"  {Green("✓")} {message}");
    
    /// <summary>
    /// Writes an error line.
    /// </summary>
    public static void Error(string message) => Console.WriteLine($"  {Red("✗")} {message}");
    
    /// <summary>
    /// Writes an info line (dimmed).
    /// </summary>
    public static void Info(string message) => Console.WriteLine($"  {Dim(message)}");
    
    /// <summary>
    /// Writes a header line.
    /// </summary>
    public static void Header(string message) => Console.WriteLine($"\n{Bold(message)}");
    
    /// <summary>
    /// Writes a tree item (for streaming samples).
    /// </summary>
    public static void TreeItem(string text, bool isLast = false)
    {
        var prefix = isLast ? "└" : "├";
        Console.WriteLine($"    {Dim(prefix)} {text}");
    }
    
    /// <summary>
    /// Writes a numbered item during streaming.
    /// </summary>
    public static void NumberedItem(int number, string text)
    {
        Console.WriteLine($"    {Dim($"[{number}]")} {Green("✓")} {text}");
    }
    
    private static async Task RunSpinnerAsync(string message, CancellationToken ct)
    {
        var frame = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                lock (_lock)
                {
                    ClearLine();
                    Console.Write($"  {Cyan(SpinnerFrames[frame])} {message}");
                }
                frame = (frame + 1) % SpinnerFrames.Length;
                await Task.Delay(80, ct);
            }
        }
        catch (OperationCanceledException) { }
    }
    
    private static void ClearLine()
    {
        if (_supportsAnsi)
        {
            Console.Write("\r\x1b[K");
        }
        else
        {
            Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
        }
    }
    
    /// <summary>
    /// Progress tracker for streaming operations.
    /// </summary>
    public class StreamingProgress : IDisposable
    {
        private readonly string _message;
        private readonly bool _supportsAnsi;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _spinnerTask;
        private int _count;
        private string? _currentItem;
        
        internal StreamingProgress(string message, bool supportsAnsi)
        {
            _message = message;
            _supportsAnsi = supportsAnsi;
            
            if (_supportsAnsi)
            {
                _spinnerTask = RunAsync();
            }
            else
            {
                Console.WriteLine($"  {message}");
                _spinnerTask = Task.CompletedTask;
            }
        }
        
        /// <summary>
        /// Updates the progress with a new item.
        /// </summary>
        public void Update(string item)
        {
            Interlocked.Increment(ref _count);
            _currentItem = item;
        }
        
        /// <summary>
        /// Gets the current count.
        /// </summary>
        public int Count => _count;
        
        private async Task RunAsync()
        {
            var frame = 0;
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var countStr = _count > 0 ? $" ({_count})" : "";
                    var itemStr = _currentItem != null ? $" → {TruncateForTerminal(_currentItem, _message.Length + countStr.Length)}" : "";
                    
                    lock (_lock)
                    {
                        ClearLine();
                        Console.Write($"  {Cyan(SpinnerFrames[frame])} {_message}{Dim(countStr)}{Dim(itemStr)}");
                    }
                    frame = (frame + 1) % SpinnerFrames.Length;
                    await Task.Delay(80, _cts.Token);
                }
            }
            catch (OperationCanceledException) { }
        }
        
        private static string TruncateForTerminal(string text, int usedWidth)
        {
            try
            {
                // Reserve: 2 (indent) + 1 (spinner) + 1 (space) + usedWidth + 4 (" → ") + 1 (safety)
                var maxLen = Console.WindowWidth - usedWidth - 9;
                if (maxLen < 10) maxLen = 30; // Reasonable minimum
                return text.Length > maxLen ? text[..(maxLen - 1)] + "…" : text;
            }
            catch
            {
                // Console.WindowWidth can throw in non-interactive contexts
                return text.Length > 40 ? text[..39] + "…" : text;
            }
        }
        
        /// <summary>
        /// Completes the progress with success.
        /// </summary>
        public void Complete(string? summary = null)
        {
            _cts.Cancel();
            try { _spinnerTask.Wait(100); } catch { } // 100ms timeout
            
            if (_supportsAnsi) ClearLine();
            
            var text = summary ?? $"{_message} ({_count})";
            Console.WriteLine($"  {Green("✓")} {text}");
        }
        
        /// <summary>
        /// Completes the progress with failure.
        /// </summary>
        public void Fail(string? message = null)
        {
            _cts.Cancel();
            try { _spinnerTask.Wait(100); } catch { } // 100ms timeout
            
            if (_supportsAnsi) ClearLine();
            Console.WriteLine($"  {Red("✗")} {message ?? _message}");
        }
        
        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
