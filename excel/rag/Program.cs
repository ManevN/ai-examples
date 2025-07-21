using ExcelRAGChatbot.Models;
using ExcelRAGChatbot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExcelRAGChatbot;

class Program
{
    private static string? _currentSessionId;
    
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var sessionManager = host.Services.GetRequiredService<SessionManager>();
        var excelConverter = host.Services.GetRequiredService<ExcelToMarkdownConverter>();
        var searchService = host.Services.GetRequiredService<AzureSearchService>();
        var ragService = host.Services.GetRequiredService<RAGService>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        
        await RunChatbotAsync(sessionManager, excelConverter, searchService, ragService, logger);
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Configure settings
                services.Configure<AzureOpenAISettings>(
                    context.Configuration.GetSection("AzureOpenAI"));
                services.Configure<AzureSearchSettings>(
                    context.Configuration.GetSection("AzureSearch"));

                // Register services
                services.AddSingleton<ExcelToMarkdownConverter>();
                services.AddSingleton<AzureSearchService>();
                services.AddSingleton<RAGService>();
                services.AddSingleton<SessionManager>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });

    static async Task RunChatbotAsync(
        SessionManager sessionManager,
        ExcelToMarkdownConverter excelConverter,
        AzureSearchService searchService,
        RAGService ragService,
        ILogger<Program> logger)
    {
        try
        {
            ShowWelcomeMessage();
            ShowHelp();

            // Create session
            _currentSessionId = await sessionManager.CreateSessionAsync();
            Console.WriteLine($"Started session: {_currentSessionId}");
            Console.WriteLine("Upload Excel files and start chatting!");
            Console.WriteLine();

            while (true)
            {
                Console.Write("Your input: ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                // Handle commands
                if (input.StartsWith("/"))
                {
                    await HandleCommandAsync(input, sessionManager, excelConverter, searchService, ragService);
                    continue;
                }

                // Handle chat
                var session = sessionManager.GetSession(_currentSessionId!);
                if (session == null)
                {
                    Console.WriteLine("Session not found. Please restart the application.");
                    break;
                }

                Console.WriteLine("Thinking...");
                var response = await ragService.GetAnswerAsync(session.IndexName, input, session.ConversationHistory);
                
                Console.WriteLine($"\nAssistant: {response}");
                
                // Update conversation history
                sessionManager.AddConversationToSession(_currentSessionId!, input, response);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Application error");
            Console.WriteLine($"\nApplication error: {ex.Message}");
        }
        finally
        {
            // Cleanup session on exit
            if (!string.IsNullOrEmpty(_currentSessionId))
            {
                await sessionManager.EndSessionAsync(_currentSessionId);
                Console.WriteLine("Session cleaned up. Goodbye!");
            }
        }
    }

    static async Task HandleCommandAsync(
        string command,
        SessionManager sessionManager,
        ExcelToMarkdownConverter excelConverter,
        AzureSearchService searchService,
        RAGService ragService)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();

        switch (cmd)
        {
            case "/help":
                ShowHelp();
                break;

            case "/upload":
                if (parts.Length < 2)
                {
                    Console.WriteLine("Usage: /upload <file_path>");
                    break;
                }
                var filePath = string.Join(" ", parts.Skip(1));
                await ProcessExcelFileAsync(filePath, excelConverter, searchService, sessionManager);
                break;

            case "/status":
                ShowStatus(sessionManager);
                break;

            case "/sessions":
                ShowAllSessions(sessionManager);
                break;

            case "/cleanup":
                await sessionManager.CleanupInactiveSessionsAsync(TimeSpan.FromHours(1));
                Console.WriteLine("Cleaned up inactive sessions.");
                break;

            case "/end":
                if (!string.IsNullOrEmpty(_currentSessionId))
                {
                    await sessionManager.EndSessionAsync(_currentSessionId);
                    Console.WriteLine("Session ended. Starting new session...");
                    _currentSessionId = await sessionManager.CreateSessionAsync();
                    Console.WriteLine($"New session: {_currentSessionId}");
                }
                break;

            case "/exit":
            case "/quit":
                if (!string.IsNullOrEmpty(_currentSessionId))
                {
                    await sessionManager.EndSessionAsync(_currentSessionId);
                }
                Console.WriteLine("Goodbye!");
                Environment.Exit(0);
                break;

            default:
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine("Type /help to see available commands.");
                break;
        }
    }

    static async Task ProcessExcelFileAsync(
        string filePath,
        ExcelToMarkdownConverter excelConverter,
        AzureSearchService searchService,
        SessionManager sessionManager)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
            {
                Console.WriteLine("Only .xlsx and .xls files are supported.");
                return;
            }

            var session = sessionManager.GetSession(_currentSessionId!);
            if (session == null)
            {
                Console.WriteLine("No active session. Please restart the application.");
                return;
            }

            Console.WriteLine($"Processing: {Path.GetFileName(filePath)}...");
            
            // Convert Excel to chunks
            var chunks = excelConverter.ConvertToChunks(filePath, _currentSessionId!);
            
            if (!chunks.Any())
            {
                Console.WriteLine("No data found in the Excel file.");
                return;
            }

            // Index the chunks
            await searchService.IndexDocumentsAsync(session.IndexName, chunks);
            
            // Update session
            sessionManager.AddFileToSession(_currentSessionId!, Path.GetFileName(filePath));
            
            Console.WriteLine($"Successfully processed: {Path.GetFileName(filePath)} ({chunks.Count} tables indexed)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {Path.GetFileName(filePath)}: {ex.Message}");
        }
    }

    static void ShowStatus(SessionManager sessionManager)
    {
        var session = sessionManager.GetSession(_currentSessionId!);
        if (session == null)
        {
            Console.WriteLine("No active session.");
            return;
        }

        Console.WriteLine("\nCurrent Session Status:");
        Console.WriteLine($"   Session ID: {session.SessionId}");
        Console.WriteLine($"   Files uploaded: {session.UploadedFiles.Count}");
        
        if (session.UploadedFiles.Any())
        {
            Console.WriteLine("   Uploaded files:");
            foreach (var fileName in session.UploadedFiles)
            {
                Console.WriteLine($"      - {fileName}");
            }
        }
        
        Console.WriteLine($"   Conversation exchanges: {session.ConversationHistory.Count / 2}");
        Console.WriteLine($"   Session duration: {session.ActiveDuration:hh\\:mm\\:ss}");
        Console.WriteLine();
    }

    static void ShowAllSessions(SessionManager sessionManager)
    {
        var sessions = sessionManager.GetActiveSessions();
        
        Console.WriteLine($"\nActive Sessions ({sessions.Count}):");
        foreach (var session in sessions)
        {
            Console.WriteLine($"   {session.SessionId} - {session.UploadedFiles.Count} files, {session.ConversationHistory.Count / 2} exchanges");
        }
        Console.WriteLine();
    }

    static void ShowWelcomeMessage()
    {
        Console.Clear();
        Console.WriteLine("Excel Financial Data RAG Chatbot");
        Console.WriteLine("=================================");
        Console.WriteLine();
        Console.WriteLine("This chatbot uses Azure AI Search for intelligent retrieval of your Excel data.");
        Console.WriteLine("Upload your Excel files and ask questions about your financial data.");
        Console.WriteLine();
    }

    static void ShowHelp()
    {
        Console.WriteLine("Available Commands:");
        Console.WriteLine("  /upload <file_path>  - Upload and index an Excel file");
        Console.WriteLine("  /status             - Show current session status");
        Console.WriteLine("  /sessions           - Show all active sessions");
        Console.WriteLine("  /cleanup            - Clean up inactive sessions");
        Console.WriteLine("  /end                - End current session and start a new one");
        Console.WriteLine("  /help               - Show this help message");
        Console.WriteLine("  /exit or /quit      - Exit the application");
        Console.WriteLine();
        Console.WriteLine("Usage Tips:");
        Console.WriteLine("  - Upload Excel files (.xlsx or .xls) using /upload command");
        Console.WriteLine("  - Ask questions about your data in natural language");
        Console.WriteLine("  - Each session creates its own search index");
        Console.WriteLine("  - Indexes are automatically deleted when sessions end");
        Console.WriteLine();
        Console.WriteLine("Configuration:");
        Console.WriteLine("  - Update appsettings.json with your Azure OpenAI and Search credentials");
        Console.WriteLine("  - Ensure your Azure Search service is running");
        Console.WriteLine();
    }
} 