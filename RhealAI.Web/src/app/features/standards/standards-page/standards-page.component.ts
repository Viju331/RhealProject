import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
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
    RouterModule,
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
  standardsByTechStack: Map<string, Standard[]> = new Map();
  techStacks: string[] = [];
  isLoading = true;
  repositoryId: string | null = null;
  repositoryName: string = '';

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
      const response = await this.standardsService.getStandardsByRepository(this.repositoryId).toPromise();
      if (response) {
        this.repositoryName = response.repositoryName || '';
        this.standards = response.standards || [];
        this.techStacks = response.techStacks || [];
        
        // Group standards by tech stack
        this.standardsByTechStack = new Map();
        if (response.standardsByTechStack) {
          response.standardsByTechStack.forEach((group: any) => {
            this.standardsByTechStack.set(group.techStack, group.standards);
          });
        }
      }
    } catch (error) {
      console.error('Error loading standards:', error);
    } finally {
      this.isLoading = false;
    }
  }
  
  getStandardsByTechStack(techStack: string): Standard[] {
    return this.standardsByTechStack.get(techStack) || [];
  }
  
  getPriorityClass(priority: string): string {
    switch(priority?.toLowerCase()) {
      case 'critical': return 'priority-critical';
      case 'high': return 'priority-high';
      case 'medium': return 'priority-medium';
      default: return 'priority-low';
    }
  }
}
