# Claude Code Assistant Configuration

This file contains authorized commands and configuration for Claude Code Assistant sessions.

## Authorized Commands

### Testing and Coverage

- `dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults` - Run tests with code coverage collection
- `reportgenerator -reports:"./TestResults/*/coverage.cobertura.xml" -targetdir:"./TestResults/CoverageReport" -reporttypes:Html` - Generate HTML coverage report
- `reportgenerator -reports:"./TestResults/*/coverage.cobertura.xml" -targetdir:"./TestResults/CoverageReport" -reporttypes:TextSummary` - Generate text summary coverage report

### Build and Development

- `dotnet build` - Build the solution
- `dotnet restore` - Restore NuGet packages
- `dotnet run --project <project_path>` - Run a .NET project (macOS/cross-platform)

### Platform-Specific Notes (macOS)

- **NEVER use mono or .exe files** - This is a .NET 8.0 project running natively on macOS
- **Use `dotnet run`** instead of compiling to .exe and running with mono
- **Use `dotnet <command>`** for all .NET operations (build, test, run, etc.)

### Git Operations

- `git status` - Check repository status
- `git add .` - Stage all changes
- `git commit -m "message"` - Commit changes
- `git pull` - Pull changes from remote
- `git push` - Push changes to remote

## Project Information

- **Target Framework**: .NET 8.0
- **Test Framework**: xUnit
- **Coverage Tool**: Coverlet
- **Report Generator**: ReportGenerator global tool

## Development Workflow Instructions

### Task Completion Tracking

When completing a set of tasks or phase milestones:

1. **Check the task list**: ALWAYS review the conversion plan to ensure all tasks in the current phase are complete
2. **Mark tasks completed**: when a task is in a docs md file, and [ ] available for that task, mark it with [x] when it is complete
3. **Add completion summary**: Include a dated summary section with key achievements
4. **Update project status**: Reflect current phase progress in README.md
5. **Commit changes**: Use descriptive commit messages and do not add a comment mentioning Claude Code, Anthropic, or any other code assistant in the commits messages, issues descriptions, PR or merges.

**IMPORTANT**: Before marking a phase as complete, systematically review ALL tasks in that phase section of the conversion plan to ensure nothing was missed. Check both main tasks and sub-tasks.

### Code Quality Standards

- Always write test in the tests/ assemblies for new code or code changes in the src/ directory
- Run `dotnet format` before committing to ensure consistent formatting
- Use the pre-commit hooks: `./scripts/setup-git-hooks.sh` (Linux/macOS) or `./scripts/setup-git-hooks.ps1` (Windows)
- Ensure all tests pass: `dotnet test`
- Generate coverage reports for significant changes

### Testing Requirements for Code Changes

**CRITICAL**: When making code changes or claiming fixes:

1. **Modify or create tests** - Update existing tests or create new tests to verify the fix
2. **Run tests before claiming completion** - Always run `dotnet test` to confirm fixes work
3. **Test implementation location** - Tests must be implemented in .NET within existing test folders and assemblies
4. **Verify the actual behavior** - Don't assume a fix works; verify it through tests and actual execution
5. **Update test expectations** - If behavior changes are intentional, update test expectations accordingly

### Documentation Updates

- Keep README.md current with latest .NET version and features
- Update local development setup guide when adding new tools or processes
- Maintain conversion plan progress tracking for transparency

## Rendering System Architecture Issues

### Current Problems (2025-08-11)

The rendering system has fundamental architectural problems that cause erratic output:

1. **Dual Rendering Approaches**: VirtualDomRenderer uses both:
   - `RenderElement` method for positioning and z-ordering
   - Visitor pattern (`Accept(this)`) for node-specific rendering
   - These approaches conflict, causing double-rendering and positioning issues

2. **Inconsistent Node Handling**:
   - FragmentNode: Special handling in `RenderElement` but empty `VisitFragment`
   - ClippingNode: Complex setup in `VisitClipping` but also processed by normal flow
   - TextNode: Only handled via visitor pattern
   - ElementNode: Handled by both approaches

3. **API Inconsistency**:
   - Some components use `Children.Add()` (not possible - readonly)
   - Others use `AddChild()` method  
   - Collection initializer syntax conflicts with fluent methods

### Required Fixes

1. **Unify Rendering**: Choose ONE approach - either visitor OR element-based
2. **Fix Node Processing**: Each node type should have ONE clear rendering path
3. **Consistent APIs**: All components should use same pattern for adding children
4. **Proper Clipping**: Implement clipping in the unified rendering approach

### Detailed Rendering Diagnostics

The comprehensive logging system captures:
- Layout calculations and constraints
- Virtual DOM tree construction and diffing  
- Rendering operations and positioning
- Focus management and state changes
- Performance metrics and timing

Use `ComprehensiveLoggingInitializer.Initialize(isTestMode: true)` in tests to enable detailed rendering logs for debugging.

## Notes

- Coverage reports are generated in `./TestResults/CoverageReport/`
- Integration tests may fail due to CLI execution context but unit tests provide good coverage
