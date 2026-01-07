import { Component, OnInit, SecurityContext } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatExpansionModule } from '@angular/material/expansion';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { AnalysisService } from '../../../core/services/analysis.service';
import { AnalysisReport } from '../../../models';

@Component({
  selector: 'app-report-detail',
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatExpansionModule,
    LoadingSpinnerComponent
  ],
  templateUrl: './report-detail.component.html',
  styleUrl: './report-detail.component.scss'
})
export class ReportDetailComponent implements OnInit {
  report: AnalysisReport | null = null;
  isLoading = true;
  htmlContent: SafeHtml | null = null;
  reportId: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private analysisService: AnalysisService,
    private sanitizer: DomSanitizer
  ) { }

  async ngOnInit(): Promise<void> {
    this.reportId = this.route.snapshot.paramMap.get('id');

    if (!this.reportId) {
      this.router.navigate(['/upload']);
      return;
    }

    try {
      // Load both the report data and HTML content
      const [report, htmlContent] = await Promise.all([
        this.analysisService.getReport(this.reportId).toPromise(),
        this.analysisService.getHtmlReport(this.reportId).toPromise()
      ]);

      this.report = report || null;

      // Sanitize HTML content for safe rendering
      if (htmlContent) {
        this.htmlContent = this.sanitizer.bypassSecurityTrustHtml(htmlContent);
      }
    } catch (error) {
      console.error('Error loading report:', error);
    } finally {
      this.isLoading = false;
    }
  }

  exportReport(format: 'json' | 'pdf' | 'html'): void {
    if (!this.reportId) return;

    if (format === 'json') {
      this.analysisService.exportReportJson(this.reportId).subscribe(blob => {
        this.downloadFile(blob, `RhealAI-Report-${this.reportId}.json`);
      });
    } else if (format === 'pdf') {
      this.analysisService.exportReportPdf(this.reportId).subscribe(blob => {
        this.downloadFile(blob, `RhealAI-Report-${this.reportId}.pdf`);
      });
    } else if (format === 'html') {
      this.analysisService.exportReportHtml(this.reportId).subscribe(blob => {
        this.downloadFile(blob, `RhealAI-Report-${this.reportId}.html`);
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
