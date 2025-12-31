# Code Redundancy Detection Feature

## Overview

Added comprehensive code duplication and redundancy detection functionality to analyze entire projects and identify duplicate code across multiple files.

## Features Implemented

### Backend (.NET)

1. **New Domain Entities**

   - `CodeDuplication.cs` - Represents detected code duplications with metadata
   - `DuplicationLocation.cs` - Tracks specific file locations where duplicated code appears
   - `DuplicationType` enum - Categorizes duplication types (ExactMatch, StructuralMatch, LogicalMatch, FunctionalMatch, PartialMatch)

2. **Updated Services**

   - `AIAnalysisService.DetectCodeDuplicationsAsync()` - AI-powered detection of code duplications
   - `GenerateMockDuplications()` - Demo mode with 3 example duplications:
     - Exact duplicate validation logic (2 locations, 100% similarity)
     - Similar API call patterns (3 locations, 95% similarity)
     - Duplicate data transformation logic (2 locations, 90% similarity)
   - `ReportService` - Updated to include duplication detection in analysis workflow (Step 5.6 at 95-97% progress)

3. **Updated Analysis Report**

   - Added `TotalDuplications` count
   - Added `TotalDuplicatedLines` metric
   - Added `FilesWithDuplications` count
   - Added `DuplicationsByImpact` severity breakdown
   - Added `Duplications` list to report

4. **API Updates**
   - `AnalysisController` - Returns duplication data in analysis response

### Frontend (Angular)

1. **New Models**

   - `duplication.model.ts` - TypeScript interfaces for CodeDuplication and DuplicationLocation

2. **Upload Page Enhancements**

   - Added "Redundancy Detection" feature card (pink gradient, content_copy icon)
   - Description: "Find duplicate code and redundant methods across your entire project"

3. **Dashboard Updates**

   - Added 2 new stats cards:
     - "Code Duplications" - Total count of duplication instances
     - "Duplicated Lines" - Total lines of duplicated code
   - Added "Duplications" tab with comprehensive table showing:
     - Impact badge (Critical/High/Medium/Low)
     - Duplication type (formatted from camelCase)
     - Similarity percentage badge (blue gradient)
     - Location count (number of files affected)
     - Description
     - Estimated effort badge
     - View details action button
   - Added `formatDuplicationType()` helper method
   - Added `viewDuplicationDetails()` method (currently shows alert, ready for future dialog component)

4. **Styling**
   - Pink/rose gradient theme for duplication-related UI elements
   - Similarity badge with blue gradient
   - Location count with orange badge
   - Effort badge with gray styling
   - Hover effects and smooth transitions

## AI Analysis Capabilities

The system detects:

- **Exact duplicate code blocks** - Identical code across files
- **Similar methods** - Same logic with different variable names
- **Repeated functionality** - Copy-pasted code with minor variations
- **Duplicate utility functions** - Redundant helper methods
- **Structural matches** - Similar code structure/patterns

## Suggestions Provided

For each duplication, the system provides:

1. **Duplicated code snippet**
2. **All file locations** (file path, line numbers, method/class names)
3. **Similarity percentage** (0-100%)
4. **Description** of what's duplicated
5. **Refactoring suggestions** to eliminate duplication
6. **Impact level** (Critical/High/Medium/Low)
7. **Refactoring options** (e.g., "Extract to utility method", "Create base class")
8. **Estimated effort** to fix (Low/Medium/High)

## Example Output

```json
{
  "duplicatedCode": "if (input == null || input.trim() == '') { throw new Error('Input cannot be empty'); }",
  "locations": [
    {
      "filePath": "src/services/UserService.ts",
      "startLine": 45,
      "endLine": 47,
      "methodName": "validateInput",
      "className": "UserService"
    },
    {
      "filePath": "src/validators/DataValidator.ts",
      "startLine": 123,
      "endLine": 125,
      "methodName": "checkData",
      "className": "DataValidator"
    }
  ],
  "type": "ExactMatch",
  "similarityPercentage": 100,
  "description": "Exact duplicate validation logic found in 2 files",
  "suggestion": "Extract this validation logic into a shared utility function",
  "impact": "High",
  "refactoringOptions": [
    "Extract to utility method",
    "Create validation service"
  ],
  "estimatedEffort": "Low"
}
```

## Progress Tracking

Analysis workflow now includes:

- **Step 5.6 (95%)**: "Scanning project for code duplications..."
- **Step 5.6 (97%)**: "Duplication detection complete: Found X duplications"

## Future Enhancements

1. Create `DuplicationDetailDialogComponent` to show:

   - All file locations with syntax-highlighted code
   - Side-by-side comparison of duplicated code
   - Interactive refactoring suggestions
   - One-click "Apply Fix" options

2. Add duplication severity chart to dashboard

3. Add filtering and sorting options for duplications table

4. Add "Group by file" view to see all duplications in a specific file

5. Add AI-powered automatic refactoring with preview

## Files Modified

**Backend:**

- `RhealAI.Domain/Entities/CodeDuplication.cs` (new)
- `RhealAI.Domain/Entities/DuplicationLocation.cs` (new)
- `RhealAI.Domain/Enums/DuplicationType.cs` (new)
- `RhealAI.Domain/Entities/AnalysisReport.cs`
- `RhealAI.Application/Interfaces/IAIAnalysisService.cs`
- `RhealAI.Infrastructure/Services/AIAnalysisService.cs`
- `RhealAI.Infrastructure/Services/ReportService.cs`
- `RhealAI.API/Controllers/AnalysisController.cs`

**Frontend:**

- `src/app/shared/models/duplication.model.ts` (new)
- `src/app/models/analysis-report.model.ts`
- `src/app/features/upload/upload-page/upload-page.component.html`
- `src/app/features/upload/upload-page/upload-page.component.scss`
- `src/app/features/dashboard/dashboard-page/dashboard-page.component.ts`
- `src/app/features/dashboard/dashboard-page/dashboard-page.component.html`
- `src/app/features/dashboard/dashboard-page/dashboard-page.component.scss`

## Build Status

✅ Backend builds successfully with no errors
✅ All services integrated into analysis workflow
✅ API endpoints return duplication data
✅ Frontend models and UI components added
✅ Feature cards updated on upload page (now 6 cards total)
