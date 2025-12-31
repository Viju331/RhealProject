import { Routes } from '@angular/router';
import { UploadPageComponent } from './features/upload/upload-page/upload-page.component';
import { AnalysisResultPageComponent } from './features/dashboard/analysis-result-page/analysis-result-page.component';
import { DashboardPageComponent } from './features/dashboard/dashboard-page/dashboard-page.component';
import { StandardsPageComponent } from './features/standards/standards-page/standards-page.component';
import { ReportDetailComponent } from './features/reports/report-detail/report-detail.component';

export const routes: Routes = [
    { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
    { path: 'upload', component: UploadPageComponent },
    { path: 'dashboard', component: DashboardPageComponent },
    { path: 'analysis-results', component: AnalysisResultPageComponent },
    { path: 'analysis-results/:id', component: AnalysisResultPageComponent },
    { path: 'standards', component: StandardsPageComponent },
    { path: 'standards/:repositoryId', component: StandardsPageComponent },
    { path: 'reports/:id', component: ReportDetailComponent },
    { path: '**', redirectTo: '/upload' }
];
