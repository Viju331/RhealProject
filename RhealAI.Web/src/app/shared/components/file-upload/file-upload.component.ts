import { Component, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormsModule } from '@angular/forms';

export interface UploadSource {
  type: 'zip' | 'folder' | 'github';
  file?: File;
  files?: File[];
  folderPath?: string;
  githubUrl?: string;
  branch?: string;
}

@Component({
  selector: 'app-file-upload',
  imports: [
    CommonModule,
    MatIconModule,
    MatButtonModule,
    MatTabsModule,
    MatInputModule,
    MatFormFieldModule,
    MatProgressSpinnerModule,
    FormsModule
  ],
  templateUrl: './file-upload.component.html',
  styleUrl: './file-upload.component.scss'
})
export class FileUploadComponent {
  @Output() fileSelected = new EventEmitter<File>();
  @Output() sourceSelected = new EventEmitter<UploadSource>();

  isDragging = false;
  selectedFile: File | null = null;
  isLoading = false;
  selectedFiles: File[] = [];
  fileCount = 0;

  // Local source (ZIP or Folder)
  folderPath: string = '';
  localSourceSelected: boolean = false;
  isZipFile: boolean = false;
  selectedSourceName: string = '';

  // GitHub input
  githubUrl: string = '';
  githubBranch: string = '';
  githubSelected: boolean = false;

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = true;
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = false;
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging = false;

    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.handleFile(files[0]);
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.isLoading = true;
      setTimeout(() => {
        this.handleFile(input.files![0]);
        this.isLoading = false;
      }, 300);
    }
  }



  private handleFile(file: File): void {
    if (file.name.endsWith('.zip')) {
      this.selectedFile = file;
      this.localSourceSelected = true;
      this.isZipFile = true;
      this.selectedSourceName = file.name;
      this.githubSelected = false;
      this.folderPath = '';
      this.fileSelected.emit(file);
      this.sourceSelected.emit({ type: 'zip', file });
    } else {
      alert('Please select a ZIP file');
    }
  }

  selectLocalSource(): void {
    if (this.folderPath.trim()) {
      // Check if the path ends with .zip
      const path = this.folderPath.trim();
      const isZip = path.toLowerCase().endsWith('.zip');

      this.localSourceSelected = true;
      this.isZipFile = isZip;
      this.selectedSourceName = path;
      this.githubSelected = false;
      this.selectedFile = null;

      if (isZip) {
        // Treat as ZIP file path
        this.sourceSelected.emit({
          type: 'zip',
          folderPath: path
        });
      } else {
        // Treat as folder path
        this.sourceSelected.emit({
          type: 'folder',
          folderPath: path
        });
      }
    }
  }

  selectGitHub(): void {
    if (this.githubUrl.trim()) {
      this.githubSelected = true;
      this.localSourceSelected = false;
      this.selectedFile = null;
      this.folderPath = '';
      this.sourceSelected.emit({
        type: 'github',
        githubUrl: this.githubUrl.trim(),
        branch: this.githubBranch.trim() || undefined
      });
    }
  }

  clearSelection(): void {
    this.selectedFile = null;
    this.selectedFiles = [];
    this.fileCount = 0;
    this.folderPath = '';
    this.githubUrl = '';
    this.githubBranch = '';
    this.localSourceSelected = false;
    this.githubSelected = false;
    this.isZipFile = false;
    this.selectedSourceName = '';
    this.isLoading = false;
  }

  getFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }
}
