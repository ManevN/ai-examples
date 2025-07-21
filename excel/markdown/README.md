# Excel Financial Data Chatbot

A C#/.NET console application that enables users to upload Excel files containing financial data and interact with them through a chatbot interface powered by Azure OpenAI (GPT-4/GPT-4o-mini).

## Features

- **Excel File Processing**: Supports both .xlsx and .xls file formats
- **Intelligent Table Detection**: Automatically detects and preserves multiple table structures within Excel sheets
- **Markdown Conversion**: Converts Excel data to structured markdown while maintaining table relationships
- **Azure OpenAI Integration**: Uses GPT-4 or GPT-4o-mini for natural language interactions
- **Conversation History**: Maintains context across multiple questions
- **Multi-file Support**: Process and analyze multiple Excel files simultaneously
- **Command Interface**: Easy-to-use command-based interface
- **Smart Token Management**: Automatically handles large files and token limits
- **Intelligent Context Optimization**: Uses multiple strategies to fit data within model limits
- **Query-Specific Summarization**: Focuses on relevant data based on user questions

## Prerequisites

- **.NET 8.0 SDK** or later
- **Azure OpenAI Service** with a deployed GPT-4 or GPT-4o-mini model
- **Visual Studio 2022** or **Visual Studio Code** (optional but recommended)

## Setup Instructions

### 1. Clone or Download the Project

Download the project files to your local machine.

### 2. Configure Azure OpenAI Settings

1. Open `appsettings.json`
2. Replace the placeholder values with your Azure OpenAI credentials:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource-name.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "DeploymentName": "gpt-4",
    "ApiVersion": "2024-06-01"
  }
}
```

**How to get these values:**
- **Endpoint**: From your Azure OpenAI resource overview page
- **ApiKey**: From your Azure OpenAI resource "Keys and Endpoint" section
- **DeploymentName**: The name you gave to your model deployment in Azure OpenAI Studio
- **ApiVersion**: Use "2024-06-01" (or the latest stable version)

### 3. Restore Dependencies and Build

```bash
dotnet restore
dotnet build
```

### 4. Run the Application

```bash
dotnet run
```

## Usage Guide

### Starting the Application

When you run the application, you'll see a welcome screen with available commands.

### Commands

| Command | Description |
|---------|-------------|
| `/upload <file_path>` | Upload and process an Excel file |
| `/status` | Show current status and loaded files |
| `/tokens` | Show token usage and limits information |
| `/clear` | Clear conversation history |
| `/clear all` | Clear all data and conversation history |
| `/help` | Show available commands |
| `/exit` or `/quit` | Exit the application |

### Example Usage Session

```
Excel Financial Data Chatbot
=============================

Your input: /upload C:\Data\financial_report.xlsx
Successfully processed: financial_report.xlsx

Your input: /upload C:\Data\budget_2024.xlsx
Successfully processed: budget_2024.xlsx

Your input: What's the total revenue in the financial report?

Thinking...
3. **Markdown Conversion**: Each table is converted to markdown format while preserving relationships
4. **Token Management**: Smart optimization ensures data fits within model token limits
5. **Context Building**: All markdown content is optimized and combined into a comprehensive context
6. **AI Analysis**: Azure OpenAI analyzes the context and responds to user questions
7. **Conversation Flow**: The system maintains conversation history for context-aware responses

### Token Limit Handling

The application intelligently manages token limits using multiple strategies:

- **Relevance-Based Filtering**: Extracts sections most relevant to your query
- **Table Compression**: Samples representative data rows while preserving structure
- **Query-Specific Summarization**: Creates intelligent summaries focused on your question
- **Conversation History Optimization**: Keeps recent context while managing memory

Use the `/tokens` command to see current usage and get optimization tips.

## Architecture

```
ExcelChatbot/
├── Models/
│   └── AzureOpenAISettings.cs          # Configuration model
├── Services/
│   ├── ExcelToMarkdownConverter.cs     # Excel parsing and conversion
│   ├── AzureOpenAIService.cs           # Azure OpenAI integration
│   ├── FileManager.cs                  # File management and storage
│   └── ChatbotService.cs               # Main orchestration service
├── Program.cs                          # Application entry point and UI
├── appsettings.json                    # Configuration file
└── ExcelChatbot.csproj                 # Project file with dependencies
```

## Key Dependencies

- **DocumentFormat.OpenXml**: For reading Excel files
- **Azure.AI.OpenAI**: For Azure OpenAI integration
- **Microsoft.Extensions.***:  For dependency injection, configuration, and logging

## Error Handling

The application includes comprehensive error handling for:
- Invalid file formats
- Corrupted Excel files
- Azure OpenAI API errors
- Network connectivity issues
- Missing configuration

## Security Considerations

- Store your Azure OpenAI API key securely
- Don't commit `appsettings.json` with real credentials to version control
- Consider using Azure Key Vault for production deployments
- Be mindful of data privacy when uploading sensitive financial information

## Limitations

- Currently processes only the first worksheet in each Excel file
- Optimal for files with clear tabular structures
- Very large files may require context optimization (handled automatically)
- Requires internet connection for Azure OpenAI API calls
- Token estimation is approximate (actual usage may vary)

## Troubleshooting

### Common Issues

1. **"Invalid Excel file" error**:
   - Ensure the file is a valid .xlsx or .xls format
   - Check that the file is not corrupted
   - Verify the file path is correct

2. **Azure OpenAI connection errors**:
   - Verify your API key and endpoint in `appsettings.json`
   - Check your Azure OpenAI deployment name
   - Ensure your Azure subscription has sufficient credits

3. **"No Excel files uploaded" message**:
   - Use the `/upload` command before asking questions
   - Verify files were processed successfully

4. **Context compression warnings**:
   - This is normal for large files and helps manage token limits
   - Ask more specific questions to get detailed information
   - Use `/tokens` to see current optimization status

## Contributing

Feel free to contribute improvements, bug fixes, or new features to this project.

## License

This project is provided as-is for educational and commercial use. 