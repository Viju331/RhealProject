import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { SeverityBadgeComponent } from '../../../shared/components/severity-badge/severity-badge.component';
import { Refactoring } from '../../../models/refactoring.model';
import { SeverityLevel } from '../../../models';

@Component({
    selector: 'app-refactoring-detail-dialog',
    standalone: true,
    imports: [
        CommonModule,
        MatDialogModule,
        MatButtonModule,
        MatIconModule,
        MatChipsModule,
        SeverityBadgeComponent
    ],
    templateUrl: './refactoring-detail-dialog.component.html',
    styleUrl: './refactoring-detail-dialog.component.scss'
})
export class RefactoringDetailDialogComponent {
    severityLevel: SeverityLevel;

    constructor(
        public dialogRef: MatDialogRef<RefactoringDetailDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public data: Refactoring
    ) {
        // Convert priority string to SeverityLevel enum
        this.severityLevel = this.parsePriority(data.priority);
    }

    private parsePriority(priority: string): SeverityLevel {
        switch (priority?.toLowerCase()) {
            case 'critical':
                return SeverityLevel.Critical;
            case 'high':
                return SeverityLevel.High;
            case 'medium':
                return SeverityLevel.Medium;
            case 'low':
                return SeverityLevel.Low;
            default:
                return SeverityLevel.Low;
        }
    }

    close(): void {
        this.dialogRef.close();
    }

    getLineRangeDisplay(): string {
        if (!this.data.lineNumber) return '';

        if (this.data.endLineNumber && this.data.endLineNumber !== this.data.lineNumber) {
            return `Lines ${this.data.lineNumber}-${this.data.endLineNumber}`;
        }

        return `Line ${this.data.lineNumber}`;
    }
}
