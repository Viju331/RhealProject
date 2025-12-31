import { ViolationType } from './violation-type.enum';
import { SeverityLevel } from './severity-level.enum';

export interface Violation {
    id: string;
    fileId: string;
    lineNumber: number;
    ruleName: string;
    type: ViolationType;
    severity: SeverityLevel;
    description: string;
    codeSnippet: string;
    suggestedFix: string;
}
