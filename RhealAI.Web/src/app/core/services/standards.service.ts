import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Standard } from '../../models';
import { HttpOperationsService } from './http-operations.service';

@Injectable({
    providedIn: 'root'
})
export class StandardsService {
    constructor(private _http: HttpOperationsService) { }

    getStandardsByRepository(repositoryId: string): Observable<Standard[]> {
        return this._http.getAPI<Standard[]>(`standards/repository/${repositoryId}`);
    }
}
