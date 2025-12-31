import { SeverityLevel } from './severity-level.enum';

export interface Bug {
    id: string;
    fileId: string;
    title: string;
    description: string;
    rootCause: string;
    impact: string;
    severity: SeverityLevel;
    reproductionSteps: string[];
    suggestedFix: string;
}
