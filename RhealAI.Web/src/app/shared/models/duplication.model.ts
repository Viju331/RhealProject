export interface CodeDuplication {
    id: string;
    duplicatedCode: string;
    locations: DuplicationLocation[];
    type: DuplicationType;
    lineCount: number;
    similarityPercentage: number;
    description: string;
    suggestion: string;
    impact: 'Critical' | 'High' | 'Medium' | 'Low';
    refactoringOptions: string[];
    estimatedEffort: string;
    detectedAt: Date;
}

export interface DuplicationLocation {
    filePath: string;
    startLine: number;
    endLine: number;
    methodName: string;
    className: string;
}

export type DuplicationType =
    | 'ExactMatch'
    | 'StructuralMatch'
    | 'LogicalMatch'
    | 'FunctionalMatch'
    | 'PartialMatch';
