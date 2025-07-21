using Microsoft.Extensions.Logging;

namespace ExcelRAGChatbot.Services;

public class SessionManager
{
    private readonly AzureSearchService _searchService;
    private readonly ILogger<SessionManager> _logger;
    private readonly Dictionary<string, SessionInfo> _activeSessions;

    public SessionManager(AzureSearchService searchService, ILogger<SessionManager> logger)
    {
        _searchService = searchService;
        _logger = logger;
        _activeSessions = new Dictionary<string, SessionInfo>();
    }

    public async Task<string> CreateSessionAsync()
    {
        var sessionId = Guid.NewGuid().ToString("N")[..8]; // Short session ID
        
        try
        {
            var indexName = await _searchService.CreateSessionIndexAsync(sessionId);
            
            var sessionInfo = new SessionInfo
            {
                SessionId = sessionId,
                IndexName = indexName,
                CreatedAt = DateTime.UtcNow,
                ConversationHistory = new List<string>(),
                UploadedFiles = new List<string>()
            };

            _activeSessions[sessionId] = sessionInfo;
            
            _logger.LogInformation($"Created session: {sessionId}");
            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating session: {sessionId}");
            throw;
        }
    }

    public async Task EndSessionAsync(string sessionId)
    {
        if (!_activeSessions.TryGetValue(sessionId, out var sessionInfo))
        {
            _logger.LogWarning($"Session {sessionId} not found for cleanup");
            return;
        }

        try
        {
            await _searchService.DeleteIndexAsync(sessionId);
            _activeSessions.Remove(sessionId);
            
            _logger.LogInformation($"Ended session: {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error ending session: {sessionId}");
            throw;
        }
    }

    public SessionInfo? GetSession(string sessionId)
    {
        return _activeSessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    public void AddConversationToSession(string sessionId, string userMessage, string assistantResponse)
    {
        if (_activeSessions.TryGetValue(sessionId, out var session))
        {
            session.ConversationHistory.Add(userMessage);
            session.ConversationHistory.Add(assistantResponse);
            session.LastActivity = DateTime.UtcNow;
        }
    }

    public void AddFileToSession(string sessionId, string fileName)
    {
        if (_activeSessions.TryGetValue(sessionId, out var session))
        {
            session.UploadedFiles.Add(fileName);
            session.LastActivity = DateTime.UtcNow;
        }
    }

    public List<SessionInfo> GetActiveSessions()
    {
        return _activeSessions.Values.ToList();
    }

    public async Task CleanupInactiveSessionsAsync(TimeSpan maxInactivity)
    {
        var cutoffTime = DateTime.UtcNow - maxInactivity;
        var inactiveSessions = _activeSessions.Values
            .Where(s => s.LastActivity < cutoffTime)
            .ToList();

        foreach (var session in inactiveSessions)
        {
            _logger.LogInformation($"Cleaning up inactive session: {session.SessionId}");
            await EndSessionAsync(session.SessionId);
        }
    }
}

public class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public string IndexName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public List<string> ConversationHistory { get; set; } = new();
    public List<string> UploadedFiles { get; set; } = new();

    public TimeSpan ActiveDuration => DateTime.UtcNow - CreatedAt;
    public TimeSpan TimeSinceLastActivity => DateTime.UtcNow - LastActivity;

    public SessionInfo()
    {
        LastActivity = DateTime.UtcNow;
    }
} 