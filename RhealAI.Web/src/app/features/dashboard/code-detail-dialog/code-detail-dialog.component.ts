import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { SeverityBadgeComponent } from '../../../shared/components/severity-badge/severity-badge.component';

export interface CodeDetailData {
    type: 'violation' | 'bug' | 'refactoring';
    title: string;
    severity?: string;
    filePath: string;
    lineNumber?: number;
    endLineNumber?: number;
    description: string;
    currentCode?: string;
    suggestion: string;
    impact?: string;
    reproductionSteps?: string[];
    additionalInfo?: any;
}

@Component({
    selector: 'app-code-detail-dialog',
    standalone: true,
    imports: [
        CommonModule,
        MatDialogModule,
        MatButtonModule,
        MatIconModule,
        MatTabsModule,
        SeverityBadgeComponent
    ],
    templateUrl: './code-detail-dialog.component.html',
    styleUrl: './code-detail-dialog.component.scss'
})
export class CodeDetailDialogComponent {
    constructor(
        public dialogRef: MatDialogRef<CodeDetailDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public data: CodeDetailData
    ) { }

    close(): void {
        this.dialogRef.close();
    }

    copyCode(code: string): void {
        navigator.clipboard.writeText(code);
    }

    getTypeIcon(): string {
        switch (this.data.type) {
            case 'violation':
                return 'warning';
            case 'bug':
                return 'bug_report';
            case 'refactoring':
                return 'build_circle';
            default:
                return 'info';
        }
    }

    getTypeColor(): string {
        switch (this.data.type) {
            case 'violation':
                return 'warn';
            case 'bug':
                return 'danger';
            case 'refactoring':
                return 'accent';
            default:
                return 'primary';
        }
    }

    getLineRangeDisplay(): string {
        if (!this.data.lineNumber) return '';

        if (this.data.endLineNumber && this.data.endLineNumber !== this.data.lineNumber) {
            return `Lines ${this.data.lineNumber}-${this.data.endLineNumber}`;
        }

        return `Line ${this.data.lineNumber}`;
    }
}
