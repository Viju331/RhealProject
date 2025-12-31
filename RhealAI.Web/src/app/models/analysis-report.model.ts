import { SeverityLevel } from './severity-level.enum';
import { Violation } from './violation.model';
import { Bug } from './bug.model';
import { Standard } from './standard.model';
import { Refactoring } from './refactoring.model';
import { CodeDuplication } from '../shared/models/duplication.model';

export interface ProjectSummary {
    projectName: string;
    description: string;
    technologyStack: string;
    architecture: string;
    businessLogic: string;
    coreFunctionality: string;
    keyFeatures: string[];
    folderStructure: { [key: string]: number };
    fileTypeDistribution: { [key: string]: number };
    mainComponents: string[];
    primaryLanguage: string;
    dependencies: string[];
}

export interface AnalysisReport {
    id: string;
    repositoryId: string;
    generatedDate: Date;
    totalFiles: number;
    filesWithViolations: number;
    filesWithBugs: number;
    filesNeedingRefactoring: number;
    filesWithDuplications: number;
    totalViolations: number;
    totalBugs: number;
    totalRefactorings: number;
    totalDuplications: number;
    totalDuplicatedLines: number;
    violationsBySeverity: Map<SeverityLevel, number>;
    bugsBySeverity: Map<SeverityLevel, number>;
    refactoringsByPriority: { [key: string]: number };
    duplicationsByImpact: { [key: string]: number };
    executionTime: string;
    projectSummary?: ProjectSummary;
    violations?: Violation[];
    bugs?: Bug[];
    refactorings?: Refactoring[];
    duplications?: CodeDuplication[];
    standards?: Standard[];
}
