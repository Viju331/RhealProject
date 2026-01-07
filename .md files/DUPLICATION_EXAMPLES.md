# Code Duplication Detection - Correct Examples

## ✅ SQL Duplication Example

**Description:** Similar stored procedure pattern repeated across migration scripts

**Duplicated Code (SQL):**

```sql
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProcessData]'))
BEGIN
    DROP PROCEDURE [dbo].[ProcessData]
END
GO

CREATE PROCEDURE [dbo].[ProcessData]
    @StartDate DATETIME,
    @EndDate DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION

        -- Process records
        INSERT INTO ProcessedData (DataId, ProcessDate, Status)
        SELECT DataId, GETDATE(), 'Processed'
        FROM RawData
        WHERE CreateDate BETWEEN @StartDate AND @EndDate

        COMMIT TRANSACTION
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION
        THROW
    END CATCH
END
GO
```

**Locations:**

1. `DB_Scripts\2020-06-23_Phase_2-3-Data.sql` (Lines 45-72)
2. `DB_Scripts\2020-10-15_Phase_2_3-Data.sql` (Lines 120-147)
3. `DB_Scripts\2020-11-06_Phase_2_1-Tables.sql` (Lines 88-115)

**Type:** StructuralMatch (95% similar)

**Suggestion:** Create a reusable stored procedure template or script generator to standardize transaction handling patterns across all migration scripts.

**Impact:** Medium

---

## ✅ C# Duplication Example

**Description:** Duplicate validation logic repeated across multiple services

**Duplicated Code (C#):**

```csharp
private void ValidateRequest(AnalysisRequest request)
{
    if (request == null)
        throw new ArgumentNullException(nameof(request));

    if (string.IsNullOrWhiteSpace(request.FilePath))
        throw new ArgumentException("File path cannot be empty", nameof(request.FilePath));

    if (!File.Exists(request.FilePath))
        throw new FileNotFoundException($"File not found: {request.FilePath}");

    var extension = Path.GetExtension(request.FilePath);
    if (!AllowedExtensions.Contains(extension))
        throw new InvalidOperationException($"Unsupported file type: {extension}");
}
```

**Locations:**

1. `RhealAI.Infrastructure\Services\AIAnalysisService.cs` (Lines 156-170)
2. `RhealAI.Infrastructure\Services\ReportService.cs` (Lines 89-103)
3. `RhealAI.Application\Services\FileProcessingService.cs` (Lines 234-248)

**Type:** ExactMatch (100% identical)

**Suggestion:** Extract to a shared validation utility class or base service class to eliminate duplication and ensure consistent validation across all services.

**Refactoring Options:**

- Create `FileRequestValidator` utility class
- Implement `IValidatable<T>` interface pattern
- Use FluentValidation library

**Impact:** High (affects multiple services)

---

## ✅ TypeScript Duplication Example

**Description:** Similar HTTP error handling repeated in multiple Angular services

**Duplicated Code (TypeScript):**

```typescript
private handleError(error: HttpErrorResponse): Observable<never> {
    let errorMessage = 'An error occurred';

    if (error.error instanceof ErrorEvent) {
        // Client-side error
        errorMessage = `Error: ${error.error.message}`;
    } else {
        // Server-side error
        errorMessage = `Error Code: ${error.status}\nMessage: ${error.message}`;
    }

    console.error(errorMessage);
    this.toastr.error(errorMessage, 'Error');
    return throwError(() => new Error(errorMessage));
}
```

**Locations:**

1. `RhealAI.Web\src\app\services\repository.service.ts` (Lines 78-92)
2. `RhealAI.Web\src\app\services\analysis.service.ts` (Lines 145-159)
3. `RhealAI.Web\src\app\services\standards.service.ts` (Lines 56-70)

**Type:** ExactMatch (100% identical)

**Suggestion:** Create a shared `ErrorHandlerService` and inject it into all services, or create an HTTP interceptor to centralize error handling globally.

**Refactoring Options:**

- Create `ErrorHandlerService` with centralized error handling
- Implement Angular HTTP Interceptor for global error handling
- Create base service class with shared error handling method

**Impact:** High

---

## ✅ JavaScript Duplication Example

**Description:** Repeated event listener setup and cleanup pattern

**Duplicated Code (JavaScript):**

```javascript
ngOnInit() {
    this.subscription = this.eventService.onDataUpdate
        .pipe(
            debounceTime(300),
            distinctUntilChanged()
        )
        .subscribe(data => {
            this.processData(data);
        });
}

ngOnDestroy() {
    if (this.subscription) {
        this.subscription.unsubscribe();
    }
}
```

**Locations:**

1. `RhealAI.Web\src\app\components\dashboard\dashboard.component.ts` (Lines 34-48)
2. `RhealAI.Web\src\app\components\analysis\analysis.component.ts` (Lines 56-70)
3. `RhealAI.Web\src\app\components\reports\report-list.component.ts` (Lines 42-56)

**Type:** StructuralMatch (90% similar)

**Suggestion:** Create a base component class or use Angular `takeUntil` pattern to standardize subscription management.

**Refactoring Options:**

- Create `BaseComponent` with auto-unsubscribe logic
- Use `takeUntil` pattern with component destroy subject
- Use Angular `async` pipe in templates to avoid manual subscriptions

**Impact:** Medium

---

## ❌ INCORRECT Example (What We Fixed)

**Description:** Event handler pattern: Event subscription and cleanup repeated across 3 files

**Current Code (WRONG - Shows JavaScript in SQL files):**

```javascript
try {
  const result = await operation();
  return processResult(result);
} catch (error) {
  logger.error("Operation failed", error);
  throw error;
}
```

**Locations (SQL files!):**

1. DB_Scripts\2020-06-23_Phase_2-3-Data.sql (Lines 108-115)
2. DB_Scripts\2020-10-15_Phase_2_3-Data.sql (Lines 174-181)
3. DB_Scripts\2020-11-06_Phase_2_1-Tables.sql (Lines 116-123)

**Problem:** JavaScript/TypeScript code cannot exist in SQL files. This is a cross-language false positive.

---

## What Changed

### Before (Incorrect):

- Analyzed **all files together** regardless of language
- AI could match patterns across different languages
- Showed JavaScript code from SQL file locations
- No language validation

### After (Correct):

1. **Group files by language** before analysis
2. **Analyze each language separately** (SQL with SQL, C# with C#, etc.)
3. **Include language metadata** in each file sent to AI
4. **Explicit AI instructions** to never mix languages
5. **Validate code snippets** match the file language

### Key Changes in Code:

````csharp
// Group files by extension/language
var filesByLanguage = files.GroupBy(f => Path.GetExtension(f.FilePath).ToLowerInvariant());

foreach (var languageGroup in filesByLanguage)
{
    var languageFiles = languageGroup.ToList();
    var languageName = GetLanguageName(extension);

    // Only analyze files of the same language together
    var filesText = string.Join("\n\n", languageFiles.Select(f =>
        $"File: {f.FilePath}\nLanguage: {languageName}\n```{lang}\n{f.Content}\n```"
    ));

    // AI prompt with strict language rules
    var prompt = $"Analyze ONLY these {languageName} files...";
}
````

## Expected Results

When analyzing a project with mixed languages:

✅ **SQL duplications** will show SQL code with SQL file locations  
✅ **C# duplications** will show C# code with .cs file locations  
✅ **TypeScript duplications** will show TypeScript code with .ts file locations  
✅ **Each language analyzed independently** - no cross-language matches  
✅ **Accurate code snippets** matching the actual file type

## Testing

Upload a project with multiple languages and verify:

1. SQL duplications only reference .sql files
2. C# duplications only reference .cs files
3. TypeScript duplications only reference .ts files
4. Code snippets match the file type syntax
5. No JavaScript code in SQL file locations
