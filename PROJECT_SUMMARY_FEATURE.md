# AI-Generated Project Summary Feature

## Overview

This feature adds comprehensive AI-powered project analysis that reads folder structure, understands business logic, and generates detailed project descriptions. The AI analyzes the entire codebase and presents an intelligent summary on the dashboard.

## What Was Added

### 1. Backend - Domain Layer

#### ProjectSummary Entity (`RhealAI.Domain/Entities/ProjectSummary.cs`)

New entity that stores comprehensive project analysis:

- **ProjectName**: Name of the analyzed project
- **Description**: AI-generated project description
- **TechnologyStack**: Detected technologies (e.g., ".NET, TypeScript, Angular")
- **Architecture**: Architecture pattern (e.g., "Clean Architecture (DDD)", "MVC", "Service-Oriented")
- **BusinessLogic**: Core business logic areas (e.g., "Code Analysis, Reporting, Data Management")
- **CoreFunctionality**: Main functionalities (e.g., "RESTful API Services, Database Operations")
- **KeyFeatures**: List of key features (e.g., "AI-Powered Analysis", "Real-Time Communication")
- **FolderStructure**: Dictionary of folder paths and file counts
- **FileTypeDistribution**: Dictionary of file types and their counts
- **MainComponents**: List of main component folders
- **PrimaryLanguage**: Primary programming language
- **Dependencies**: List of detected dependencies

#### AnalysisReport Update

- Added `ProjectSummary? ProjectSummary` property to include AI-generated project analysis in reports

### 2. Backend - Infrastructure Layer

#### AIAnalysisService (`RhealAI.Infrastructure/Services/AIAnalysisService.cs`)

New method: `AnalyzeProjectStructureAsync()`

- Analyzes project folder structure
- Detects technology stack (supports 30+ languages)
- Identifies architecture patterns
- Extracts business logic keywords
- Identifies key features
- Maps main components
- Extracts dependencies from package.json, .csproj, etc.

**Technology Stack Detection:**

- Backend: .NET, Java, Python, PHP, Ruby, Go, Rust
- Frontend: TypeScript, JavaScript, Angular, React, Vue.js
- Database: SQL Database detection

**Architecture Pattern Detection:**

- Clean Architecture (DDD)
- MVC (Model-View-Controller)
- Service-Oriented Architecture
- Component-Based Architecture
- Layered Architecture

**Business Logic Extraction:**
Detects domains like:

- Code Analysis, Reporting, Data Management
- Security & Authentication, Access Control
- Payment Processing, Order Management
- User Management, Product Catalog
- Workflow Management, Task Management

#### ReportService Update

- Integrated project analysis step (progress 20-22%)
- Generates project summary with AI before analyzing violations
- Passes projectSummary to AnalysisReport

#### IAIAnalysisService Interface

- Added `AnalyzeProjectStructureAsync()` method signature

### 3. Frontend - Models

#### analysis-report.model.ts

New interface: `ProjectSummary`

```typescript
interface ProjectSummary {
  projectName: string;
  description: string;
  technologyStack: string;
  architecture: string;
  businessLogic: string;
  coreFunctionality: string;
  keyFeatures: string[];
  folderStructure: { [key: string]: number };
  fileTypeDistribution: { [key: string]: number };
  mainComponents: string[];
  primaryLanguage: string;
  dependencies: string[];
}
```

Updated `AnalysisReport` interface:

- Added `projectSummary?: ProjectSummary` property

### 4. Frontend - Dashboard Component

#### dashboard-page.component.ts

- Updated report parsing to include `projectSummary` from API response
- Added safe navigation for projectSummary properties

#### dashboard-page.component.html

New "AI-Generated Project Summary" card displayed at top of dashboard:

**Summary Sections:**

1. **Description** - Full AI-generated project description
2. **Architecture Grid** - 4-item grid showing:
   - Architecture pattern
   - Technology stack
   - Primary language
   - Business logic
3. **Key Features** - Chips with checkmark icons
4. **Main Components** - Tags showing folder names and file counts
5. **Dependencies** - Chips showing detected dependencies

#### dashboard-page.component.scss

New styles for project summary:

- `.project-summary-card` - Gradient background with purple/indigo theme
- `.project-summary-header` - Large icon and title
- `.summary-section` - Organized sections with icons
- `.summary-grid` - 2-column grid for architecture items
- `.features-chips` - Rounded chips with icons
- `.components-list` - Component tags
- `.dependencies-list` - Dependency chips with indigo theme

## How It Works

### Analysis Flow

1. **Upload Project** → User uploads ZIP/Folder or clones from GitHub
2. **Extract Files** → System extracts and categorizes all files (150+ extensions supported)
3. **Folder Analysis** → System maps folder structure and counts files
4. **AI Analysis** (Progress 20-22%):
   - Technology stack detection
   - Architecture pattern recognition
   - Business logic extraction
   - Feature identification
   - Dependency extraction
5. **Generate Summary** → AI creates comprehensive project description
6. **Standards Analysis** → Continues with existing workflow
7. **Display Results** → Project summary shown at top of dashboard

### Example Output

**RhealAI Project Summary:**

- **Description**: "A Clean Architecture (DDD)-based application built with .NET, TypeScript, Angular. The project implements Code Analysis, Reporting, Data Management functionality with a focus on maintainability and scalability."
- **Architecture**: Clean Architecture (DDD)
- **Technology Stack**: .NET, TypeScript, Angular
- **Primary Language**: CSharp
- **Business Logic**: Code Analysis, Reporting, Data Management
- **Key Features**:
  - AI-Powered Analysis
  - Real-Time Communication
  - File Upload & Processing
  - Git Repository Integration
  - Comprehensive Reporting
  - Analytics Dashboard
- **Main Components**:
  - Controllers (3 files)
  - Services (7 files)
  - Entities (6 files)
  - Components (12 files)
- **Dependencies**:
  - Entity Framework Core
  - Swagger/OpenAPI
  - SignalR
  - Angular Framework

## User Benefits

1. **Instant Understanding**: Immediately see what the project does without reading code
2. **Architecture Insight**: Understand the project structure and design patterns
3. **Technology Overview**: See all technologies used at a glance
4. **Business Logic**: Understand core business domains and functionality
5. **Feature Discovery**: Identify key capabilities without documentation
6. **Dependency Awareness**: Know what external libraries are used

## Real-Time Progress Updates

During analysis, users see:

- Progress 7%: "Analyzing project structure and business logic..."
- Progress 20%: "Analyzing business logic and generating project summary..."
- Progress 22%: "Project Analysis: Clean Architecture (DDD) architecture detected"

## Supported Project Types

The AI can analyze projects in:

- **.NET**: C#, VB.NET, F#, ASP.NET
- **Java**: Java, Kotlin, Scala, Groovy
- **JavaScript/TypeScript**: Node.js, Angular, React, Vue.js
- **Python**: Django, Flask, FastAPI
- **PHP**: Laravel, Symfony, WordPress
- **Ruby**: Rails, Sinatra
- **Go**: Go microservices
- **Rust**: Rust applications
- **C/C++**: Native applications
- **Swift**: iOS applications
- **Dart**: Flutter applications
- **Mobile**: Android, iOS, Flutter

## Technical Details

### Backend Analysis Methods

1. **DetectTechnologyStack()**: Scans fileTypeDistribution and file contents
2. **DetectArchitecturePattern()**: Analyzes folder structure patterns
3. **ExtractBusinessLogic()**: Keyword matching in folders and files
4. **GenerateCoreFunctionality()**: Content analysis for API, DB, Auth patterns
5. **IdentifyKeyFeatures()**: Pattern matching for AI, SignalR, File processing, Git
6. **IdentifyMainComponents()**: Lists folders with >5 files
7. **ExtractDependencies()**: Parses package.json, .csproj for dependencies

### UI Components

- **Glass Card Design**: Translucent background with gradients
- **Material Icons**: Visual indicators for each section
- **Responsive Grid**: 2-column layout on desktop, 1-column on mobile
- **Hover Effects**: Shadows and border changes on hover
- **Color Coding**:
  - Purple: Architecture and features
  - Green: Success indicators
  - Indigo: Dependencies

## Configuration

No configuration needed - works in Demo mode by default. When using real AI providers:

- Set `AI:Provider` to "Azure" or "OpenAI"
- AI will analyze file contents and generate more detailed descriptions
- Falls back to rule-based analysis if AI fails

## Future Enhancements

Potential improvements:

1. **Deeper Code Analysis**: Parse actual code patterns and design principles
2. **Security Analysis**: Identify security patterns and vulnerabilities
3. **Performance Insights**: Detect performance patterns and bottlenecks
4. **Documentation Quality**: Assess documentation coverage
5. **Test Coverage**: Analyze test files and coverage
6. **Complexity Metrics**: Calculate cyclomatic complexity and code quality metrics
7. **Migration Recommendations**: Suggest modernization opportunities
8. **AI Chat**: Allow users to ask questions about the project

## Testing

To test the feature:

1. Press F5 to start the API
2. Navigate to `http://localhost:4200`
3. Upload a project (ZIP, folder, or GitHub URL)
4. Wait for analysis to complete
5. View the AI-Generated Project Summary card at the top of the dashboard
6. Verify all sections are populated with accurate information

## Files Modified/Created

### Created:

- `RhealAI.Domain/Entities/ProjectSummary.cs`
- `PROJECT_SUMMARY_FEATURE.md`

### Modified:

- `RhealAI.Domain/Entities/AnalysisReport.cs`
- `RhealAI.Application/Interfaces/IAIAnalysisService.cs`
- `RhealAI.Infrastructure/Services/AIAnalysisService.cs`
- `RhealAI.Infrastructure/Services/ReportService.cs`
- `RhealAI.Web/src/app/models/analysis-report.model.ts`
- `RhealAI.Web/src/app/features/dashboard/dashboard-page/dashboard-page.component.ts`
- `RhealAI.Web/src/app/features/dashboard/dashboard-page/dashboard-page.component.html`
- `RhealAI.Web/src/app/features/dashboard/dashboard-page/dashboard-page.component.scss`
