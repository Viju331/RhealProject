# Code Snippet with Line Ranges Implementation

## Overview

Updated the system to intelligently capture code snippets with line ranges, displaying them as "Line 5" for single-line issues or "Lines 10-25" for multi-line issues (methods/blocks).

## Backend Changes

### 1. Domain Entities

**Files Modified:**

- `RhealAI.Domain/Entities/Violation.cs`
- `RhealAI.Domain/Entities/Bug.cs`

**Changes:**

- Added `EndLineNumber` property to both entities
- This allows tracking of line ranges for code issues

### 2. AI Analysis Service

**File Modified:** `RhealAI.Infrastructure/Services/AIAnalysisService.cs`

**New Methods Added:**

#### `GetCodeSnippetWithRange(string[] lines, int lineNumber)`

Returns a tuple of `(snippet, startLine, endLine)` with intelligent context detection:

- **Single-line changes**: Returns just that line if it's a simple statement
- **Multi-line changes**: Extracts the entire method or code block

#### `IsSingleLineChange(string line)`

Detects if a line represents a single-line statement:

- Variable declarations (`var`, `const`, `let`)
- Return statements
- Simple statements ending with `;`
- Short lines (<150 chars) without braces or parentheses

#### `FindCodeBlock(string[] lines, int currentIndex)`

Intelligently finds the start and end of a code block:

- Looks backward for method declarations or opening braces
- Looks forward for closing braces or next method
- Limits search to 50 lines in each direction
- Ensures minimum 3 lines of context

**Updated Methods:**

- `GenerateMockViolations()` - Uses `GetCodeSnippetWithRange()` for all violations
- `GenerateMockBugs()` - Uses `GetCodeSnippetWithRange()` for all bugs
- All mock generation now properly sets `LineNumber` and `EndLineNumber`

### 3. DTO Classes

**Added `EndLineNumber` to:**

- `ViolationDto`
- `BugDto`

**Updated Parser Methods:**

- `ParseViolationsFromResponse()` - Maps `EndLineNumber` from AI responses
- `ParseBugsFromResponse()` - Maps `EndLineNumber` from AI responses
- Defaults `EndLineNumber` to `LineNumber` if not provided

### 4. AI Prompts

**Files Modified:**

- `RhealAI.Application/Prompts/ViolationDetectionPrompts.cs`
- `RhealAI.Application/Prompts/BugDetectionPrompts.cs`

**Updated Instructions:**
AI is now instructed to:

- Provide `lineNumber` and `endLineNumber` in JSON responses
- For single-line issues: set `endLineNumber = lineNumber`
- For multi-line issues: set appropriate range
- Include full method code for complex issues
- Include single line for simple issues

## Frontend Changes

### 1. Dialog Component

**Files Modified:**

- `code-detail-dialog.component.ts`
- `code-detail-dialog.component.html`

**Changes:**

- Added `endLineNumber?: number` to `CodeDetailData` interface
- Added `getLineRangeDisplay()` method that returns:
  - `"Line 5"` for single-line issues
  - `"Lines 10-25"` for multi-line issues
- Updated template to display formatted line range

### 2. Analysis Result Page

**File Modified:** `analysis-result-page.component.ts`

**Changes:**

- `viewViolationDetails()`: Passes `endLineNumber` to dialog
- `viewBugDetails()`: Passes `endLineNumber` to dialog
- Both methods default `endLineNumber` to `lineNumber` if not available

## How It Works

### Example 1: Single-Line Issue

```csharp
// Input: Line 42 has naming convention violation
var my_variable = 5;  // ❌ Underscore in variable name

// Output:
// lineNumber: 42
// endLineNumber: 42
// codeSnippet: "var my_variable = 5;"
// Display: "Line 42"
```

### Example 2: Multi-Line Issue (Method)

```csharp
// Input: Line 15 is in a method with error handling issue
public async Task ProcessData()  // Line 10
{
    var data = await GetDataAsync();  // Line 12
    // Process data
    return result;  // Line 15
}  // Line 16

// Output:
// lineNumber: 10
// endLineNumber: 16
// codeSnippet: [entire method code]
// Display: "Lines 10-16"
```

## Benefits

1. **Better Context**: Users see the full method when needed, not just a single line
2. **Accurate Line Ranges**: Display shows exactly where the issue spans
3. **Intelligent Detection**: Automatically determines if single-line or multi-line
4. **Consistent Format**: "Line X" or "Lines X-Y" format throughout UI
5. **AI-Ready**: AI models can provide detailed line ranges in responses

## Testing

To test the implementation:

1. **Run Analysis**: Upload a project and run analysis
2. **View Violations**: Click on a violation action button
3. **Check Display**:
   - Single-line issues show "Line X"
   - Multi-line issues show "Lines X-Y"
   - Code snippet matches the line range
4. **Verify Code**: Code snippet should contain either:
   - Just the problematic line (for simple issues)
   - The entire method/block (for complex issues)

## Future Enhancements

Potential improvements:

1. **Syntax Highlighting**: Add code highlighting based on file type
2. **Line Number Sidebar**: Display line numbers in the code view
3. **Jump to Line**: Add ability to jump to specific line in code editor
4. **Diff View**: Show before/after comparison with line numbers
5. **Export**: Include line ranges in exported reports
