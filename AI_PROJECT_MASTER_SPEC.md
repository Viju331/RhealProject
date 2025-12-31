PROJECT BRIEF FOR COPILOT / CLAUDE AGENT

(Final – Includes Existing .md Handling)

Project Name

Rheal AI Project Inspector

Objective

Build an AI-powered platform from scratch that can analyze an entire software repository, understand its business logic, read and respect existing documentation, enforce or generate coding standards, detect bugs, and explain how to reproduce those bugs from the UI.

Existing Documentation Rule (Very Important)

If the uploaded repository already contains .md files (coding standards, architecture rules, error handling, API rules, UI rules, etc.):

The system must read, understand, and treat them as the source of truth

No new standards should override existing ones

Only missing sections may be extended, clearly marked as AI-added

If no coding standards exist:

The system must derive standards from the codebase

Generate clean, project-specific .md files

What the System Must Do

Accept a full repository folder / ZIP

Analyze all relevant files (UI, API, DB, configs, docs)

Ignore non-essential folders (node_modules, bin, obj, .git, dist)

Understand:

Project structure

Data flow

Business workflows

Detect presence of:

Coding standards

Architecture rules

Error handling guidelines

Enforce existing standards OR generate missing ones

Analyze the project and report:

Coding standard violations (file, line, rule)

Bugs and logical issues

For each detected bug:

Root cause

Impact

Exact UI steps to reproduce

Produce human-readable reports and actionable fixes

Required Tech Stack

Frontend: Angular (latest), standalone components, Tailwind CSS

Backend: .NET 8 Web API, clean architecture

AI: Claude Sonnet (long-context, multi-pass reasoning)

Architecture: Modular, scalable, SOLID-based

Architectural Rules (Mandatory)

No large or god components

Small, reusable components and services

Strict separation of concerns

AI reasoning isolated from controllers

Prompts stored centrally

Coding standards enforced per module

UI Expectations

Clean, modern, enterprise-grade UI

Extremely user-friendly

Dashboard with severity levels

File-level and rule-level drill-down

Demo-ready design

Output Expectations

Generate a complete project specification .md

Create frontend & backend from scratch

Follow and enforce existing .md rules if present

Produce clean, maintainable code

Include README and run instructions

Final Instruction to Agent

First, scan the repository and read all existing .md files.
If standards exist, adopt them as authoritative.
If missing, generate standards.
Then generate a full project specification and begin implementation from scratch.
