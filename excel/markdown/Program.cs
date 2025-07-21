using ExcelChatbot.Models;
using ExcelChatbot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ExcelChatbot;

class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var chatbotService = host.Services.GetRequiredService<ChatbotService>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();    

        await RunChatbotAsync(chatbotService, logger);
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Configure Azure OpenAI settings
                services.Configure<AzureOpenAISettings>(
                    context.Configuration.GetSection("AzureOpenAI"));

                // Register services
                services.AddSingleton<ExcelToMarkdownConverter>();
                services.AddSingleton<TokenManager>();
                services.AddSingleton<ContextSummarizer>();
                services.AddSingleton<AzureOpenAIService>();
                services.AddSingleton<FileManager>();
                services.AddSingleton<ChatbotService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });

    static async Task RunChatbotAsync(ChatbotService chatbotService, ILogger<Program> logger)
    {
        try
        {
            ShowWelcomeMessage();
            ShowHelp();

            while (true)
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.InputEncoding = System.Text.Encoding.UTF8;

                Console.Write("\nYour input: ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                // Handle commands
                if (input.StartsWith("/"))
                {
                    await HandleCommandAsync(input, chatbotService);
                    continue;
                }

                // Handle chat
                Console.WriteLine("\nThinking...");
                var response = await chatbotService.GetChatResponseAsync(input);
                
                Console.WriteLine($"\nAssistant: {response}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Application error");
            Console.WriteLine($"\nApplication error: {ex.Message}");
        }
    }

    static async Task HandleCommandAsync(string command, ChatbotService chatbotService)
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
                await chatbotService.ProcessExcelFileAsync(filePath);
                break;

            case "/status":
                chatbotService.ShowStatus();
                break;

            case "/clear":
                if (parts.Length > 1 && parts[1].ToLowerInvariant() == "all")
                {
                    chatbotService.ClearAllData();
                }
                else
                {
                    chatbotService.ClearConversationHistory();
                }
                break;

            case "/tokens":
                await chatbotService.ShowTokenInfoAsync();
                break;

            case "/exit":
            case "/quit":
                Console.WriteLine("Goodbye!");
                Environment.Exit(0);
                break;

            default:
                Console.WriteLine($"Unknown command: {command}");
                Console.WriteLine("Type /help to see available commands.");
                break;
        }
    }

    static void ShowWelcomeMessage()
    {
        Console.Clear();
        Console.WriteLine("Excel Financial Data Chatbot");
        Console.WriteLine("============================");
        Console.WriteLine();
        Console.WriteLine("Welcome! I can help you analyze financial data from Excel files.");
        Console.WriteLine("Upload your Excel files and ask questions about your data.");
        Console.WriteLine();
    }

    static void ShowHelp()
    {
        Console.WriteLine("Available Commands:");
        Console.WriteLine("  /upload <file_path>  - Upload and process an Excel file");
        Console.WriteLine("  /status             - Show current status and loaded files");
        Console.WriteLine("  /tokens             - Show token usage and limits information");
        Console.WriteLine("  /clear              - Clear conversation history");
        Console.WriteLine("  /clear all          - Clear all data and conversation history");
        Console.WriteLine("  /help               - Show this help message");
        Console.WriteLine("  /exit or /quit      - Exit the application");
        Console.WriteLine();
        Console.WriteLine("Usage Tips:");
        Console.WriteLine("  - Upload Excel files (.xlsx or .xls) using /upload command");
        Console.WriteLine("  - Ask questions about your data in natural language");
        Console.WriteLine("  - Reference specific tables, columns, or data points");
        Console.WriteLine("  - Ask for calculations, summaries, or insights");
        Console.WriteLine();
        Console.WriteLine("Configuration:");
        Console.WriteLine("  - Update appsettings.json with your Azure OpenAI credentials");
        Console.WriteLine("  - Ensure your deployment name matches your Azure setup");
        Console.WriteLine();
    }
} 