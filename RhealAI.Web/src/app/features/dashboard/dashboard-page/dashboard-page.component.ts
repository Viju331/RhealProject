import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { SeverityBadgeComponent } from '../../../shared/components/severity-badge/severity-badge.component';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { RepositoryService } from '../../../core/services/repository.service';
import { AnalysisService } from '../../../core/services/analysis.service';
import { Repository, AnalysisReport, Violation, Bug, SeverityLevel } from '../../../models';

@Component({
  selector: 'app-dashboard-page',
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    MatTableModule,
    StatCardComponent,
    SeverityBadgeComponent,
    LoadingSpinnerComponent
  ],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss'
})
export class DashboardPageComponent implements OnInit {
  repository: Repository | null = null;
  report: AnalysisReport | null = null;
  isLoading = true;

  // Make enum accessible in template
  SeverityLevel = SeverityLevel;

  allViolations: Violation[] = [];
  allBugs: Bug[] = [];

  violationColumns = ['severity', 'file', 'line', 'rule', 'description', 'actions'];
  bugColumns = ['severity', 'title', 'file', 'impact', 'actions'];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private repositoryService: RepositoryService,
    private analysisService: AnalysisService
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

      // Aggregate violations and bugs from all files for the detailed tables
      if (this.repository) {
        this.repository.files.forEach(file => {
          this.allViolations.push(...file.violations.map(v => ({ ...v, fileName: file.fileName })));
          this.allBugs.push(...file.bugs.map(b => ({ ...b, fileName: file.fileName })));
        });
      }

      // Fetch actual analysis report from the API
      // The report should be cached from the analysis endpoint
      const reportId = this.route.snapshot.queryParamMap.get('reportId');

      if (reportId) {
        // Fetch the full report
        const reportResponse: any = await this.analysisService.getReport(reportId).toPromise();

        // Parse the API response into our model
        this.report = {
          id: reportResponse.id || reportId,
          repositoryId: repositoryId,
          generatedDate: new Date(reportResponse.generatedAt || Date.now()),
          totalFiles: reportResponse.totalFiles || 0,
          filesWithViolations: reportResponse.filesWithViolations || 0,
          filesWithBugs: reportResponse.filesWithBugs || 0,
          totalViolations: reportResponse.totalViolations || 0,
          totalBugs: reportResponse.totalBugs || 0,
          executionTime: reportResponse.executionTime || '0s',
          violationsBySeverity: this.parseSeverityMap(reportResponse.violationsBySeverity),
          bugsBySeverity: this.parseSeverityMap(reportResponse.bugsBySeverity),
          projectSummary: reportResponse.projectSummary
        };
      } else {
        // Fallback: Create report from aggregated data if no reportId is provided
        console.warn('No reportId provided, using aggregated file data');
        this.report = {
          id: '1',
          repositoryId: repositoryId,
          generatedDate: new Date(),
          totalFiles: this.repository?.files.length || 0,
          filesWithViolations: new Set(this.allViolations.map((v: any) => v.fileName)).size,
          filesWithBugs: new Set(this.allBugs.map((b: any) => b.fileName)).size,
          totalViolations: this.allViolations.length,
          totalBugs: this.allBugs.length,
          executionTime: '0s',
          violationsBySeverity: this.getCountBySeverity(this.allViolations),
          bugsBySeverity: this.getCountBySeverity(this.allBugs)
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
        totalViolations: 0,
        totalBugs: 0,
        executionTime: '0s',
        violationsBySeverity: new Map(),
        bugsBySeverity: new Map()
      };
    } finally {
      this.isLoading = false;
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
}
