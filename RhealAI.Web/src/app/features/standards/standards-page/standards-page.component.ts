import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatExpansionModule } from '@angular/material/expansion';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import { StandardsService } from '../../../core/services/standards.service';
import { Standard } from '../../../models';

@Component({
  selector: 'app-standards-page',
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatExpansionModule,
    LoadingSpinnerComponent
  ],
  templateUrl: './standards-page.component.html',
  styleUrl: './standards-page.component.scss'
})
export class StandardsPageComponent implements OnInit {
  standards: Standard[] = [];
  isLoading = true;
  repositoryId: string | null = null;

  get categorizedStandards(): Map<string, Standard[]> {
    const map = new Map<string, Standard[]>();
    this.standards.forEach(standard => {
      if (!map.has(standard.category)) {
        map.set(standard.category, []);
      }
      map.get(standard.category)!.push(standard);
    });
    return map;
  }

  get categories(): string[] {
    return Array.from(this.categorizedStandards.keys());
  }
  get existingStandardsCount(): number {
    return this.standards.filter(s => s.isFromExistingDocs).length;
  }

  get generatedStandardsCount(): number {
    return this.standards.filter(s => !s.isFromExistingDocs).length;
  }
  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private standardsService: StandardsService
  ) { }

  async ngOnInit(): Promise<void> {
    this.repositoryId = this.route.snapshot.paramMap.get('repositoryId');

    if (!this.repositoryId) {
      this.router.navigate(['/upload']);
      return;
    }

    try {
      this.standards = await this.standardsService.getStandardsByRepository(this.repositoryId).toPromise() || [];
    } catch (error) {
      console.error('Error loading standards:', error);
    } finally {
      this.isLoading = false;
    }
  }
}
