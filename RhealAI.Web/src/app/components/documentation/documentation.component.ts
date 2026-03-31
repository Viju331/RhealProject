import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';

@Component({
    selector: 'app-documentation',
    standalone: true,
    imports: [
        CommonModule,
        MatCardModule,
        MatIconModule,
        MatTabsModule,
        MatExpansionModule,
        MatChipsModule
    ],
    templateUrl: './documentation.component.html',
    styleUrls: ['./documentation.component.scss']
})
export class DocumentationComponent {
    features = [
        {
            icon: 'psychology',
            title: 'AI-Powered Analysis',
            description: 'Comprehensive code analysis using advanced AI models including GPT-4o, GPT-4-turbo, and o3-mini',
            features: [
                'Multi-provider support (OpenAI, GitHub Models, Google Gemini)',
                'Intelligent code understanding with 128K token context',
                'Advanced reasoning for complex architectural patterns',
                'Real-time analysis progress tracking'
            ]
        },
        {
            icon: 'analytics',
            title: '7-Step Comprehensive Analysis',
            description: 'Deep project understanding through systematic analysis',
            features: [
                'Step 1: Project Structure Analysis (22%)',
                'Step 2: Standards Extraction (40%)',
                'Step 3: Violations Detection (70%)',
                'Step 4: Bug Detection (90%)',
                'Step 5: Refactoring Analysis (94%)',
                'Step 6: Code Duplication Detection (97%)',
                'Step 7: Report Generation (100%)'
            ]
        },
        {
            icon: 'bug_report',
            title: 'Smart Detection',
            description: 'Language-specific violation and bug detection',
            features: [
                'Context-aware suggestions for C#, JavaScript, TypeScript, SQL, Python',
                'Architectural pattern recognition',
                'Clean Architecture violation detection',
                'Cross-file dependency analysis'
            ]
        },
        {
            icon: 'build',
            title: 'Intelligent Refactoring',
            description: 'AI-suggested code improvements',
            features: [
                'Business logic separation recommendations',
                'Service layer extraction suggestions',
                'Method complexity reduction',
                'Code organization improvements'
            ]
        },
        {
            icon: 'content_copy',
            title: 'Duplication Detection',
            description: 'Find similar code across your project',
            features: [
                'Exact code duplication detection',
                'Similar logic identification',
                'Cross-file duplication analysis',
                'Consolidation recommendations'
            ]
        },
        {
            icon: 'cloud_upload',
            title: 'Flexible Upload',
            description: 'Multiple ways to analyze your code',
            features: [
                'ZIP file upload',
                'GitHub repository integration',
                'Local folder analysis',
                'Support for large projects (up to 2GB)'
            ]
        }
    ];

    aiProviders = [
        {
            name: 'OpenAI',
            models: ['gpt-4o', 'gpt-4-turbo', 'gpt-4.1'],
            description: 'Direct OpenAI integration with full token capacity (128K)',
            status: 'Paid - $5+ credits required'
        },
        {
            name: 'GitHub Models',
            models: ['gpt-4o-mini', 'gpt-4o', 'o3-mini'],
            description: 'Free AI models through GitHub Copilot subscription',
            status: 'Free with GitHub Copilot'
        },
        {
            name: 'Google Gemini',
            models: ['gemini-pro', 'gemini-1.5-pro', 'gemini-1.5-flash'],
            description: 'Google\'s Gemini AI models with large context windows',
            status: 'Free tier available'
        }
    ];

    technicalStack = [
        { category: 'Frontend', items: ['Angular 19.1', 'TypeScript', 'Angular Material', 'SignalR Client', 'Tailwind CSS'] },
        { category: 'Backend', items: ['.NET 10.0', 'ASP.NET Core API', 'Clean Architecture', 'SignalR'] },
        { category: 'AI Integration', items: ['OpenAI SDK 2.8.0', 'GitHub Models API', 'Google Gemini API'] },
        { category: 'Analysis Engine', items: ['ProjectContext System', 'Language-Specific Validators', 'Multi-Step Pipeline'] }
    ];

    analysisSteps = [
        {
            step: 1,
            name: 'Project Structure Analysis',
            progress: '7-22%',
            description: 'Deep understanding of your project architecture',
            details: [
                'Detects project modules (API, UI, Infrastructure, Domain, Application)',
                'Maps file languages (C#, TypeScript, JavaScript, SQL, etc.)',
                'Analyzes file dependencies and connections',
                'Identifies coding patterns (DI, async/await, Repository pattern)',
                'Detects error handling strategies',
                'Builds complete inventory of APIs, services, and database queries'
            ]
        },
        {
            step: 2,
            name: 'Standards Extraction',
            progress: '22-40%',
            description: 'Learns your project\'s coding standards',
            details: [
                'Analyzes existing code patterns',
                'Extracts naming conventions',
                'Identifies architectural standards',
                'Documents best practices used in the project'
            ]
        },
        {
            step: 3,
            name: 'Violations Detection',
            progress: '40-70%',
            description: 'Finds code that doesn\'t follow standards',
            details: [
                'Language-specific violation detection',
                'Architectural pattern violations',
                'Naming convention violations',
                'Best practice violations with context-aware suggestions'
            ]
        },
        {
            step: 4,
            name: 'Bug Detection',
            progress: '70-90%',
            description: 'Identifies potential bugs and issues',
            details: [
                'Logic errors and potential null references',
                'Async/await misuse',
                'Resource leak detection',
                'Exception handling issues'
            ]
        },
        {
            step: 5,
            name: 'Refactoring Analysis',
            progress: '90-94%',
            description: 'Suggests code improvements',
            details: [
                'Business logic in controllers → Move to services',
                'Large method extraction suggestions',
                'Code organization improvements',
                'Dependency injection recommendations'
            ]
        },
        {
            step: 6,
            name: 'Duplication Detection',
            progress: '94-97%',
            description: 'Finds duplicate or similar code',
            details: [
                'Exact code duplication',
                'Similar logic across files',
                'Repeated patterns',
                'Consolidation opportunities'
            ]
        },
        {
            step: 7,
            name: 'Report Generation',
            progress: '97-100%',
            description: 'Creates comprehensive analysis report',
            details: [
                'Detailed findings with file locations',
                'Severity classification',
                'Code snippets and examples',
                'Actionable recommendations'
            ]
        }
    ];

    getStartedSteps = [
        {
            number: 1,
            title: 'Configure AI Provider',
            description: 'Choose and configure your preferred AI provider in appsettings.Development.json',
            code: `{
  "AI": {
    "Provider": "OpenAI",  // or "GitHub" or "Gemini"
    "OpenAI": {
      "ApiKey": "your-api-key",
      "Model": "gpt-4o"
    }
  }
}`
        },
        {
            number: 2,
            title: 'Upload Your Code',
            description: 'Upload via ZIP file, GitHub URL, or select local folder',
            code: null
        },
        {
            number: 3,
            title: 'Start Analysis',
            description: 'Click "Analyze" and watch real-time progress through 7 comprehensive steps',
            code: null
        },
        {
            number: 4,
            title: 'Review Results',
            description: 'Navigate through Violations, Bugs, Refactorings, and Duplications tabs',
            code: null
        }
    ];
}
