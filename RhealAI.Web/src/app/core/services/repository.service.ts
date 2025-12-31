import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Repository } from '../../models';
import { HttpOperationsService } from './http-operations.service';

@Injectable({
    providedIn: 'root'
})
export class RepositoryService {
    constructor(private _http: HttpOperationsService) { }

    uploadRepository(file: File, connectionId?: string): Observable<Repository> {
        const formData = new FormData();
        formData.append('file', file);

        const url = connectionId
            ? `repository/upload?connectionId=${connectionId}`
            : 'repository/upload';

        return this._http.uploadFile(url, formData);
    }

    uploadBatch(files: File[], connectionId?: string): Observable<Repository> {
        const formData = new FormData();
        files.forEach((file, index) => {
            formData.append('files', file, file.webkitRelativePath || file.name);
        });

        const url = connectionId
            ? `repository/upload-batch?connectionId=${connectionId}`
            : 'repository/upload-batch';

        return this._http.uploadFile(url, formData);
    }

    processFolder(folderPath: string, repositoryName?: string): Observable<Repository> {
        return this._http.postAPI('repository/folder', {
            folderPath,
            repositoryName
        });
    }

    processGitHub(githubUrl: string, branch?: string): Observable<Repository> {
        return this._http.postAPI('repository/github', {
            githubUrl,
            branch
        });
    }

    getRepository(id: string): Observable<Repository> {
        return this._http.getAPI<Repository>(`repository/${id}`);
    }
}
