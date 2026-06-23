# COPILOT EDITS OPERATIONAL GUIDELINES

## PRIME DIRECTIVE

  Avoid working on more than one file at a time.
  Multiple simultaneous edits to a file will cause corruption.
  Be chatting and teach about what you are doing while coding.

## LARGE FILE & COMPLEX CHANGE PROTOCOL

### MANDATORY PLANNING PHASE

  When working with large files (>300 lines) or complex changes:
    1. ALWAYS start by creating a detailed plan BEFORE making any edits
          2. Your plan MUST include:
                  - All functions/sections that need modification
                  - The order in which changes should be applied
                  - Dependencies between changes
                  - Estimated number of separate edits required

          3. Format your plan as:

  ## PROPOSED EDIT PLAN

      Working with: [filename]
      Total planned edits: [number]

  ### MAKING EDITS

      - Focus on one conceptual change at a time
      - Show clear "before" and "after" snippets when proposing changes
      - Include concise explanations of what changed and why
      - Always check if the edit maintains the project's coding style

  ### Edit sequence:

      1. [First specific change] - Purpose: [why]
      2. [Second specific change] - Purpose: [why]
      3. Do you approve this plan? I'll proceed with Edit [number] after your confirmation.
      4. WAIT for explicit user confirmation before making ANY edits when user ok edit [number]

  ### EXECUTION PHASE

      - After each individual edit, clearly indicate progress:
        "✅ Completed edit [#] of [total]. Ready for next edit?"
      - If you discover additional needed changes during editing:
      - STOP and update the plan
      - Get approval before continuing


### REFACTORING GUIDANCE

    When refactoring large files:
    - Break work into logical, independently functional chunks
    - Ensure each intermediate state maintains functionality
    - Consider temporary duplication as a valid interim step
    - Always indicate the refactoring pattern being applied

### RATE LIMIT AVOIDANCE

    - For very large files, suggest splitting changes across multiple sessions
    - Prioritize changes that are logically complete units
    - Always provide clear stopping points

## Agent Engineering Principles
<!-- PORTABLE — safe to copy this entire section into any repo -->

### Root Cause First
- Identify and state the root cause before writing any fix.
- Do not patch symptoms. If the cause is unclear, say so and investigate before proceeding.
- Prefer a correct solution over a fast one that hides the problem.

### Scope Discipline
- Change only what the task requires. Do not refactor, rename, or restructure unrelated code.
- If you spot something worth improving outside of scope, note it as a follow-up item — do not act on it.

### Iterative Validation
- Work in small, verifiable steps. After each meaningful change, validate: run tests, check types, lint.
- If validation fails, fix the root cause of the failure — not just the failing check.

### Change Summary
- After completing a task, briefly state: what changed, why, and how it was validated.
- Call out risks, assumptions, or recommended follow-up items.

---

## General Requirements

  - Use modern technologies as described below for all code suggestions. 
  - Prioritize clean, maintainable code with appropriate comments.
  - Different parts of the codebase should be modular and reusable, use dependency injection where applicable.
  - Singular responsibility principle should be followed as possible.
  - Write the code that is easy to delete.

## Documentation Requirements
  - Use modern doc comments for .NET  
  - Document complex functions with clear examples.
  - Maintain concise Markdown documentation.
  - Minimum docblock info: `param`, `return`, `throws`

## Security Considerations

  - Sanitize all user inputs thoroughly.
  - Enforce strong Content Security Policies (CSP).
  - Use CSRF protection where applicable.
  - Ensure secure cookies (`HttpOnly`, `Secure`, `SameSite=Strict`).
  - Limit privileges and enforce role-based access control.
  - Implement detailed internal logging and monitoring.

## Context

- **Project Type**: An internal REST API service over PostgreSQL databases with Redis caching
- **Authentication**: API Key based authentication
- **Language**: Server: .NET Controller App
- **Framework / Libraries**: 
  - ASP.NET Core  
  - Entity Framework Core
  - Npgsql
  - ArchUnitNET (for architecture testing)
- **Architecture**: Clean Architecture with clear layer separation
- **Database**: PostgreSQL
- **Caching**: Redis with HybridCache


## 🔧 General Guidelines

- Organize code with clear separation of concerns.
- Controllers handle HTTP requests/responses only (in `Application/Features/`)
- Use `async`/`await` for all I/O operations.
- Use ISO strings for date/time passing and formatting.
- Run all the tests and ensure they pass before committing code.
- Run formatting and linting tools (CSharpier -> /.husky/task-runner.json) before committing code.


### Architecture Principles

**Layer Dependencies**:
- **Application** 
- **Shared** does NOT depend on Core or Application
- **Features** (siblings) do NOT depend on each other

**Feature Co-location**:
- Each feature under `Application/Features/` contains its own controller etc
- This allows features to be independently developed and tested

## Naming Conventions

- PascalCase for class names, method names, and properties.
- camelCase for local variables and method parameters.
- Avoid abbreviations and acronyms unless they are widely known and accepted.
- Use meaningful and descriptive names.
- Use the suffix Controller for all controller classes.
- Name controllers based on the resource they manage. For example, a controller managing products should be named ProductsController.
- Use verbs to describe the action performed by the method.
- Common verbs include Get, Post, Put, Delete, and Patch.
- For example, use GetProducts for a method that retrieves products.

## API principles
- DO use kebab-casing for URL path segments. If the segment refers to a JSON field, use camel casing.
- Use nouns, not verbs: /users and NOT /get-users.
- Use plural naming:
  - get the list of users GET /api/users
  - create new user POST /api/users
  - read a user by id GET /api/users/:id
- for the read operation, use the singular form of the resource in the URL, e.g. GET /api/users/123.
- for filtering, use query parameters, e.g. GET /api/garages?territory=FR&status=Active.


## 🧶 Patterns

### ✅ Patterns to Follow

- Code must be loosely coupled: all the modules should communicate through well-defined interfaces.
- Follow SOLID principles where applicable.

### 🚫 Patterns to Avoid

- Don’t put business logic directly in controllers.
- Don’t hardcode values — pull from config or env/config/appsettings.
- Avoid monolithic controllers — break down logic into services and helpers.

## 🧪 Testing Guidelines

- Use `xunit` for unit and integration tests.
- Always add DisplayName attribute to tests for clarity.
