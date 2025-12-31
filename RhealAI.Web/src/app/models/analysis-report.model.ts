import { SeverityLevel } from './severity-level.enum';

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
    totalViolations: number;
    totalBugs: number;
    violationsBySeverity: Map<SeverityLevel, number>;
    bugsBySeverity: Map<SeverityLevel, number>;
    executionTime: string;
    projectSummary?: ProjectSummary;
}
