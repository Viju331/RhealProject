namespace RhealAI.Domain.Enums;

/// <summary>
/// Types of files that can be analyzed
/// </summary>
public enum FileType
{
    Unknown,
    // .NET Languages
    CSharp,
    VisualBasic,
    FSharp,

    // Web Languages
    TypeScript,
    JavaScript,
    HTML,
    CSS,

    // JVM Languages
    Java,
    Kotlin,
    Scala,
    Groovy,

    // Python
    Python,

    // PHP
    PHP,

    // Ruby
    Ruby,

    // Go
    Go,

    // Rust
    Rust,

    // C/C++
    C,
    CPlusPlus,
    ObjectiveC,

    // Swift
    Swift,

    // Database
    SQL,

    // Markup/Data
    JSON,
    XML,
    YAML,
    Markdown,

    // Configuration
    Configuration,

    // ASP.NET
    ASPX,
    ASCX,
    ASMX,
    ASPNET,

    // JSX/TSX
    JSX,
    TSX,

    // Vue/Angular/React
    Vue,
    Svelte,

    // Mobile
    Dart,

    // Shell
    Shell,
    PowerShell,

    // Other
    Perl,
    R,
    Lua,
    Elixir,
    Haskell
}
