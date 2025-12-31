import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { AnalysisReport } from '../../models';
import { HttpOperationsService } from './http-operations.service';

@Injectable({
    providedIn: 'root'
})
export class AnalysisService {
    constructor(private _http: HttpOperationsService) { }

    analyzeRepository(repositoryId: string, connectionId?: string): Observable<AnalysisReport> {
        const url = connectionId
            ? `analysis/${repositoryId}/analyze?connectionId=${connectionId}`
            : `analysis/${repositoryId}/analyze`;
        return this._http.postAPI<AnalysisReport>(url, {});
    }

    getReport(reportId: string): Observable<AnalysisReport> {
        return this._http.getAPI<AnalysisReport>(`analysis/report/${reportId}`);
    }

    exportReportJson(reportId: string): Observable<Blob> {
        return this._http.getDownloadFileAPI(`analysis/report/${reportId}/export/json`);
    }

    exportReportPdf(reportId: string): Observable<Blob> {
        return this._http.getDownloadFileAPI(`analysis/report/${reportId}/export/pdf`);
    }
}
