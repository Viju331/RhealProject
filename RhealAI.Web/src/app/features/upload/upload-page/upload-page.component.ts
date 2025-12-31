import { Component, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { FileUploadComponent, UploadSource } from '../../../shared/components/file-upload/file-upload.component';
import { RepositoryService } from '../../../core/services/repository.service';
import { AnalysisService } from '../../../core/services/analysis.service';
import { SignalRService } from '../../../core/services/signalr.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-upload-page',
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatSnackBarModule,
    FileUploadComponent
  ],
  templateUrl: './upload-page.component.html',
  styleUrl: './upload-page.component.scss'
})
export class UploadPageComponent implements OnDestroy {
  isUploading = false;
  isAnalyzing = false;
  uploadProgress = 0;
  selectedFile: File | null = null;
  uploadSource: UploadSource | null = null;
  repositoryId: string | null = null;
  processingStatus = '';

  private progressSubscription?: Subscription;
  private connectionId: string | null = null;

  constructor(
    private repositoryService: RepositoryService,
    private analysisService: AnalysisService,
    private router: Router,
    private snackBar: MatSnackBar,
    private signalRService: SignalRService
  ) { }

  ngOnDestroy(): void {
    if (this.progressSubscription) {
      this.progressSubscription.unsubscribe();
    }
    this.signalRService.stopConnection();
  }

  onFileSelected(file: File): void {
    this.selectedFile = file;
  }

  onSourceSelected(source: UploadSource): void {
    this.uploadSource = source;
    if (source.type === 'zip' && source.file) {
      this.selectedFile = source.file;
    }
  }

  async uploadAndAnalyze(): Promise<void> {
    if (!this.uploadSource) {
      this.snackBar.open('Please select a source first', 'Close', { duration: 3000 });
      return;
    }

    try {
      // Initialize SignalR connection
      await this.initializeSignalR();
      console.log('Using connectionId:', this.connectionId);

      // Upload/Process repository
      this.isUploading = true;
      this.uploadProgress = 0;

      let repository: any;

      switch (this.uploadSource.type) {
        case 'zip':
          if (!this.uploadSource.file) {
            throw new Error('No file selected');
          }
          this.processingStatus = 'Uploading ZIP file...';
          console.log('Uploading with connectionId:', this.connectionId);
          repository = await this.repositoryService.uploadRepository(
            this.uploadSource.file,
            this.connectionId || undefined
          ).toPromise();
          break;

        case 'folder':
          if (!this.uploadSource.folderPath) {
            throw new Error('No folder path provided');
          }
          this.processingStatus = 'Processing folder...';
          repository = await this.repositoryService.processFolder(
            this.uploadSource.folderPath
          ).toPromise();
          break;

        case 'github':
          if (!this.uploadSource.githubUrl) {
            throw new Error('No GitHub URL provided');
          }
          this.processingStatus = 'Cloning GitHub repository...';
          repository = await this.repositoryService.processGitHub(
            this.uploadSource.githubUrl,
            this.uploadSource.branch
          ).toPromise();
          break;
      }

      this.repositoryId = repository!.repositoryId;
      this.uploadProgress = 100;

      if (!this.repositoryId) {
        throw new Error('Repository ID not received from server');
      }

      this.snackBar.open('Repository loaded successfully!', 'Close', { duration: 3000 });

      // Start analysis
      await this.startAnalysis();
    } catch (error: any) {
      console.error('Error:', error);
      this.snackBar.open(error.message || 'An error occurred', 'Close', { duration: 5000 });
      this.isUploading = false;
      this.isAnalyzing = false;
    }
  }

  private async startAnalysis(): Promise<void> {
    this.isUploading = false;
    this.isAnalyzing = true;
    this.uploadProgress = 0;
    this.processingStatus = 'Starting AI analysis...';

    try {
      // Pass connectionId to analysis endpoint for progress updates
      const report: any = await this.analysisService.analyzeRepository(
        this.repositoryId!,
        this.connectionId || undefined
      ).toPromise();

      this.uploadProgress = 100;
      this.snackBar.open('Analysis completed!', 'Close', { duration: 3000 });

      // Navigate to dashboard with reportId
      this.router.navigate(['/dashboard', this.repositoryId], {
        queryParams: { reportId: report.reportId }
      });
    } catch (error) {
      console.error('Analysis failed:', error);
      this.snackBar.open('Analysis failed. Please try again.', 'Close', { duration: 5000 });
      this.isAnalyzing = false;
    }
  }

  private async initializeSignalR(): Promise<void> {
    try {
      this.connectionId = await this.signalRService.startConnection();
      console.log('SignalR initialized with connectionId:', this.connectionId);

      // Subscribe to progress updates
      this.progressSubscription = this.signalRService.progress$.subscribe(update => {
        console.log('Progress update received:', update);
        this.uploadProgress = update.progress;
        this.processingStatus = update.message;
      });
    } catch (error) {
      console.error('Failed to initialize SignalR:', error);
      // Continue without real-time progress
    }
  }

  getActivityIcon(): string {
    const message = this.processingStatus.toLowerCase();

    if (message.includes('folder') || message.includes('structure')) return 'folder_open';
    if (message.includes('documentation') || message.includes('readme')) return 'description';
    if (message.includes('standard') || message.includes('pattern')) return 'rule';
    if (message.includes('controller')) return 'api';
    if (message.includes('service') || message.includes('business logic')) return 'settings';
    if (message.includes('model') || message.includes('entity')) return 'storage';
    if (message.includes('repository')) return 'database';
    if (message.includes('interface')) return 'code';
    if (message.includes('analyzing') || message.includes('detection')) return 'search';
    if (message.includes('violation')) return 'warning';
    if (message.includes('bug')) return 'bug_report';
    if (message.includes('report') || message.includes('completed')) return 'check_circle';
    if (message.includes('ai') || message.includes('model')) return 'psychology';

    return 'autorenew';
  }
}
