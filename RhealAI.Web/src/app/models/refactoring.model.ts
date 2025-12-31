export interface Refactoring {
    id: string;
    filePath: string;
    fileName?: string; // Added for UI display
    lineNumber: number;
    endLineNumber?: number;
    refactoringType: string;
    title: string;
    description: string;
    currentCode: string;
    suggestedCode: string;
    reason: string;
    benefits: string;
    priority: 'Critical' | 'High' | 'Medium' | 'Low';
    improvementAreas: string[];
}

export interface RefactoringsByPriority {
    [key: string]: number;
}
