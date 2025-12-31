import { Component, OnInit, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { SeverityBadgeComponent } from '../../../shared/components/severity-badge/severity-badge.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { RepositoryService } from '../../../core/services/repository.service';
import { AnalysisService } from '../../../core/services/analysis.service';
import { Repository, AnalysisReport, Violation, Bug, SeverityLevel } from '../../../models';
import { Refactoring } from '../../../models/refactoring.model';
import { CodeDuplication, DuplicationType } from '../../../shared/models/duplication.model';
import { RefactoringDetailDialogComponent } from '../refactoring-detail-dialog/refactoring-detail-dialog.component';
import { CodeDetailDialogComponent, CodeDetailData } from '../code-detail-dialog/code-detail-dialog.component';

@Component({
  selector: 'app-analysis-result-page',
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    MatTableModule,
    MatPaginatorModule,
    MatTooltipModule,
    MatDialogModule,
    StatCardComponent,
    SeverityBadgeComponent,
    LoadingSpinnerComponent
  ],
  templateUrl: './analysis-result-page.component.html',
  styleUrl: './analysis-result-page.component.scss'
})
export class AnalysisResultPageComponent implements OnInit, AfterViewInit {
  repository: Repository | null = null;
  report: AnalysisReport | null = null;
  isLoading = true;

  // Make enum accessible in template
  SeverityLevel = SeverityLevel;

  // Track which tabs have been loaded
  selectedTabIndex = 0;
  tabsLoaded = {
    violations: false,
    bugs: false,
    refactorings: false,
    duplications: false
  };

  // Store raw data for lazy loading
  rawViolations: Violation[] = [];
  rawBugs: Bug[] = [];
  rawRefactorings: Refactoring[] = [];
  rawDuplications: CodeDuplication[] = [];

  // Use MatTableDataSource for efficient pagination
  allViolations = new MatTableDataSource<Violation>([]);
  allBugs = new MatTableDataSource<Bug>([]);
  allRefactorings = new MatTableDataSource<Refactoring>([]);
  allDuplications = new MatTableDataSource<CodeDuplication>([]);

  // Paginator references - static: false for lazy-loaded tabs
  @ViewChild('violationsPaginator', { static: false }) violationsPaginator?: MatPaginator;
  @ViewChild('bugsPaginator', { static: false }) bugsPaginator?: MatPaginator;
  @ViewChild('refactoringsPaginator', { static: false }) refactoringsPaginator?: MatPaginator;
  @ViewChild('duplicationsPaginator', { static: false }) duplicationsPaginator?: MatPaginator;

  violationColumns = ['severity', 'file', 'line', 'rule', 'description', 'actions'];
  bugColumns = ['severity', 'title', 'file', 'impact', 'actions'];
  refactoringColumns = ['priority', 'type', 'file', 'line', 'title', 'actions'];
  duplicationColumns = ['impact', 'type', 'similarity', 'locations', 'description', 'effort', 'actions'];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private repositoryService: RepositoryService,
    private analysisService: AnalysisService,
    private dialog: MatDialog
  ) { }

  async ngOnInit(): Promise<void> {
    const repositoryId = this.route.snapshot.paramMap.get('id');

    if (!repositoryId) {
      this.router.navigate(['/upload']);
      return;
    }

    try {
      // Fetch repository details
      this.repository = (await this.repositoryService.getRepository(repositoryId).toPromise()) || null;

      // Fetch actual analysis report from the API
      // The report should be cached from the analysis endpoint
      const reportId = this.route.snapshot.queryParamMap.get('reportId');

      if (reportId) {
        // Fetch the full report
        let reportResponse: any = await this.analysisService.getReport(reportId).toPromise();

        // If the response is a string (JSON string), parse it
        if (typeof reportResponse === 'string') {
          reportResponse = JSON.parse(reportResponse);
        }

        console.log('Report Response:', reportResponse);
        console.log('Violations:', reportResponse.violations);
        console.log('Bugs:', reportResponse.bugs);
        console.log('Project Summary:', reportResponse.projectSummary);

        // Parse the API response into our model
        this.report = {
          id: reportResponse.id || reportId,
          repositoryId: repositoryId,
          generatedDate: new Date(reportResponse.generatedAt || Date.now()),
          totalFiles: reportResponse.totalFiles || 0,
          filesWithViolations: reportResponse.filesWithViolations || 0,
          filesWithBugs: reportResponse.filesWithBugs || 0,
          filesNeedingRefactoring: reportResponse.filesNeedingRefactoring || 0,
          filesWithDuplications: reportResponse.filesWithDuplications || 0,
          totalViolations: reportResponse.totalViolations || 0,
          totalBugs: reportResponse.totalBugs || 0,
          totalRefactorings: reportResponse.totalRefactorings || 0,
          totalDuplications: reportResponse.totalDuplications || 0,
          totalDuplicatedLines: reportResponse.totalDuplicatedLines || 0,
          executionTime: reportResponse.executionTime || '0s',
          violationsBySeverity: this.parseSeverityMap(reportResponse.violationsBySeverity),
          bugsBySeverity: this.parseSeverityMap(reportResponse.bugsBySeverity),
          refactoringsByPriority: reportResponse.refactoringsByPriority || {},
          duplicationsByImpact: reportResponse.duplicationsByImpact || {},
          projectSummary: reportResponse.projectSummary,
          violations: reportResponse.violations || [],
          bugs: reportResponse.bugs || [],
          refactorings: reportResponse.refactorings || [],
          duplications: reportResponse.duplications || [],
          standards: reportResponse.standards || []
        };

        // Store raw data for lazy loading
        this.rawViolations = (this.report.violations || []).map((v: any) => ({
          ...v,
          fullPath: this.formatFilePath(v.filePath || ''),
          fileName: this.extractFileName(v.filePath || ''),
          severity: this.mapSeverity(v.severity)
        }));

        this.rawBugs = (this.report.bugs || []).map((b: any) => ({
          ...b,
          fullPath: this.formatFilePath(b.filePath || ''),
          fileName: this.extractFileName(b.filePath || ''),
          severity: this.mapSeverity(b.severity)
        }));

        this.rawRefactorings = (this.report.refactorings || []).map((r: any) => ({
          ...r,
          fullPath: this.formatFilePath(r.filePath || ''),
          fileName: this.extractFileName(r.filePath || ''),
          priority: this.mapSeverity(r.priority)
        }));

        this.rawDuplications = (this.report.duplications || []).map((d: any) => ({
          ...d,
          impact: this.mapSeverity(d.impact)
        }));

        // Load only Violations tab initially
        this.loadTabData(0);

        console.log('Raw data stored - Violations:', this.rawViolations.length);
        console.log('Bugs:', this.rawBugs.length);
        console.log('Refactorings:', this.rawRefactorings.length);
        console.log('Duplications:', this.rawDuplications.length);
      } else {
        // Fallback: Aggregate violations and bugs from files if no report available
        console.warn('No reportId provided, using aggregated file data');

        if (this.repository) {
          const violations: Violation[] = [];
          const bugs: Bug[] = [];
          this.repository.files.forEach(file => {
            violations.push(...file.violations.map(v => ({ ...v, fileName: file.fileName })));
            bugs.push(...file.bugs.map(b => ({ ...b, fileName: file.fileName })));
          });
          this.allViolations.data = violations;
          this.allBugs.data = bugs;
        }

        this.report = {
          id: '1',
          repositoryId: repositoryId,
          generatedDate: new Date(),
          totalFiles: this.repository?.files.length || 0,
          filesWithViolations: new Set(this.allViolations.data.map((v: any) => v.fileName)).size,
          filesWithBugs: new Set(this.allBugs.data.map((b: any) => b.fileName)).size,
          filesNeedingRefactoring: 0,
          filesWithDuplications: 0,
          totalViolations: this.allViolations.data.length,
          totalBugs: this.allBugs.data.length,
          totalRefactorings: 0,
          totalDuplications: 0,
          totalDuplicatedLines: 0,
          executionTime: '0s',
          violationsBySeverity: this.getCountBySeverity(this.allViolations.data),
          bugsBySeverity: this.getCountBySeverity(this.allBugs.data),
          refactoringsByPriority: {},
          duplicationsByImpact: {}
        };
      }

    } catch (error) {
      console.error('Error loading dashboard:', error);
      // Create empty report on error
      this.report = {
        id: '1',
        repositoryId: repositoryId,
        generatedDate: new Date(),
        totalFiles: this.repository?.files.length || 0,
        filesWithViolations: 0,
        filesWithBugs: 0,
        filesNeedingRefactoring: 0,
        filesWithDuplications: 0,
        totalViolations: 0,
        totalBugs: 0,
        totalRefactorings: 0,
        totalDuplications: 0,
        totalDuplicatedLines: 0,
        executionTime: '0s',
        violationsBySeverity: new Map(),
        bugsBySeverity: new Map(),
        refactoringsByPriority: {},
        duplicationsByImpact: {}
      };
    } finally {
      this.isLoading = false;
    }
  }

  ngAfterViewInit(): void {
    // Connect paginator for initially loaded tab (Violations)
    setTimeout(() => {
      if (this.violationsPaginator && this.tabsLoaded.violations) {
        this.allViolations.paginator = this.violationsPaginator;
      }
    }, 200);
  }

  onTabChange(event: any): void {
    this.selectedTabIndex = event.index;
    this.loadTabData(event.index);
  }

  private loadTabData(tabIndex: number): void {
    switch (tabIndex) {
      case 0: // Violations
        if (!this.tabsLoaded.violations) {
          console.log('Loading Violations tab with', this.rawViolations.length, 'items...');
          this.tabsLoaded.violations = true;
          setTimeout(() => {
            if (this.violationsPaginator) {
              this.allViolations.paginator = this.violationsPaginator;
              setTimeout(() => {
                this.allViolations.data = this.rawViolations;
              }, 50);
            } else {
              this.allViolations.data = this.rawViolations.slice(0, 10);
            }
          }, 100);
        }
        break;
      case 1: // Bugs
        if (!this.tabsLoaded.bugs) {
          console.log('Loading Bugs tab with', this.rawBugs.length, 'items...');
          this.tabsLoaded.bugs = true;
          setTimeout(() => {
            if (this.bugsPaginator) {
              this.allBugs.paginator = this.bugsPaginator;
              setTimeout(() => {
                this.allBugs.data = this.rawBugs;
              }, 50);
            } else {
              this.allBugs.data = this.rawBugs.slice(0, 10);
            }
          }, 100);
        }
        break;
      case 2: // Refactorings
        if (!this.tabsLoaded.refactorings) {
          console.log('Loading Refactorings tab with', this.rawRefactorings.length, 'items...');
          this.tabsLoaded.refactorings = true;

          // For large datasets, connect paginator BEFORE setting data
          setTimeout(() => {
            if (this.refactoringsPaginator) {
              this.allRefactorings.paginator = this.refactoringsPaginator;
              // Set data after paginator is connected
              setTimeout(() => {
                this.allRefactorings.data = this.rawRefactorings;
                console.log('Refactorings data loaded with pagination');
              }, 50);
            } else {
              console.warn('Refactorings paginator not found!');
              // Fallback: load only first page worth of data
              this.allRefactorings.data = this.rawRefactorings.slice(0, 10);
            }
          }, 100);
        }
        break;
      case 3: // Duplications
        if (!this.tabsLoaded.duplications) {
          console.log('Loading Duplications tab with', this.rawDuplications.length, 'items...');
          this.tabsLoaded.duplications = true;
          setTimeout(() => {
            if (this.duplicationsPaginator) {
              this.allDuplications.paginator = this.duplicationsPaginator;
              setTimeout(() => {
                this.allDuplications.data = this.rawDuplications;
              }, 50);
            } else {
              this.allDuplications.data = this.rawDuplications.slice(0, 10);
            }
          }, 100);
        }
        break;
    }
  }

  private parseSeverityMap(obj: any): Map<SeverityLevel, number> {
    const map = new Map<SeverityLevel, number>();
    if (obj) {
      Object.keys(obj).forEach(key => {
        const severity = SeverityLevel[key as keyof typeof SeverityLevel];
        if (severity !== undefined) {
          map.set(severity, obj[key]);
        }
      });
    }
    return map;
  }

  private getCountBySeverity(items: Array<{ severity: SeverityLevel }>): Map<SeverityLevel, number> {
    const map = new Map<SeverityLevel, number>();
    items.forEach(item => {
      map.set(item.severity, (map.get(item.severity) || 0) + 1);
    });
    return map;
  }

  getSeverityCount(severity: SeverityLevel, type: 'violations' | 'bugs'): number {
    const map = type === 'violations' ? this.report?.violationsBySeverity : this.report?.bugsBySeverity;
    return map?.get(severity) || 0;
  }

  private mapSeverity(severity: any): SeverityLevel {
    // If already a string, return as is
    if (typeof severity === 'string') {
      return severity as SeverityLevel;
    }
    // Convert numeric severity to enum
    switch (severity) {
      case 1: return SeverityLevel.Low;
      case 2: return SeverityLevel.Medium;
      case 3: return SeverityLevel.High;
      case 4: return SeverityLevel.Critical;
      default: return SeverityLevel.Medium;
    }
  }

  private formatFilePath(filePath: string): string {
    if (!filePath) return 'Unknown';
    // Return the full path with forward slashes for consistency
    return filePath.replace(/\\/g, '/');
  }

  private extractFileName(filePath: string): string {
    if (!filePath) return 'Unknown';
    // Extract just the filename from the full path
    const parts = filePath.replace(/\\/g, '/').split('/');
    return parts[parts.length - 1] || filePath;
  }

  exportReport(format: string): void {
    if (!this.report) return;

    if (format === 'json') {
      this.analysisService.exportReportJson(this.report.id).subscribe(blob => {
        this.downloadFile(blob, `report-${this.report!.id}.json`);
      });
    }
  }

  private downloadFile(blob: Blob, filename: string): void {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    window.URL.revokeObjectURL(url);
  }

  viewRefactoringDetails(refactoring: Refactoring): void {
    this.dialog.open(RefactoringDetailDialogComponent, {
      width: '900px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      data: refactoring,
      panelClass: 'refactoring-dialog'
    });
  }

  viewViolationDetails(violation: any): void {
    const dialogData: CodeDetailData = {
      type: 'violation',
      title: violation.ruleName || 'Code Violation',
      severity: violation.severity,
      filePath: violation.fullPath || violation.fileName || 'Unknown',
      lineNumber: violation.lineNumber,
      endLineNumber: violation.endLineNumber || violation.lineNumber,
      description: violation.description || 'No description available',
      currentCode: violation.codeSnippet || '',
      suggestion: violation.suggestedFix || 'Please review and fix this violation',
      additionalInfo: {
        details: `Rule: ${violation.ruleName}\nType: ${violation.type || 'N/A'}`
      }
    };

    this.dialog.open(CodeDetailDialogComponent, {
      width: '1200px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      data: dialogData,
      panelClass: 'code-detail-dialog'
    });
  }

  viewBugDetails(bug: any): void {
    const dialogData: CodeDetailData = {
      type: 'bug',
      title: bug.title || 'Bug Report',
      severity: bug.severity,
      filePath: bug.fullPath || bug.fileName || 'Unknown',
      lineNumber: bug.lineNumber || 0,
      endLineNumber: bug.endLineNumber || bug.lineNumber || 0,
      description: bug.description || 'No description available',
      currentCode: bug.codeSnippet || '',
      suggestion: bug.suggestedFix || 'Please review and fix this bug',
      impact: bug.impact,
      reproductionSteps: bug.reproductionSteps || [],
      additionalInfo: {
        details: `Impact: ${bug.impact}\nRoot Cause: ${bug.rootCause || 'N/A'}`
      }
    };

    this.dialog.open(CodeDetailDialogComponent, {
      width: '1200px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      data: dialogData,
      panelClass: 'code-detail-dialog'
    });
  }

  viewDuplicationDetails(duplication: CodeDuplication): void {
    const locationText = duplication.locations.map((loc, idx) =>
      `${idx + 1}. ${loc.filePath} (Lines ${loc.startLine}-${loc.endLine})`
    ).join('\n');

    const dialogData: CodeDetailData = {
      type: 'refactoring',
      title: 'Code Duplication Detected',
      severity: duplication.impact,
      filePath: duplication.locations[0]?.filePath || 'Multiple Files',
      lineNumber: duplication.locations[0]?.startLine,
      endLineNumber: duplication.locations[0]?.endLine,
      description: duplication.description || 'Duplicate code detected in multiple locations',
      currentCode: duplication.duplicatedCode || '',
      suggestion: `${duplication.suggestion || 'Consider extracting this code into a reusable method'}\n\nLocations:\n${locationText}\n\nEstimated Effort: ${duplication.estimatedEffort}`,
      additionalInfo: {
        details: `Type: ${this.formatDuplicationType(duplication.type)}\nSimilarity: ${duplication.similarityPercentage}%\nLine Count: ${duplication.lineCount}\nLocations: ${duplication.locations.length}`
      }
    };

    this.dialog.open(CodeDetailDialogComponent, {
      width: '1200px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      data: dialogData,
      panelClass: 'code-detail-dialog'
    });
  }

  formatDuplicationType(type: string | DuplicationType | undefined | null): string {
    // Handle null/undefined cases
    if (!type) {
      return 'Unknown';
    }

    // Ensure type is a string
    const typeStr = String(type);

    // Convert camelCase to readable format
    return typeStr.replace(/([A-Z])/g, ' $1').trim();
  }
}
