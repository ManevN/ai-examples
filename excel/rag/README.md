# Excel Financial Data RAG Chatbot

A C#/.NET console application that uses Azure AI Search and Azure OpenAI to provide intelligent Q&A over Excel financial data through a Retrieval-Augmented Generation (RAG) approach.

## Architecture Overview

This application implements a **session-based RAG architecture**:

1. **Excel Processing**: Converts Excel files to structured markdown chunks
2. **Indexing**: Each user session gets its own Azure AI Search index
3. **Retrieval**: User questions trigger semantic search across indexed content
4. **Generation**: Retrieved context is provided to Azure OpenAI for response generation
5. **Cleanup**: Session indexes are automatically deleted when conversations end

## Key Features

- **Session-Based Indexing**: Each user session creates an isolated search index
- **Intelligent Chunking**: Preserves table structure while creating searchable chunks
- **Smart Filtering**: Automatic filtering based on data types (currency, dates, numeric)
- **Multi-file Support**: Index and search across multiple Excel files simultaneously
- **Automatic Cleanup**: Indexes are deleted when sessions end to save costs
- **Rich Metadata**: Chunks include column headers, data types, and file information

## Prerequisites

- **.NET 8.0 SDK** or later
- **Azure OpenAI Service** with a deployed GPT-4 or GPT-4o-mini model
- **Azure AI Search Service** (Basic tier or higher recommended)
- **Visual Studio 2022** or **Visual Studio Code** (optional but recommended)

## Setup Instructions

### 1. Azure Services Setup

#### Azure AI Search
1. Create an Azure AI Search service in the Azure portal
2. Note the service name and admin key
3. Ensure the service is in a supported region

#### Azure OpenAI
1. Create an Azure OpenAI resource
2. Deploy a GPT-4 or GPT-4o-mini model
3. Note the endpoint, API key, and deployment name

### 2. Configure Application Settings

Update `appsettings.json` with your Azure credentials:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource-name.openai.azure.com/",
    "ApiKey": "your-openai-api-key-here",
    "DeploymentName": "gpt-4",
    "ApiVersion": "2024-06-01"
  },
  "AzureSearch": {
    "ServiceName": "your-search-service-name",
    "AdminKey": "your-search-admin-key-here",
    "IndexPrefix": "excel-rag"
  }
}
```

### 3. Build and Run

```bash
cd rag
dotnet restore
dotnet build
dotnet run
```

## Usage Guide

### Starting the Application

When you start the application:
1. A new session is automatically created
2. A unique Azure AI Search index is created for your session
3. You can start uploading Excel files and asking questions

### Commands

| Command | Description |
|---------|-------------|
| `/upload <file_path>` | Upload and index an Excel file |
| `/status` | Show current session status |
| `/sessions` | Show all active sessions |
| `/cleanup` | Clean up inactive sessions |
| `/end` | End current session and start a new one |
| `/help` | Show available commands |
| `/exit` or `/quit` | Exit and clean up session |

### Example Session

```
Excel Financial Data RAG Chatbot
=================================

Started session: a1b2c3d4
Upload Excel files and start chatting!

Your input: /upload C:\Data\financial_report.xlsx
Processing: financial_report.xlsx...
Successfully processed: financial_report.xlsx (3 tables indexed)

Your input: What's the total revenue this quarter?

Thinking... 