using RhealAI.Domain.Entities;
using RhealAI.Domain.Enums;
using RhealAI.Infrastructure.FileProcessing;
using System.Text.RegularExpressions;

namespace RhealAI.Infrastructure.Services;

/// <summary>
/// Service for generating coding standards based on actual project analysis
/// </summary>
public class StandardsGeneratorService
{
    private readonly FolderStructureAnalyzer _structureAnalyzer;

    public StandardsGeneratorService()
    {
        _structureAnalyzer = new FolderStructureAnalyzer();
    }

    /// <summary>
    /// Generates standards based on actual code analysis and project structure
    /// </summary>
    public List<Standard> GenerateStandardsByTechStack(List<CodeFile> files)
    {
        var standards = new List<Standard>();
        
        // Analyze the actual project structure and code patterns
        var projectAnalysis = AnalyzeProjectStructure(files);
        var codePatterns = AnalyzeCodePatterns(files);
        
        var techStacks = DetectTechStacks(files);

        foreach (var techStack in techStacks)
        {
            standards.AddRange(GenerateStandardsForTechStack(techStack, projectAnalysis, codePatterns, files));
        }

        return standards;
    }

    private HashSet<string> DetectTechStacks(List<CodeFile> files)
    {
        var techStacks = new HashSet<string>();

        var fileTypes = files.Select(f => f.FileType).Distinct().ToList();
        var fileNames = files.Select(f => f.FileName.ToLower()).ToList();
        var filePaths = files.Select(f => f.FilePath.ToLower()).ToList();

        // Detect API/Backend
        if (fileTypes.Contains(FileType.CSharp))
        {
            if (fileNames.Any(f => f.Contains("controller") || f.Contains("service") || f.Contains("repository")))
            {
                techStacks.Add("API");
            }
        }

        // Detect UI/Frontend
        if (fileTypes.Contains(FileType.TypeScript) || fileTypes.Contains(FileType.HTML))
        {
            techStacks.Add("UI");
        }

        // Detect Database if there are repository or data access patterns
        if (filePaths.Any(p => p.Contains("repository") || p.Contains("data") || p.Contains("persistence")))
        {
            techStacks.Add("Database");
        }

        // Always add General standards
        techStacks.Add("General");

        return techStacks;
    }

    private ProjectAnalysis AnalyzeProjectStructure(List<CodeFile> files)
    {
        var analysis = new ProjectAnalysis();
        
        var folders = files.Select(f => Path.GetDirectoryName(f.FilePath) ?? "").Distinct().ToList();
        
        // Detect folder patterns
        analysis.HasControllerFolder = folders.Any(f => f.Contains("Controller", StringComparison.OrdinalIgnoreCase));
        analysis.HasServiceFolder = folders.Any(f => f.Contains("Service", StringComparison.OrdinalIgnoreCase));
        analysis.HasRepositoryFolder = folders.Any(f => f.Contains("Repository", StringComparison.OrdinalIgnoreCase) || 
                                                        f.Contains("Persistence", StringComparison.OrdinalIgnoreCase));
        analysis.HasModelFolder = folders.Any(f => f.Contains("Model", StringComparison.OrdinalIgnoreCase) || 
                                                  f.Contains("Entities", StringComparison.OrdinalIgnoreCase) ||
                                                  f.Contains("Domain", StringComparison.OrdinalIgnoreCase));
        analysis.HasInterfaceFolder = folders.Any(f => f.Contains("Interface", StringComparison.OrdinalIgnoreCase));
        analysis.HasComponentsFolder = folders.Any(f => f.Contains("component", StringComparison.OrdinalIgnoreCase));
        analysis.HasServicesFolder = folders.Any(f => f.Contains("service", StringComparison.OrdinalIgnoreCase));
        analysis.HasFeaturesFolder = folders.Any(f => f.Contains("feature", StringComparison.OrdinalIgnoreCase));
        analysis.HasSharedFolder = folders.Any(f => f.Contains("shared", StringComparison.OrdinalIgnoreCase) || 
                                                    f.Contains("common", StringComparison.OrdinalIgnoreCase));
        analysis.HasCoreFolder = folders.Any(f => f.Contains("core", StringComparison.OrdinalIgnoreCase));
        
        // Detect Clean Architecture
        analysis.UsesCleanArchitecture = folders.Any(f => f.Contains("Domain", StringComparison.OrdinalIgnoreCase)) &&
                                         folders.Any(f => f.Contains("Application", StringComparison.OrdinalIgnoreCase)) &&
                                         folders.Any(f => f.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        
        analysis.UsesFeatureBasedOrganization = folders.Any(f => f.Contains("features", StringComparison.OrdinalIgnoreCase));
        
        // Count specific file types
        analysis.ComponentCount = files.Count(f => f.FileName.EndsWith(".component.ts", StringComparison.OrdinalIgnoreCase));
        analysis.ServiceCount = files.Count(f => f.FileName.EndsWith(".service.ts", StringComparison.OrdinalIgnoreCase) ||
                                                 f.FileName.Contains("Service.cs", StringComparison.OrdinalIgnoreCase));
        analysis.ControllerCount = files.Count(f => f.FileName.EndsWith("Controller.cs", StringComparison.OrdinalIgnoreCase));
        
        return analysis;
    }

    private CodePatterns AnalyzeCodePatterns(List<CodeFile> files)
    {
        var patterns = new CodePatterns();
        
        foreach (var file in files)
        {
            var content = file.Content;
            
            // Analyze C# patterns
            if (file.FileType == FileType.CSharp)
            {
                patterns.UsesAsyncAwait = patterns.UsesAsyncAwait || 
                    content.Contains("async ") || content.Contains("await ") || content.Contains("Task<");
                
                patterns.UsesDependencyInjection = patterns.UsesDependencyInjection ||
                    Regex.IsMatch(content, @"public\s+\w+\([^)]*I\w+[^)]*\)");
                
                patterns.UsesTryCatch = patterns.UsesTryCatch ||
                    content.Contains("try") && content.Contains("catch");
                
                patterns.HasErrorHandling = patterns.HasErrorHandling ||
                    content.Contains("try") || content.Contains("catch") || content.Contains("throw");
                
                patterns.UsesLogging = patterns.UsesLogging ||
                    content.Contains("ILogger") || content.Contains("_logger");
                
                patterns.UsesInterfaces = patterns.UsesInterfaces ||
                    Regex.IsMatch(content, @"interface\s+I\w+");
                
                patterns.UsesDTO = patterns.UsesDTO ||
                    content.Contains("DTO") || content.Contains("Dto");
                
                patterns.UsesValidation = patterns.UsesValidation ||
                    content.Contains("[Required]") || content.Contains("[ValidateAntiForgeryToken]");
                
                patterns.UsesEntityFramework = patterns.UsesEntityFramework ||
                    content.Contains("DbContext") || content.Contains("DbSet");
            }
            
            // Analyze TypeScript/Angular patterns
            if (file.FileType == FileType.TypeScript)
            {
                patterns.UsesObservables = patterns.UsesObservables ||
                    content.Contains("Observable") || content.Contains("subscribe");
                
                patterns.UsesCatchError = patterns.UsesCatchError ||
                    content.Contains("catchError");
                
                patterns.HasErrorHandling = patterns.HasErrorHandling ||
                    content.Contains("catchError") || content.Contains("try") || content.Contains("catch");
                
                patterns.UsesReactiveForms = patterns.UsesReactiveForms ||
                    content.Contains("FormBuilder") || content.Contains("FormGroup");
                
                patterns.UsesAngularServices = patterns.UsesAngularServices ||
                    content.Contains("@Injectable");
                
                patterns.UsesAngularComponents = patterns.UsesAngularComponents ||
                    content.Contains("@Component");
                
                patterns.UsesAsyncPipe = patterns.UsesAsyncPipe ||
                    content.Contains("| async");
                
                patterns.UsesAngularMaterial = patterns.UsesAngularMaterial ||
                    content.Contains("@angular/material");
                
                patterns.UsesHttpClient = patterns.UsesHttpClient ||
                    content.Contains("HttpClient");
            }
            
            // Analyze HTML patterns
            if (file.FileType == FileType.HTML)
            {
                patterns.UsesAngularDirectives = patterns.UsesAngularDirectives ||
                    content.Contains("*ngIf") || content.Contains("*ngFor");
                
                patterns.UsesEventBinding = patterns.UsesEventBinding ||
                    content.Contains("(click)") || content.Contains("(change)");
            }
        }
        
        return patterns;
    }

    private List<Standard> GenerateStandardsForTechStack(
        string techStack, 
        ProjectAnalysis projectAnalysis, 
        CodePatterns codePatterns,
        List<CodeFile> files)
    {
        return techStack switch
        {
            "API" => GenerateAPIStandards(projectAnalysis, codePatterns, files),
            "UI" => GenerateUIStandards(projectAnalysis, codePatterns, files),
            "Database" => GenerateDatabaseStandards(projectAnalysis, codePatterns),
            "General" => GenerateGeneralStandards(projectAnalysis, codePatterns),
            _ => new List<Standard>()
        };
    }

    private List<Standard> GenerateAPIStandards(ProjectAnalysis analysis, CodePatterns patterns, List<CodeFile> files)
    {
        var standards = new List<Standard>();
        
        // Analyze actual project structure
        var apiFiles = files.Where(f => f.FileType == FileType.CSharp).ToList();
        var hasControllers = apiFiles.Any(f => f.FileName.Contains("Controller"));
        var hasServices = apiFiles.Any(f => f.FileName.Contains("Service"));
        var hasRepositories = apiFiles.Any(f => f.FileName.Contains("Repository"));
        
        // Generate standards based on what we found
        standards.Add(new Standard
        {
            Name = $"Project Structure - {(analysis.UsesCleanArchitecture ? "Clean Architecture" : "Layered Architecture")}",
            Description = analysis.UsesCleanArchitecture 
                ? "Your project follows Clean Architecture with Domain, Application, and Infrastructure layers. Continue maintaining separation of concerns between layers."
                : "Maintain clear separation between Controllers, Services, and Data Access layers.",
            Category = "Architecture",
            TechStack = "API",
            Priority = "Critical",
            Tags = new List<string> { "Architecture", "Structure", "Layers" },
            Examples = new List<string>
            {
                analysis.UsesCleanArchitecture 
                    ? "Domain: Core business entities and interfaces"
                    : "Controllers: HTTP endpoints and routing",
                analysis.UsesCleanArchitecture 
                    ? "Application: Business logic and use cases"
                    : "Services: Business logic implementation",
                analysis.UsesCleanArchitecture 
                    ? "Infrastructure: External dependencies and data access"
                    : "Repositories: Data access layer"
            }
        });
        
        if (patterns.UsesAsyncAwait)
        {
            standards.Add(new Standard
            {
                Name = "Async/Await Pattern - Already Implemented",
                Description = "Your project uses async/await for I/O operations. Continue this pattern for all database calls and external API requests.",
                Category = "Performance",
                TechStack = "API",
                Priority = "High",
                Tags = new List<string> { "Async", "Performance", "Best Practice" },
                Examples = new List<string>
                {
                    "public async Task<ActionResult> GetUsersAsync()",
                    "await _repository.GetAllAsync()",
                    "Avoid blocking calls like .Result or .Wait()"
                }
            });
        }
        
        if (patterns.UsesDependencyInjection)
        {
            standards.Add(new Standard
            {
                Name = "Dependency Injection - Current Pattern",
                Description = "Your project uses constructor-based dependency injection. Continue injecting dependencies through constructors for better testability.",
                Category = "Architecture",
                TechStack = "API",
                Priority = "Critical",
                Tags = new List<string> { "DI", "IoC", "Testing" },
                Examples = new List<string>
                {
                    "Register in Program.cs: builder.Services.AddScoped<IService, Service>()",
                    "Inject via constructor: public Controller(IService service)",
                    "Avoid using 'new' keyword for services"
                }
            });
        }
        
        if (patterns.UsesTryCatch)
        {
            standards.Add(new Standard
            {
                Name = "Error Handling - Current Implementation",
                Description = "Your project implements try-catch blocks for error handling. Continue this pattern and ensure all controllers have proper error handling.",
                Category = "Error Handling",
                TechStack = "API",
                Priority = "Critical",
                Tags = new List<string> { "Error", "Exception", "Resilience" },
                Examples = new List<string>
                {
                    "Wrap risky operations in try-catch blocks",
                    "Log errors with appropriate context",
                    "Return consistent error responses",
                    "Don't expose internal error details to clients"
                }
            });
        }
        
        if (patterns.UsesLogging)
        {
            standards.Add(new Standard
            {
                Name = "Logging Pattern - Current Usage",
                Description = "Your project uses ILogger for logging. Continue using structured logging with appropriate log levels.",
                Category = "Observability",
                TechStack = "API",
                Priority = "High",
                Tags = new List<string> { "Logging", "Monitoring", "Debugging" },
                Examples = new List<string>
                {
                    "_logger.LogInformation(\"Processing request for {UserId}\", userId)",
                    "_logger.LogError(ex, \"Error occurred\")",
                    "Use appropriate log levels (Info, Warning, Error)",
                    "Avoid logging sensitive data"
                }
            });
        }
        
        // Add standards based on folder structure
        if (analysis.HasControllerFolder && analysis.HasServiceFolder)
        {
            standards.Add(new Standard
            {
                Name = "Service Layer Separation - Detected Pattern",
                Description = "Your project separates Controllers from Services. Controllers should delegate business logic to services.",
                Category = "Architecture",
                TechStack = "API",
                Priority = "High",
                Tags = new List<string> { "Separation", "Services", "Controllers" },
                Examples = new List<string>
                {
                    "Controllers: Handle HTTP, routing, and validation",
                    "Services: Implement business logic",
                    "Avoid business logic in controllers",
                    "Keep controllers thin"
                }
            });
        }
        
        return standards;
    }

    private List<Standard> GenerateUIStandards(ProjectAnalysis analysis, CodePatterns patterns, List<CodeFile> files)
    {
        var standards = new List<Standard>();
        
        // Analyze actual UI files
        var uiFiles = files.Where(f => 
            f.FileType == FileType.TypeScript || 
            f.FileType == FileType.HTML).ToList();
        
        var componentFiles = uiFiles.Where(f => f.FileName.Contains(".component.")).ToList();
        var serviceFiles = uiFiles.Where(f => f.FileName.Contains(".service.")).ToList();
        
        // Folder structure standard
        if (analysis.UsesFeatureBasedOrganization)
        {
            standards.Add(new Standard
            {
                Name = "Feature-Based Organization - Current Structure",
                Description = "Your project uses feature-based folder organization. Continue organizing code by feature rather than by technical type.",
                Category = "Architecture",
                TechStack = "UI",
                Priority = "Critical",
                Tags = new List<string> { "Organization", "Features", "Structure" },
                Examples = new List<string>
                {
                    "features/dashboard/ - All dashboard-related code",
                    "features/reports/ - All reports-related code",
                    "Shared components in shared/ folder",
                    "Core services in core/services/"
                }
            });
        }
        
        // Component architecture
        if (componentFiles.Any())
        {
            standards.Add(new Standard
            {
                Name = "Component-Based Architecture - Detected Pattern",
                Description = $"Your project has {componentFiles.Count} components. Continue breaking UI into reusable, self-contained components with single responsibility.",
                Category = "Architecture",
                TechStack = "UI",
                Priority = "Critical",
                Tags = new List<string> { "Components", "Reusability", "Architecture" },
                Examples = new List<string>
                {
                    "Create focused components (under 300 lines)",
                    "Use @Input() and @Output() for communication",
                    "Follow naming: feature-name.component.ts",
                    "Keep logic separate from presentation"
                }
            });
        }
        
        // RxJS and Observables
        if (patterns.UsesObservables)
        {
            standards.Add(new Standard
            {
                Name = "Reactive Programming with RxJS - Current Usage",
                Description = "Your project uses RxJS Observables for async operations. Continue this pattern and always unsubscribe to prevent memory leaks.",
                Category = "State Management",
                TechStack = "UI",
                Priority = "High",
                Tags = new List<string> { "RxJS", "Observables", "Async" },
                Examples = new List<string>
                {
                    "Use async pipe in templates to auto-unsubscribe",
                    "Implement takeUntil pattern with destroy$ subject",
                    "Avoid nested subscriptions",
                    "Use operators like map, switchMap, catchError"
                }
            });
        }
        
        // Error handling in UI
        if (patterns.UsesCatchError)
        {
            standards.Add(new Standard
            {
                Name = "Error Handling - Current Implementation",
                Description = "Your project uses catchError operator for handling errors in observables. Continue handling errors gracefully with user-friendly messages.",
                Category = "Error Handling",
                TechStack = "UI",
                Priority = "Critical",
                Tags = new List<string> { "Error", "UX", "Observables" },
                Examples = new List<string>
                {
                    "Use catchError in service methods",
                    "Display toast/snackbar messages for errors",
                    "Show loading states during operations",
                    "Provide meaningful error messages to users"
                }
            });
        }
        
        // Services pattern
        if (serviceFiles.Any())
        {
            standards.Add(new Standard
            {
                Name = "Service Layer Pattern - Detected Structure",
                Description = $"Your project has {serviceFiles.Count} services handling business logic and HTTP calls. Keep components thin by delegating to services.",
                Category = "Architecture",
                TechStack = "UI",
                Priority = "High",
                Tags = new List<string> { "Services", "Separation", "HTTP" },
                Examples = new List<string>
                {
                    "Services handle HTTP requests and business logic",
                    "Components handle presentation and user interaction",
                    "Inject services via constructor",
                    "Make services providedIn: 'root' for singletons"
                }
            });
        }
        
        // Reactive forms
        if (patterns.UsesReactiveForms)
        {
            standards.Add(new Standard
            {
                Name = "Reactive Forms - Current Pattern",
                Description = "Your project uses Reactive Forms. Continue using FormBuilder and validators for complex forms with proper validation.",
                Category = "Forms",
                TechStack = "UI",
                Priority = "High",
                Tags = new List<string> { "Forms", "Validation", "Reactive" },
                Examples = new List<string>
                {
                    "Use FormBuilder to create forms",
                    "Implement custom validators when needed",
                    "Show validation errors appropriately",
                    "Handle form submission with error handling"
                }
            });
        }
        
        // Angular Material
        if (patterns.UsesAngularMaterial)
        {
            standards.Add(new Standard
            {
                Name = "Angular Material - Current UI Library",
                Description = "Your project uses Angular Material components. Continue using Material Design components for consistent UI/UX.",
                Category = "UI Components",
                TechStack = "UI",
                Priority = "Medium",
                Tags = new List<string> { "Material", "Components", "Design" },
                Examples = new List<string>
                {
                    "Use mat-* components for consistency",
                    "Follow Material Design guidelines",
                    "Customize theme in theme.scss",
                    "Use Material icons"
                }
            });
        }
        
        // Event binding
        if (patterns.UsesEventBinding)
        {
            standards.Add(new Standard
            {
                Name = "Template Event Handling - Detected Pattern",
                Description = "Your templates use event binding for user interactions. Keep templates clean by moving complex logic to component methods.",
                Category = "Templates",
                TechStack = "UI",
                Priority = "Medium",
                Tags = new List<string> { "Templates", "Events", "Binding" },
                Examples = new List<string>
                {
                    "Use (click)=\"onAction()\" for event binding",
                    "Move complex logic to component methods",
                    "Use async pipe for observables in templates",
                    "Use trackBy with *ngFor for performance"
                }
            });
        }
        
        return standards;
    }

    private List<Standard> GenerateDatabaseStandards(ProjectAnalysis analysis, CodePatterns patterns)
    {
        var standards = new List<Standard>();
        
        // Only add database standards if we detect database usage
        if (patterns.UsesEntityFramework || analysis.HasRepositoryFolder)
        {
            standards.Add(new Standard
            {
                Name = "Repository Pattern - Detected Structure",
                Description = analysis.HasRepositoryFolder 
                    ? "Your project uses Repository pattern for data access. Continue abstracting database operations behind repository interfaces."
                    : "Consider implementing Repository pattern to abstract data access logic.",
                Category = "Architecture",
                TechStack = "Database",
                Priority = "High",
                Tags = new List<string> { "Repository", "Data Access", "Abstraction" },
                Examples = new List<string>
                {
                    "Define IRepository<T> interface",
                    "Implement concrete repositories in Infrastructure",
                    "Inject repositories into services",
                    "Keep database logic out of controllers"
                }
            });
        }
        
        if (patterns.UsesEntityFramework)
        {
            standards.Add(new Standard
            {
                Name = "Entity Framework - Current ORM",
                Description = "Your project uses Entity Framework. Use async methods and avoid common pitfalls like N+1 queries.",
                Category = "Data Access",
                TechStack = "Database",
                Priority = "High",
                Tags = new List<string> { "EF", "ORM", "Performance" },
                Examples = new List<string>
                {
                    "Use ToListAsync(), FirstOrDefaultAsync() for async operations",
                    "Use Include() for eager loading to avoid N+1",
                    "Use AsNoTracking() for read-only queries",
                    "Avoid loading entire tables - use pagination"
                }
            });
        }
        
        standards.Add(new Standard
        {
            Name = "Parameterized Queries",
            Description = "Always use parameterized queries or ORMs to prevent SQL injection attacks.",
            Category = "Security",
            TechStack = "Database",
            Priority = "Critical",
            Tags = new List<string> { "Security", "SQL", "Injection" },
            Examples = new List<string>
            {
                "Use Entity Framework or Dapper with parameters",
                "Never concatenate user input in SQL queries",
                "Use stored procedures with parameters if needed",
                "Validate and sanitize all inputs"
            }
        });
        
        return standards;
    }

    private List<Standard> GenerateGeneralStandards(ProjectAnalysis analysis, CodePatterns patterns)
    {
        var standards = new List<Standard>();
        
        // Clean Architecture standard
        if (analysis.UsesCleanArchitecture)
        {
            standards.Add(new Standard
            {
                Name = "Clean Architecture - Current Implementation",
                Description = "Your project follows Clean Architecture with Domain, Application, and Infrastructure layers. Continue maintaining this separation for better testability and maintainability.",
                Category = "Architecture",
                TechStack = "General",
                Priority = "Critical",
                Tags = new List<string> { "Clean Architecture", "Layers", "Separation" },
                Examples = new List<string>
                {
                    "Domain: Contains entities, enums, and core business rules",
                    "Application: Contains interfaces and business logic contracts",
                    "Infrastructure: Contains implementations and external dependencies",
                    "API: Contains controllers and presentation logic"
                }
            });
        }
        
        standards.Add(new Standard
        {
            Name = "Naming Conventions - Detected Pattern",
            Description = "Follow consistent naming conventions throughout the codebase. Your project uses standard C# and TypeScript conventions.",
            Category = "Code Quality",
            TechStack = "General",
            Priority = "High",
            Tags = new List<string> { "Naming", "Conventions", "Readability" },
            Examples = new List<string>
            {
                "PascalCase for classes, methods, and properties",
                "camelCase for variables and parameters",
                "Use descriptive names: GetUserById vs Get()",
                "Prefix interfaces with 'I': IRepository"
            }
        });
        
        if (patterns.UsesAsyncAwait)
        {
            standards.Add(new Standard
            {
                Name = "Async Programming - Current Pattern",
                Description = "Your project uses async/await consistently. Continue this pattern across all I/O operations for better scalability.",
                Category = "Performance",
                TechStack = "General",
                Priority = "High",
                Tags = new List<string> { "Async", "Performance", "Scalability" },
                Examples = new List<string>
                {
                    "Suffix async methods with 'Async'",
                    "Use async all the way (don't mix with blocking calls)",
                    "Avoid .Result or .Wait() - use await",
                    "Return Task<T> for async operations"
                }
            });
        }
        
        standards.Add(new Standard
        {
            Name = "Code Documentation",
            Description = "Write clear, concise comments for complex logic and public APIs. Your project structure is self-documenting through Clean Architecture.",
            Category = "Documentation",
            TechStack = "General",
            Priority = "Medium",
            Tags = new List<string> { "Documentation", "Comments", "Clarity" },
            Examples = new List<string>
            {
                "Document WHY, not WHAT (code shows what)",
                "Use XML comments for public APIs",
                "Keep README files up-to-date",
                "Document architectural decisions"
            }
        });
        
        if (analysis.HasControllerFolder && analysis.HasServiceFolder)
        {
            standards.Add(new Standard
            {
                Name = "DRY Principle - Reusability Pattern",
                Description = "Your project separates concerns into Controllers, Services, and Repositories. Continue extracting common functionality to avoid duplication.",
                Category = "Code Quality",
                TechStack = "General",
                Priority = "High",
                Tags = new List<string> { "DRY", "Reusability", "Maintainability" },
                Examples = new List<string>
                {
                    "Extract common logic into shared services",
                    "Use base classes for shared controller functionality",
                    "Create utility classes for repeated operations",
                    "Avoid copy-pasting code between features"
                }
            });
        }
        
        return standards;
    }
}

// Analysis models
public class ProjectAnalysis
{
    public bool UsesCleanArchitecture { get; set; }
    public bool UsesFeatureBasedOrganization { get; set; }
    public bool HasControllerFolder { get; set; }
    public bool HasServiceFolder { get; set; }
    public bool HasRepositoryFolder { get; set; }
    public bool HasModelsFolder { get; set; }
    public bool HasComponentsFolder { get; set; }
    public bool HasSharedFolder { get; set; }
    public bool HasCoreFolder { get; set; }
    public bool HasFeaturesFolder { get; set; }
    public bool HasInfrastructureFolder { get; set; }
    public bool HasModelFolder { get; set; }
    public bool HasInterfaceFolder { get; set; }
    public bool HasServicesFolder { get; set; }
    public int ComponentCount { get; set; }
    public int ServiceCount { get; set; }
    public int ControllerCount { get; set; }
}

public class CodePatterns
{
    public bool UsesAsyncAwait { get; set; }
    public bool UsesDependencyInjection { get; set; }
    public bool UsesTryCatch { get; set; }
    public bool UsesLogging { get; set; }
    public bool UsesEntityFramework { get; set; }
    public bool UsesObservables { get; set; }
    public bool UsesCatchError { get; set; }
    public bool UsesReactiveForms { get; set; }
    public bool UsesAngularMaterial { get; set; }
    public bool UsesEventBinding { get; set; }
    public bool UsesHttpClient { get; set; }
    public bool UsesAngularDirectives { get; set; }
    public bool HasErrorHandling { get; set; }
    public bool UsesInterfaces { get; set; }
    public bool UsesDTO { get; set; }
    public bool UsesValidation { get; set; }
    public bool UsesAngularServices { get; set; }
    public bool UsesAngularComponents { get; set; }
    public bool UsesAsyncPipe { get; set; }
}
