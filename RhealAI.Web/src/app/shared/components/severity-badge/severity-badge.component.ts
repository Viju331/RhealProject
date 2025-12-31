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
  @Input() severity!: SeverityLevel;
  @Input() count?: number;

  getSeverityClass(): string {
    return `severity-${this.severity.toLowerCase()}`;
  }
}
