# Rheal AI Project Inspector

An AI-powered platform that analyzes software repositories, understands business logic, enforces/generates coding standards, detects bugs, and provides UI-based reproduction steps.

## 🌟 Features

- **Repository Analysis**: Upload ZIP files containing your codebase for comprehensive analysis
- **Smart Documentation Processing**:
  - Reads existing .md files as source of truth
  - Generates standards from codebase if documentation is missing
- **AI-Powered Detection**:
  - Coding standard violations
  - Bug detection with root cause analysis
  - UI reproduction steps for bugs
- **Enterprise Dashboard**: Clean, modern UI with severity-based filtering and drill-down capabilities
- **Detailed Reports**: Exportable JSON reports with actionable fixes

## 🏗️ Architecture

### Backend (.NET 8 Web API)

- **Clean Architecture**: API → Application → Domain → Infrastructure
- **AI Integration**: Microsoft Agent Framework with GitHub Models (gpt-4.1)
- **File Processing**: ZIP extraction with intelligent file filtering
- **In-Memory Caching**: Fast data retrieval

### Frontend (Angular 19 + Tailwind CSS)

- **Standalone Components**: Modern Angular architecture
- **Feature-Based Organization**: Modular, maintainable structure
- **Responsive Design**: Enterprise-grade UI with Tailwind CSS

## 📋 Prerequisites

- **.NET 8 SDK** (version 8.0 or later)
- **Node.js** (version 18 or later) for Angular
- **GitHub Personal Access Token** for AI model access

## 🚀 Getting Started

### 1. Configure GitHub Token

1. Create a GitHub Personal Access Token (PAT) with appropriate permissions
2. Open `src/RhealAI.API/appsettings.json`
3. Replace `YOUR_GITHUB_TOKEN_HERE` with your actual token:

```json
{
  "GitHub": {
    "Token": "github_pat_your_actual_token_here",
    "Model": "openai/gpt-4.1"
  }
}
```

**Important**: Never commit your actual token to source control. Consider using environment variables or user secrets for production.

### 2. Run the Backend

```powershell
# Navigate to API project
cd src\RhealAI.API

# Restore dependencies
dotnet restore

# Run the application
dotnet run
```

The API will start at `https://localhost:5001` (or the port shown in console).

### 3. Test the API

Use the provided HTTP file or tools like Postman:

#### Upload Repository

```http
POST https://localhost:5001/api/repository/upload
Content-Type: multipart/form-data

file: [your-repository.zip]
```

#### Start Analysis

```http
POST https://localhost:5001/api/analysis/{repositoryId}/analyze
```

#### Get Report

```http
GET https://localhost:5001/api/analysis/report/{reportId}
```

## 📝 API Endpoints

### Repository Management

- `POST /api/repository/upload` - Upload repository ZIP file
- `GET /api/repository/{id}` - Get repository details

### Analysis

- `POST /api/analysis/{repositoryId}/analyze` - Start code analysis
- `GET /api/analysis/report/{reportId}` - Get full analysis report
- `GET /api/analysis/report/{reportId}/export/json` - Export report as JSON

### Standards

- `GET /api/standards/repository/{repositoryId}` - Get coding standards for repository

## 🧠 How It Works

### 1. Repository Upload

- User uploads a ZIP file containing their codebase
- System extracts and filters files (ignores `node_modules`, `bin`, `obj`, `.git`, `dist`)
- Files are categorized by type (.cs, .ts, .js, .html, .css, .md, etc.)

### 2. Standards Detection

- **If .md files exist**: System reads and extracts coding standards, architecture rules, error handling guidelines
- **If no documentation**: AI analyzes codebase and generates standards based on observed patterns

### 3. AI Analysis

- **Violation Detection**: Compares code against standards, identifies violations with line numbers
- **Bug Detection**: Identifies logical errors, security vulnerabilities, performance issues
- **Reproduction Steps**: For UI bugs, generates exact steps to reproduce from the user interface

### 4. Report Generation

- Compiles all findings with severity levels (Critical, High, Medium, Low)
- Provides suggested fixes for each issue
- Organizes results by file, line number, and type

## 🤖 AI Model Information

This project uses **GitHub Models** with the **gpt-4.1** model:

- **Context Window**: 1M tokens (excellent for large codebases)
- **Capabilities**: Advanced code understanding, multi-pass reasoning
- **Cost**: Free tier available for development
- **Endpoint**: `https://models.github.ai/inference`

### Why gpt-4.1?

- Excellent code comprehension and analysis
- Long context window for entire repository analysis
- Strong reasoning for bug detection and root cause analysis
- High quality standards generation

## 🏢 Clean Architecture

```
src/
├── RhealAI.API/              # Controllers, Program.cs (Presentation Layer)
├── RhealAI.Application/      # Services, Interfaces, DTOs (Business Logic)
├── RhealAI.Domain/           # Entities, Enums, Value Objects (Core)
└── RhealAI.Infrastructure/   # AI Integration, File Processing, Persistence
```

### Layer Responsibilities

**Domain**: Core business entities and rules (no dependencies)
**Application**: Business logic, service interfaces, centralized AI prompts
**Infrastructure**: AI agents, file processing, external integrations
**API**: HTTP endpoints, request/response handling

## 📦 NuGet Packages

The following packages are used:

- `Microsoft.Agents.AI.OpenAI` (1.0.0-preview.251219.1) - Microsoft Agent Framework
- `OpenAI` (2.8.0) - OpenAI client for model interaction
- `Microsoft.Extensions.AI` - AI abstractions and utilities

**Note**: The `--prerelease` flag is required for Microsoft.Agents packages as they are in preview.

## 🔒 Security Considerations

- **File Upload Limits**: 500 MB maximum
- **File Type Validation**: Only ZIP files accepted
- **No Code Execution**: Uploaded code is never executed
- **Token Storage**: Use environment variables or Azure Key Vault in production

## 🎯 Project Status

### ✅ Completed

- Clean architecture backend structure
- Domain entities and enums
- AI agent integration with Microsoft Agent Framework
- File processing and ZIP extraction
- Repository upload and analysis services
- Standards extraction and generation
- Bug detection and violation analysis
- API controllers and endpoints
- Centralized AI prompts
- In-memory caching

### 🚧 Pending

- Angular frontend implementation
- Dashboard UI components
- PDF report export
- Additional file type support
- Unit and integration tests

## 📚 Next Steps

1. **Set Up Frontend**:

   ```bash
   ng new rheal-ui --standalone --routing --style scss
   npm install -D tailwindcss postcss autoprefixer
   npx tailwindcss init
   ```

2. **Implement Dashboard**: Create Angular components for upload, analysis, and reporting

3. **Add Authentication**: Implement user authentication and authorization

4. **Database Integration**: Replace in-memory cache with persistent storage (SQL Server, PostgreSQL)

5. **Testing**: Add comprehensive unit and integration tests

6. **Deployment**: Deploy to Azure App Service or similar platform

## 🤝 Contributing

This project follows SOLID principles and clean architecture patterns. When contributing:

1. Keep services small and focused (Single Responsibility)
2. Use interfaces for dependencies (Dependency Inversion)
3. Follow existing naming conventions
4. Add XML documentation comments
5. Maintain layer separation

## 📄 License

[Add your license information here]

## 🆘 Support

For issues or questions:

1. Check the logs in the console output
2. Verify GitHub token configuration
3. Ensure .NET 8 SDK is properly installed
4. Review the PROJECT_ARCHITECTURE.md for detailed design information

## 🔗 Useful Links

- [Microsoft Agent Framework Documentation](https://github.com/microsoft/agent-framework)
- [GitHub Models](https://github.com/marketplace/models)
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [Angular Documentation](https://angular.io/docs)
