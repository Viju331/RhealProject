export interface Standard {
    id: string;
    name: string;
    description: string;
    category: string;
    techStack: string;
    priority: string;
    isFromExistingDocs: boolean;
    sourceFile?: string;
    examples: string[];
    tags: string[];
}
