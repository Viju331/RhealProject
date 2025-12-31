# Quick Test Guide - Refactoring Feature

## Test the New Feature

### Prerequisites

- Backend running: `dotnet run --project RhealAI.API`
- Frontend running: `npm start` (in RhealAI.Web folder)
- Any code project ready for upload

### Test Steps

#### 1. Start the Application

**Backend (Terminal 1)**

```bash
cd d:\RhealProject
dotnet run --project RhealAI.API
```

Expected: `Now listening on: http://localhost:5145`

**Frontend (Terminal 2)**

```bash
cd d:\RhealProject\RhealAI.Web
npm start
```

Expected: `Compiled successfully` and `http://localhost:4200`

#### 2. Upload a Project

1. Open browser: `http://localhost:4200`
2. Navigate to Upload page
3. Choose any option:
   - **Browse ZIP File**: Select a .zip project
   - **Browse Folder**: Click and select project folder
   - **Manual Path**: Type: `d:\RhealProject\RhealAI.API`

#### 3. Watch Real-Time Progress

You should see progress updates:

```
[5%]  Loading repository...
[42%] Starting standards compliance check...
[70%] Standards check complete: Found X violations
[72%] Starting bug detection...
[90%] Bug detection complete: Found X potential bugs
[91%] 🆕 Analyzing refactoring opportunities...     ⬅️ NEW!
[92%] 🆕 Checking Service for refactoring...        ⬅️ NEW!
[94%] 🆕 Refactoring analysis complete: Found 42 suggestions  ⬅️ NEW!
[96%] Generating final report...
[100%] Analysis completed!
```

#### 4. View Dashboard

Dashboard automatically loads with 8 stat cards:

**Existing Cards:**

1. Total Files Analyzed
2. Files with Violations
3. Files with Bugs
4. Total Violations
5. Total Bugs
6. Analysis Time

**🆕 New Cards:** 7. **Files Need Refactoring** - e.g., "15" 8. **Refactorings Suggested** - e.g., "42"

#### 5. Click Refactorings Tab

1. Click **"Refactorings (42)"** tab
2. View table with columns:
   - Priority (colored badges)
   - Type (styled chips)
   - File (filename only)
   - Line (number)
   - Issue (title + description)
   - Actions (view button)

#### 6. Inspect Refactoring Details

Example refactoring you might see:

```
Priority: 🔴 High
Type: Extract Method
File: AIAnalysisService.cs
Line: 250
Title: Long Method Detected
Description: This method has approximately 120 lines and should be
broken down into smaller, more focused methods.

Current Code:
public async Task AnalyzeCodeViolationsAsync(...)
{
    // 120 lines of mixed logic...
}

Suggested Code:
// Extract logical blocks into separate methods
// Example: ExtractValidationLogic(), ExtractAnalysisLogic()

Reason: Long methods are difficult to understand, test, and maintain.

Benefits: Improved readability, easier testing, better maintainability

Improvement Areas: [Readability] [Maintainability] [Testability]
```

### Expected Results

#### Demo Mode (Default)

- ✅ Refactorings based on actual code analysis
- ✅ Long methods detected (>50 lines)
- ✅ Deep nesting detected (>3 levels)
- ✅ Magic numbers detected
- ✅ Duplicate code detected
- ✅ Long parameter lists detected (>4 params)

#### Test Scenarios

**Scenario 1: Small Project (10 files)**

- Expected: 5-15 refactorings
- Most common: Magic numbers, some long methods

**Scenario 2: Medium Project (50 files)**

- Expected: 20-50 refactorings
- Common: All types, especially duplicates

**Scenario 3: Large Project (100+ files)**

- Expected: 50-150 refactorings
- Common: Many long methods, deep nesting, duplicates

### API Testing (Optional)

#### Test Analyze Endpoint

```bash
# 1. Upload a repository (example: manual path)
curl -X POST http://localhost:5145/api/repository/upload \
  -H "Content-Type: application/json" \
  -d '{"localPath": "d:\\RhealProject\\RhealAI.API"}'

# Response: { "repositoryId": "abc123" }

# 2. Analyze with connectionId for real-time updates
curl -X POST http://localhost:5145/api/analysis/abc123/analyze?connectionId=test123

# 3. Check response includes refactoring data:
{
  "filesNeedingRefactoring": 15,
  "totalRefactorings": 42,
  "refactoringsByPriority": {
    "Critical": 5,
    "High": 12,
    "Medium": 18,
    "Low": 7
  },
  "refactorings": [ ... ]
}

# 4. Get full report
curl http://localhost:5145/api/analysis/report/{reportId}
```

### Verification Checklist

- [ ] Backend builds without errors
- [ ] Frontend builds (budget warnings OK)
- [ ] Backend starts on port 5145
- [ ] Frontend starts on port 4200
- [ ] Upload works (any method)
- [ ] Real-time progress shows refactoring stage
- [ ] Dashboard shows 8 stat cards (not 6)
- [ ] "Files Need Refactoring" card displays
- [ ] "Refactorings Suggested" card displays
- [ ] "Refactorings" tab appears with count
- [ ] Refactoring table displays correctly
- [ ] Priority badges show (colored)
- [ ] Type chips show (styled)
- [ ] File names extracted from paths
- [ ] Line numbers displayed
- [ ] Titles and descriptions visible
- [ ] View button present (with tooltip)

### Common Issues & Solutions

#### Issue: "No refactorings detected"

**Solution**: Project might be too small. Try uploading the RhealAI solution itself:

```
Manual Path: d:\RhealProject
```

#### Issue: "Frontend won't build"

**Solution**: Budget warnings are OK. The app still works. Ignore size warnings.

#### Issue: "Tab doesn't show refactorings"

**Solution**: Check browser console for errors. Verify `allRefactorings` array has data:

```javascript
console.log(this.allRefactorings); // Should show array of refactorings
```

#### Issue: "Backend error during analysis"

**Solution**: Check you have enough sample code. At least 5-10 code files needed.

### Debug Mode

To see detailed refactoring detection:

1. Open browser DevTools (F12)
2. Go to Console tab
3. Upload and analyze a project
4. Look for log messages:
   - "Report Response" - full API response
   - "Mapped Refactorings" - frontend-mapped data

### Performance

**Expected Analysis Times**

- Small (10 files): ~5 seconds
- Medium (50 files): ~15 seconds
- Large (100 files): ~30 seconds
- Very Large (500+ files): ~2-3 minutes

**Progress Breakdown**

- Refactoring detection: ~3-4% of total time
- Appears at: 91-94% progress
- Processing: ~1-5 seconds depending on file count

### Sample Output

For the RhealAI.API project itself, expect:

```
Files Need Refactoring: 8-12 files
Total Refactorings: 25-40 suggestions

Top Issues:
1. Long methods in AIAnalysisService.cs
2. Magic numbers in various config files
3. Deep nesting in file processing logic
4. Duplicate validation code
5. Long parameter lists in service constructors
```

### Success Indicators

✅ **Feature Working Correctly If:**

1. Stats show non-zero refactoring counts
2. Refactorings tab contains actual data
3. Each refactoring has:
   - Valid file path
   - Line number >0
   - Non-empty description
   - Current code snippet
   - Suggested code
   - Priority level
4. Improvement areas displayed as tags
5. Type badges styled correctly

### Next Test: Real AI Mode

To test with actual OpenAI (optional):

1. Add API key to `appsettings.json`:

```json
{
  "AI": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "your-key-here",
      "Model": "gpt-4"
    }
  }
}
```

2. Re-run analysis
3. Expect more sophisticated suggestions
4. Results should include:
   - Context-aware refactorings
   - Framework-specific suggestions
   - Design pattern recommendations

---

**Test complete! Your refactoring feature is ready to help improve code quality! 🎉**
