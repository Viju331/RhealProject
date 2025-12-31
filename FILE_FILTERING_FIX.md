# File Filtering Fix for Angular Projects

## Problem

When uploading Angular project ZIP files, the system was analyzing compiled/minified files (chunk-_.js, polyfills-_.js, etc.) from the `dist` folder instead of the actual source files (_.ts, _.component.ts) from the `src` folder.

## Root Causes

### 1. Directory Filtering Bug in ZipExtractor.cs

**Issue**: Line 70 was only passing the directory name instead of the full relative path to `ShouldIgnoreFile()`

```csharp
// Before (incorrect)
var dirName = Path.GetFileName(subDir);
if (!FileAnalyzer.ShouldIgnoreFile(dirName))

// After (correct)
var relativeDirPath = Path.GetRelativePath(basePath, subDir);
if (!FileAnalyzer.ShouldIgnoreFile(relativeDirPath))
```

This meant that nested dist folders like `project/frontend/dist` wouldn't be properly filtered because only "dist" was checked, not the full path.

### 2. Missing Build Output File Pattern Filtering

**Issue**: FileAnalyzer only checked folder names, not file name patterns

The system was missing pattern-based filtering for:

- Angular build outputs: `main-*.js`, `chunk-*.js`, `polyfills-*.js`, `runtime-*.js`
- Minified files: `*.min.js`, `*.min.css`
- Source maps: `*.js.map`, `*.css.map`
- Lock files: `package-lock.json`, `yarn.lock`

## Solution

### 1. Added File Pattern Filtering Array

Added `IgnoredFilePatterns` array with common build output patterns:

```csharp
private static readonly string[] IgnoredFilePatterns = new[]
{
    // Angular/React/Vue build outputs
    "main-*.js",
    "chunk-*.js",
    "polyfills-*.js",
    "runtime-*.js",
    "vendor-*.js",
    "styles-*.css",
    "*.min.js",
    "*.min.css",
    "*.bundle.js",
    "*.bundle.css",

    // Source maps
    "*.js.map",
    "*.css.map",
    "*.ts.map",

    // Lock files
    "package-lock.json",
    "yarn.lock",
    "pnpm-lock.yaml",
    "composer.lock",
    "Gemfile.lock",
    "Pipfile.lock",
    "poetry.lock"
};
```

### 2. Enhanced ShouldIgnoreFile Method

Updated to check both folder paths and file name patterns:

```csharp
public static bool ShouldIgnoreFile(string filePath)
{
    // Check if path contains any ignored folders
    var pathParts = filePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
    if (pathParts.Any(part => IgnoredFolders.Contains(part)))
    {
        return true;
    }

    // Check if file name matches any ignored patterns
    var fileName = Path.GetFileName(filePath);
    foreach (var pattern in IgnoredFilePatterns)
    {
        if (MatchesPattern(fileName, pattern))
        {
            return true;
        }
    }

    return false;
}
```

### 3. Added Pattern Matching Helper

Created `MatchesPattern` method to handle wildcard patterns:

```csharp
private static bool MatchesPattern(string fileName, string pattern)
{
    if (pattern.Contains('*'))
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
    }
    return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
}
```

### 4. Added Angular-Specific Ignored Folders

Extended `IgnoredFolders` to include:

- `.angular` - Angular CLI cache
- `.next` - Next.js build
- `.nuxt` - Nuxt.js build
- `www` - Ionic/Cordova build
- `output` - Common build output folder

### 5. Fixed ZipExtractor Directory Filtering

Changed to use full relative path instead of just directory name:

```csharp
var relativeDirPath = Path.GetRelativePath(basePath, subDir);
if (!FileAnalyzer.ShouldIgnoreFile(relativeDirPath))
{
    await ProcessDirectoryAsync(subDir, basePath, files);
}
```

## Impact

### Before Fix

- ✗ Analyzed: `main-ABC123.js`, `chunk-XYZ789.js`, `polyfills-DEF456.js`
- ✗ Ignored: `app.component.ts`, `app.service.ts`, `app.module.ts`
- ✗ Dist folders with nested paths were not filtered properly

### After Fix

- ✓ Analyzes: `app.component.ts`, `app.service.ts`, `app.module.ts`
- ✓ Ignores: `main-*.js`, `chunk-*.js`, `polyfills-*.js`, all minified files
- ✓ Properly filters dist/build folders at any nesting level
- ✓ Skips source maps and lock files

## Testing

To verify the fix works correctly:

1. Upload an Angular project ZIP that includes both `src/` and `dist/` folders
2. Monitor the analysis progress
3. Verify that:
   - TypeScript files (_.ts, _.component.ts) are being read and analyzed
   - Chunk files (chunk-\*.js) are being skipped
   - The analysis shows proper file counts for TypeScript files
   - The app component and other Angular components appear in the analysis results

## Files Modified

1. **FileAnalyzer.cs**

   - Added `IgnoredFilePatterns` array
   - Added `.angular`, `.next`, `.nuxt`, `www`, `output` to `IgnoredFolders`
   - Enhanced `ShouldIgnoreFile()` to check both folder paths and file patterns
   - Added `MatchesPattern()` helper method
   - Added `using System.Text.RegularExpressions;`

2. **ZipExtractor.cs**
   - Fixed directory filtering to use full relative path instead of directory name
   - Changed from `Path.GetFileName(subDir)` to `Path.GetRelativePath(basePath, subDir)`

## Build Status

✅ Build succeeded with no errors (only existing NU1510 warning about SignalR package)
