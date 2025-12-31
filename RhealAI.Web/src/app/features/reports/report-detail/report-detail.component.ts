import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { SeverityBadgeComponent } from '../../../shared/components/severity-badge/severity-badge.component';
import { AnalysisService } from '../../../core/services/analysis.service';
import { AnalysisReport, SeverityLevel } from '../../../models';

@Component({
  selector: 'app-report-detail',
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatExpansionModule,
    LoadingSpinnerComponent,
    SeverityBadgeComponent
  ],
  templateUrl: './report-detail.component.html',
  styleUrl: './report-detail.component.scss'
})
export class ReportDetailComponent implements OnInit {
  report: AnalysisReport | null = null;
  isLoading = true;

  // Make enum accessible in template
  SeverityLevel = SeverityLevel;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private analysisService: AnalysisService
  ) { }

  async ngOnInit(): Promise<void> {
    const reportId = this.route.snapshot.paramMap.get('id');

    if (!reportId) {
      this.router.navigate(['/upload']);
      return;
    }

    try {
      this.report = (await this.analysisService.getReport(reportId).toPromise()) || null;
    } catch (error) {
      console.error('Error loading report:', error);
    } finally {
      this.isLoading = false;
    }
  }

  exportReport(format: 'json' | 'pdf'): void {
    if (!this.report) return;

    if (format === 'json') {
      this.analysisService.exportReportJson(this.report.id).subscribe(blob => {
        this.downloadFile(blob, `report-${this.report!.id}.json`);
      });
    } else if (format === 'pdf') {
      this.analysisService.exportReportPdf(this.report.id).subscribe(blob => {
        this.downloadFile(blob, `report-${this.report!.id}.pdf`);
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
