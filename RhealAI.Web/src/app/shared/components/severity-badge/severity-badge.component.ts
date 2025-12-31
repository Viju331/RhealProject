import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeverityLevel } from '../../../models';

@Component({
  selector: 'app-severity-badge',
  imports: [CommonModule],
  templateUrl: './severity-badge.component.html',
  styleUrl: './severity-badge.component.scss'
})
export class SeverityBadgeComponent {
  @Input() severity!: SeverityLevel | number | string;
  @Input() count?: number;

  getSeverityClass(): string {
    const severityStr = this.getSeverityString();
    return `severity-${severityStr.toLowerCase()}`;
  }

  getSeverityString(): string {
    // Handle numeric severity values from API
    if (typeof this.severity === 'number') {
      switch (this.severity) {
        case 1: return 'Low';
        case 2: return 'Medium';
        case 3: return 'High';
        case 4: return 'Critical';
        default: return 'Medium';
      }
    }
    // Handle string severity values
    return String(this.severity);
  }

  get displaySeverity(): string {
    return this.getSeverityString();
  }
}
