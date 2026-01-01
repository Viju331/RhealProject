import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { Standard } from '../../models';
import { HttpOperationsService } from './http-operations.service';

@Injectable({
    providedIn: 'root'
})
export class StandardsService {
    constructor(private _http: HttpOperationsService) { }

    getStandardsByRepository(repositoryId: string): Observable<any> {
        return this._http.getAPI<any>(`standards/repository/${repositoryId}`).pipe(
            map(response => {
                // Handle the new grouped response structure
                if (response.standardsByTechStack) {
                    return {
                        ...response,
                        standards: response.standardsByTechStack.flatMap((group: any) => 
                            group.standards.map((s: Standard) => ({
                                ...s,
                                techStack: group.techStack
                            }))
                        )
                    };
                }
                return response;
            })
        );
    }
}
