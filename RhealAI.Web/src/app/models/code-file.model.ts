import { FileType } from './file-type.enum';
import { Violation } from './violation.model';
import { Bug } from './bug.model';

export interface CodeFile {
    id: string;
    filePath: string;
    fileName: string;
    fileType: FileType;
    content: string;
    lineCount: number;
    violations: Violation[];
    bugs: Bug[];
}
