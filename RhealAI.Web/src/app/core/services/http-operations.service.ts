import { HttpClient, HttpEvent, HttpHeaders, HttpParams, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class HttpOperationsService {
  private baseUrl: string;

  constructor(private http: HttpClient) {
    // Set base URL from environment
    this.baseUrl = environment.apiUrl;
  }

  public getBaseUrl(): string { 
    return this.baseUrl; 
  }

  public getAPI<T>(url: string, params?: HttpParams): Observable<T> {
    const headers = this.getHeader();
    const httpOptions = params
      ? { headers: headers, params: params }
      : { headers: headers };

    return this.http.get<T>(`${this.baseUrl}/${url}`, httpOptions);
  }

  public postAPI<T>(url: string, data: any): Observable<T> {
    const headers = this.getHeader();
    return this.http.post<T>(`${this.baseUrl}/${url}`, data, { headers });
  }

  public putAPI<T>(url: string, data: any): Observable<T> {
    const headers = this.getHeader();
    return this.http.put<T>(`${this.baseUrl}/${url}`, data, { headers });
  }

  public deleteAPI<T>(url: string): Observable<T> {
    const headers = this.getHeader();
    return this.http.delete<T>(`${this.baseUrl}/${url}`, { headers });
  }

  public uploadFile(url: string, formData: FormData): Observable<any> {
    // Don't set Content-Type for FormData, browser will set it with boundary
    return this.http.post(`${this.baseUrl}/${url}`, formData, {
      reportProgress: true
    });
  }

  public downloadFile(url: string, method: 'GET' | 'POST' = 'GET', data?: any): Observable<HttpEvent<Blob>> {
    const headers = new HttpHeaders({
      'Accept': 'application/octet-stream',
      'Access-Control-Expose-Headers': 'Content-Disposition'
    });

    return this.http.request(new HttpRequest(method, `${this.baseUrl}/${url}`, data, {
      headers: headers,
      reportProgress: true,
      responseType: 'blob'
    }));
  }

  public getDownloadFileAPI(url: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${url}`, {
      responseType: 'blob'
    });
  }

  private getHeader(): HttpHeaders {
    let headers = new HttpHeaders();
    headers = headers.set('Content-Type', 'application/json');
    return headers;
  }
}
