import { CodeFile } from './code-file.model';
import { Standard } from './standard.model';

export interface Repository {
    id: string;
    name: string;
    uploadPath: string;
    uploadedDate: Date;
    hasExistingStandards: boolean;
    files: CodeFile[];
    standards: Standard[];
}
