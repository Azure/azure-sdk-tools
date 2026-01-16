// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Concurrent;

namespace Azure.Sdk.Tools.Cli.Tools.Telemetry
{
    public class TelemetrySession : IDisposable
    {
        public string SessionId { get; }
        public DateTime StartTime { get; }
        public string? UserId { get; set; }
        public string? InitialContext { get; set; }
        public List<string> ConversationMessages { get; }
        public Dictionary<string, object> Metadata { get; }
        public ConcurrentQueue<AuditLogItem> AuditLog { get; }
        public bool IsActive { get; set; }
        
        private readonly SemaphoreSlim _sessionLock = new(1, 1);

        public TelemetrySession(string? userId = null, string? initialContext = null)
        {
            SessionId = Guid.NewGuid().ToString();
            StartTime = DateTime.UtcNow;
            UserId = userId;
            InitialContext = initialContext;
            ConversationMessages = new List<string>();
            Metadata = new Dictionary<string, object>();
            AuditLog = new ConcurrentQueue<AuditLogItem>();
            IsActive = true;

            AuditLog.Enqueue(new AuditLogItem(SessionId, $"Session started at {StartTime:yyyy-MM-dd HH:mm:ss UTC}"));
        }

        public async Task AddMessage(string message)
        {
            await _sessionLock.WaitAsync();
            try
            {
                if (IsActive)
                {
                    ConversationMessages.Add(message);
                    AuditLog.Enqueue(new AuditLogItem(SessionId, $"Message added: {message.Length} characters"));
                }
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public async Task<string> GetFullConversation()
        {
            await _sessionLock.WaitAsync();
            try
            {
                var conversation = string.Join("\n\n", ConversationMessages);
                if (!string.IsNullOrEmpty(InitialContext))
                {
                    return $"Initial Context: {InitialContext}\n\n{conversation}";
                }
                return conversation;
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public async Task EndSession()
        {
            await _sessionLock.WaitAsync();
            try
            {
                IsActive = false;
                var endTime = DateTime.UtcNow;
                var duration = endTime - StartTime;
                
                Metadata["EndTime"] = endTime;
                Metadata["Duration"] = duration;
                Metadata["MessageCount"] = ConversationMessages.Count;
                
                AuditLog.Enqueue(new AuditLogItem(SessionId, 
                    $"Session ended at {endTime:yyyy-MM-dd HH:mm:ss UTC}. Duration: {duration.TotalMinutes:F2} minutes"));
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        public void Dispose()
        {
            _sessionLock?.Dispose();
        }
    }

    public class AuditLogItem
    {
        public string SessionId { get; }
        public DateTime Timestamp { get; }
        public string Message { get; }

        public AuditLogItem(string sessionId, string message)
        {
            SessionId = sessionId;
            Timestamp = DateTime.UtcNow;
            Message = message;
        }
    }
}