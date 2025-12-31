import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-stat-card',
  imports: [CommonModule, MatIconModule],
  templateUrl: './stat-card.component.html',
  styleUrl: './stat-card.component.scss'
})
export class StatCardComponent {
  @Input() title!: string;
  @Input() value!: number | string;
  @Input() icon!: string;
  @Input() color: string = 'primary';
  @Input() trend?: { value: number; isPositive: boolean };
}
