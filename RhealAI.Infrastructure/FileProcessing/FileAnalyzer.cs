using RhealAI.Domain.Enums;

namespace RhealAI.Infrastructure.FileProcessing;

/// <summary>
/// Utility for analyzing and categorizing files
/// </summary>
public static class FileAnalyzer
{
    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        // JavaScript/Node.js
        "node_modules",
        "bower_components",
        ".npm",
        ".yarn",
        ".pnp",
        
        // Build outputs
        "dist",
        "build",
        "out",
        "target",
        "bin",
        "obj",
        
        // IDE/Editor folders
        ".vs",
        ".vscode",
        ".idea",
        ".eclipse",
        ".settings",
        
        // Version control
        ".git",
        ".svn",
        ".hg",
        
        // Package managers
        "packages",
        "vendor",
        
        // Python
        "__pycache__",
        ".pytest_cache",
        ".mypy_cache",
        ".tox",
        "venv",
        ".venv",
        "env",
        ".env",
        "virtualenv",
        
        // Ruby
        ".bundle",
        
        // Java/Android
        ".gradle",
        ".m2",
        
        // Testing/Coverage
        "coverage",
        ".nyc_output",
        "htmlcov",
        
        // Logs
        "logs",
        "log",
        
        // Temporary files
        "tmp",
        "temp",
        ".tmp",
        ".temp",
        
        // OS
        ".DS_Store",
        "Thumbs.db"
    };

    private static readonly Dictionary<string, FileType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // .NET Languages
        { ".cs", FileType.CSharp },
        { ".vb", FileType.VisualBasic },
        { ".fs", FileType.FSharp },
        { ".fsx", FileType.FSharp },
        { ".fsi", FileType.FSharp },
        { ".csproj", FileType.Configuration },
        { ".vbproj", FileType.Configuration },
        { ".fsproj", FileType.Configuration },
        { ".sln", FileType.Configuration },
        
        // ASP.NET
        { ".aspx", FileType.ASPX },
        { ".ascx", FileType.ASCX },
        { ".asmx", FileType.ASMX },
        { ".ashx", FileType.ASPNET },
        { ".master", FileType.ASPNET },
        { ".vbhtml", FileType.ASPNET },
        { ".cshtml", FileType.ASPNET },
        { ".razor", FileType.ASPNET },
        { ".resx", FileType.XML },
        
        // Web - JavaScript/TypeScript
        { ".js", FileType.JavaScript },
        { ".jsx", FileType.JSX },
        { ".ts", FileType.TypeScript },
        { ".tsx", FileType.TSX },
        { ".mjs", FileType.JavaScript },
        { ".cjs", FileType.JavaScript },
        
        // Web - HTML/CSS
        { ".html", FileType.HTML },
        { ".htm", FileType.HTML },
        { ".css", FileType.CSS },
        { ".scss", FileType.CSS },
        { ".sass", FileType.CSS },
        { ".less", FileType.CSS },
        
        // Web - Frameworks
        { ".vue", FileType.Vue },
        { ".svelte", FileType.Svelte },
        
        // JVM Languages
        { ".java", FileType.Java },
        { ".kt", FileType.Kotlin },
        { ".kts", FileType.Kotlin },
        { ".scala", FileType.Scala },
        { ".sc", FileType.Scala },
        { ".groovy", FileType.Groovy },
        { ".gradle", FileType.Groovy },
        
        // Python
        { ".py", FileType.Python },
        { ".pyw", FileType.Python },
        { ".pyx", FileType.Python },
        { ".pyi", FileType.Python },
        { ".ipynb", FileType.Python },
        
        // PHP
        { ".php", FileType.PHP },
        { ".php3", FileType.PHP },
        { ".php4", FileType.PHP },
        { ".php5", FileType.PHP },
        { ".phtml", FileType.PHP },
        
        // Ruby
        { ".rb", FileType.Ruby },
        { ".erb", FileType.Ruby },
        { ".rake", FileType.Ruby },
        { ".gemspec", FileType.Ruby },
        
        // Go
        { ".go", FileType.Go },
        
        // Rust
        { ".rs", FileType.Rust },
        
        // C/C++/Objective-C
        { ".c", FileType.C },
        { ".h", FileType.C },
        { ".cpp", FileType.CPlusPlus },
        { ".cc", FileType.CPlusPlus },
        { ".cxx", FileType.CPlusPlus },
        { ".hpp", FileType.CPlusPlus },
        { ".hxx", FileType.CPlusPlus },
        { ".m", FileType.ObjectiveC },
        { ".mm", FileType.ObjectiveC },
        
        // Swift
        { ".swift", FileType.Swift },
        
        // Mobile - Dart (Flutter)
        { ".dart", FileType.Dart },
        
        // Database
        { ".sql", FileType.SQL },
        
        // Shell Scripts
        { ".sh", FileType.Shell },
        { ".bash", FileType.Shell },
        { ".zsh", FileType.Shell },
        { ".fish", FileType.Shell },
        { ".ps1", FileType.PowerShell },
        { ".psm1", FileType.PowerShell },
        { ".psd1", FileType.PowerShell },
        { ".bat", FileType.Shell },
        { ".cmd", FileType.Shell },
        
        // Markup/Data Formats
        { ".json", FileType.JSON },
        { ".xml", FileType.XML },
        { ".yaml", FileType.YAML },
        { ".yml", FileType.YAML },
        { ".toml", FileType.Configuration },
        
        // Configuration Files
        { ".config", FileType.Configuration },
        { ".ini", FileType.Configuration },
        { ".conf", FileType.Configuration },
        { ".properties", FileType.Configuration },
        { ".env", FileType.Configuration },
        { ".settings", FileType.Configuration },
        { ".editorconfig", FileType.Configuration },
        { ".gitignore", FileType.Configuration },
        { ".dockerignore", FileType.Configuration },
        
        // Documentation
        { ".md", FileType.Markdown },
        { ".markdown", FileType.Markdown },
        { ".txt", FileType.Markdown },
        { ".rst", FileType.Markdown },
        { ".adoc", FileType.Markdown },
        
        // Other Languages
        { ".pl", FileType.Perl },
        { ".pm", FileType.Perl },
        { ".r", FileType.R },
        { ".lua", FileType.Lua },
        { ".ex", FileType.Elixir },
        { ".exs", FileType.Elixir },
        { ".hs", FileType.Haskell },
        { ".lhs", FileType.Haskell },
        
        // Docker/Containerization  
        { ".dockerfile", FileType.Configuration }
    };

    private static readonly HashSet<string> SpecialFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dockerfile",
        "docker-compose.yml",
        "docker-compose.yaml",
        "Makefile",
        "CMakeLists.txt",
        "build.gradle",
        "pom.xml",
        "package.json",
        "composer.json",
        "Gemfile",
        "Cargo.toml",
        "requirements.txt",
        "setup.py",
        "Pipfile"
    };

    /// <summary>
    /// Checks if a file should be ignored during analysis
    /// </summary>
    public static bool ShouldIgnoreFile(string filePath)
    {
        var pathParts = filePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return pathParts.Any(part => IgnoredFolders.Contains(part));
    }

    /// <summary>
    /// Determines the file type from extension
    /// </summary>
    public static FileType GetFileType(string fileName)
    {
        // Check special file names first (Dockerfile, Makefile, etc.)
        var name = Path.GetFileName(fileName);
        if (SpecialFileNames.Contains(name))
        {
            // Determine type based on the file name
            if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase))
                return FileType.Configuration;
            if (name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                return FileType.YAML;
            if (name.Equals("Makefile", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("CMakeLists.txt", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Pipfile", StringComparison.OrdinalIgnoreCase))
                return FileType.Configuration;
            if (name.Equals("build.gradle", StringComparison.OrdinalIgnoreCase))
                return FileType.Groovy;
            if (name.Equals("pom.xml", StringComparison.OrdinalIgnoreCase))
                return FileType.XML;
            if (name.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("composer.json", StringComparison.OrdinalIgnoreCase))
                return FileType.JSON;
            if (name.Equals("Gemfile", StringComparison.OrdinalIgnoreCase))
                return FileType.Ruby;
            if (name.Equals("setup.py", StringComparison.OrdinalIgnoreCase))
                return FileType.Python;
        }

        // Check by extension
        var extension = Path.GetExtension(fileName);
        return ExtensionMap.TryGetValue(extension, out var fileType)
            ? fileType
            : FileType.Unknown;
    }

    /// <summary>
    /// Checks if file is a code file that should be analyzed
    /// </summary>
    public static bool IsCodeFile(FileType fileType)
    {
        return fileType != FileType.Unknown
            && fileType != FileType.Markdown
            && fileType != FileType.Configuration;
    }

    /// <summary>
    /// Checks if file is a markdown documentation file
    /// </summary>
    public static bool IsMarkdownFile(FileType fileType)
    {
        return fileType == FileType.Markdown;
    }
}
