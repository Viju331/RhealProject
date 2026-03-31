# Rheal AI Project Inspector - Architecture Specification

## Project Overview

Rheal AI Project Inspector is an AI-powered platform that analyzes software repositories, understands business logic, enforces/generates coding standards, detects bugs, and provides UI-based reproduction steps.

## Technology Stack

### Frontend

- **Framework**: Angular 19 (latest) with standalone components
- **Styling**: Tailwind CSS
- **State Management**: Angular Signals & Services
- **HTTP Client**: Angular HttpClient
- **Architecture**: Modular, feature-based organization

### Backend

- **Framework**: .NET 8 Web API
- **Architecture**: Clean Architecture (API → Application → Domain → Infrastructure)
- **AI Integration**: Microsoft Agent Framework (OpenAI Client)
- **AI Model**: GitHub Models (gpt-4.1 for code analysis)
- **File Processing**: System.IO.Compression for ZIP handling
- **Pattern**: SOLID principles, CQRS pattern

### AI Components

- **Primary Model**: GitHub Models gpt-4.1 (excellent for code understanding)
- **Framework**: Microsoft Agent Framework (.NET)
- **Context**: Long-context analysis with multi-pass reasoning
- **Tools**: File analysis, pattern detection, standard enforcement

## Architecture Layers

### Backend Structure

```
src/
├── RhealAI.API/                    # Web API Layer
│   ├── Controllers/                 # API Controllers
│   │   ├── RepositoryController.cs  # Upload & Analysis
│   │   ├── AnalysisController.cs    # Results & Reports
│   │   └── StandardsController.cs   # Standards Management
│   ├── Middleware/                  # Custom Middleware
│   ├── Filters/                     # Global Filters
│   └── Program.cs                   # Entry Point
│
├── RhealAI.Application/            # Application Layer
│   ├── Services/                    # Business Logic Services
│   │   ├── RepositoryService.cs     # Repository Processing
│   │   ├── DocumentationService.cs  # .md File Analysis
│   │   ├── AIAnalysisService.cs     # AI Integration
│   │   ├── StandardsService.cs      # Standards Enforcement
│   │   ├── BugDetectionService.cs   # Bug Detection
│   │   └── ReportService.cs         # Report Generation
│   ├── DTOs/                        # Data Transfer Objects
│   ├── Interfaces/                  # Service Interfaces
│   ├── Prompts/                     # Centralized AI Prompts
│   │   ├── StandardsAnalysisPrompt.cs
│   │   ├── BugDetectionPrompt.cs
│   │   └── DocumentationPrompt.cs
│   └── Mappers/                     # Object Mappers
│
├── RhealAI.Domain/                 # Domain Layer
│   ├── Entities/                    # Domain Entities
│   │   ├── Repository.cs
│   │   ├── CodeFile.cs
│   │   ├── Standard.cs
│   │   ├── Violation.cs
│   │   ├── Bug.cs
│   │   └── AnalysisReport.cs
│   ├── Enums/                       # Domain Enums
│   │   ├── FileType.cs
│   │   ├── SeverityLevel.cs
│   │   └── ViolationType.cs
│   └── ValueObjects/                # Value Objects
│
└── RhealAI.Infrastructure/         # Infrastructure Layer
    ├── AI/                          # AI Integration
    │   ├── AgentFactory.cs          # Agent Creation
    │   ├── AgentTools.cs            # Custom Tools
    │   └── AIClientProvider.cs      # OpenAI Client Setup
    ├── FileProcessing/              # File Handling
    │   ├── ZipExtractor.cs
    │   ├── FileAnalyzer.cs
    │   └── FileFilters.cs
    ├── Persistence/                 # Data Persistence
    │   └── InMemoryCache.cs         # Simple caching
    └── Configuration/               # Config Helpers
```

### Frontend Structure

```
src/
├── app/
│   ├── core/                        # Core Module
│   │   ├── services/                # Core Services
│   │   │   ├── api.service.ts       # HTTP Service
│   │   │   ├── auth.service.ts      # Authentication
│   │   │   └── notification.service.ts
│   │   ├── interceptors/            # HTTP Interceptors
│   │   └── guards/                  # Route Guards
│   │
│   ├── shared/                      # Shared Module
│   │   ├── components/              # Reusable Components
│   │   │   ├── button/
│   │   │   ├── card/
│   │   │   ├── loader/
│   │   │   └── severity-badge/
│   │   ├── pipes/                   # Custom Pipes
│   │   └── models/                  # Shared Models
│   │
│   ├── features/                    # Feature Modules
│   │   ├── dashboard/               # Dashboard Feature
│   │   │   ├── components/
│   │   │   │   ├── dashboard.component.ts
│   │   │   │   ├── stats-card.component.ts
│   │   │   │   └── severity-chart.component.ts
│   │   │   └── services/
│   │   │       └── dashboard.service.ts
│   │   │
│   │   ├── upload/                  # Upload Feature
│   │   │   ├── components/
│   │   │   │   ├── upload.component.ts
│   │   │   │   └── upload-zone.component.ts
│   │   │   └── services/
│   │   │       └── upload.service.ts
│   │   │
│   │   ├── analysis/                # Analysis Feature
│   │   │   ├── components/
│   │   │   │   ├── analysis-results.component.ts
│   │   │   │   ├── violation-list.component.ts
│   │   │   │   ├── bug-list.component.ts
│   │   │   │   └── file-viewer.component.ts
│   │   │   └── services/
│   │   │       └── analysis.service.ts
│   │   │
│   │   ├── standards/               # Standards Feature
│   │   │   ├── components/
│   │   │   │   ├── standards-list.component.ts
│   │   │   │   └── standards-editor.component.ts
│   │   │   └── services/
│   │   │       └── standards.service.ts
│   │   │
│   │   └── reports/                 # Reports Feature
│   │       ├── components/
│   │       │   ├── report-view.component.ts
│   │       │   └── report-export.component.ts
│   │       └── services/
│   │           └── report.service.ts
│   │
│   ├── layouts/                     # Layout Components
│   │   ├── main-layout.component.ts
│   │   └── header.component.ts
│   │
│   └── app.component.ts             # Root Component
│
├── assets/                          # Static Assets
└── styles/                          # Global Styles
```

## Data Flow

### Repository Analysis Flow

1. **Upload**: User uploads ZIP/folder via Angular UI
2. **Extraction**: Backend extracts and filters files (ignore node_modules, bin, obj, .git, dist)
3. **Documentation Scan**: System reads all .md files first
4. **Standards Detection**:
   - If .md standards exist → Use them as source of truth
   - If missing → AI generates standards from codebase
5. **AI Analysis**:
   - Agent analyzes code structure
   - Detects violations against standards
   - Identifies bugs and logic issues
6. **Report Generation**: System creates detailed reports with:
   - File → Line → Violation/Bug
   - Severity levels (Critical, High, Medium, Low)
   - UI reproduction steps for bugs
7. **UI Display**: Dashboard shows results with drill-down capability

### AI Agent Workflow

```
┌─────────────────────────────────────────────────────────────┐
│                    AI Agent Architecture                     │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         Centralized Prompts Repository              │   │
│  │  - StandardsAnalysisPrompt                          │   │
│  │  - BugDetectionPrompt                               │   │
│  │  - DocumentationPrompt                              │   │
│  │  - ViolationDetectionPrompt                         │   │
│  └──────────────────────────────────────────────────────┘   │
│                          ▼                                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │            GitHub Models (gpt-4.1)                  │   │
│  │  - Long context window (1M tokens)                  │   │
│  │  - Code understanding & analysis                    │   │
│  │  - Multi-pass reasoning                             │   │
│  └──────────────────────────────────────────────────────┘   │
│                          ▼                                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Custom AI Tools                        │   │
│  │  - FileAnalysisTool (analyze code files)           │   │
│  │  - StandardComparisonTool (check standards)        │   │
│  │  - PatternDetectionTool (find patterns)            │   │
│  └──────────────────────────────────────────────────────┘   │
│                          ▼                                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Analysis Results                       │   │
│  │  - Violations (file, line, rule, severity)         │   │
│  │  - Bugs (description, impact, reproduction)        │   │
│  │  - Recommendations (fixes, improvements)           │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

## Key Features

### 1. Documentation-Aware Analysis

- Scans for existing .md files (coding standards, architecture, errors)
- Treats existing documentation as source of truth
- Only extends missing sections (clearly marked)

### 2. Smart Code Analysis

- Understands project structure and data flow
- Detects business logic patterns
- Identifies architectural violations

### 3. Bug Detection with Reproduction

- Finds logical issues and bugs
- Provides root cause analysis
- Generates exact UI steps to reproduce

### 4. Enterprise-Grade UI

- Clean, modern dashboard
- Severity-based filtering (Critical, High, Medium, Low)
- File-level drill-down
- Rule-level navigation
- Export capabilities

## SOLID Principles Implementation

### Single Responsibility

- Each service handles one specific concern
- Controllers only handle HTTP requests/responses
- Domain entities contain only business logic

### Open/Closed

- Extension points through interfaces
- Strategy pattern for different analysis types
- Plugin architecture for AI tools

### Liskov Substitution

- Interface-based dependency injection
- Polymorphic AI tool implementations

### Interface Segregation

- Small, focused interfaces
- Service-specific contracts

### Dependency Inversion

- Depend on abstractions (interfaces)
- Infrastructure depends on domain

## Security Considerations

- Input validation on file uploads
- File type restrictions
- Size limits on uploads
- Sanitization of file content
- No execution of uploaded code

## Performance Optimization

- Async/await throughout
- Streaming AI responses
- Lazy loading in Angular
- Pagination for large result sets
- Caching of analysis results

## Development Workflow

1. Backend development (Clean Architecture)
2. AI integration with Microsoft Agent Framework
3. Frontend development (Angular + Tailwind)
4. Integration testing
5. UI/UX refinement

## Next Steps

1. Set up .NET 8 Web API project structure
2. Implement clean architecture layers
3. Set up Angular project with Tailwind
4. Implement core services
5. Integrate AI agent with GitHub Models
6. Build UI components
7. Testing & refinement
