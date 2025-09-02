# Contributing to Andy.Llm

Thank you for your interest in contributing to Andy.Llm! We welcome contributions from the community and are grateful for any help you can provide.

## Code of Conduct

By participating in this project, you agree to abide by our Code of Conduct:
- Be respectful and inclusive
- Welcome newcomers and help them get started
- Focus on constructive criticism
- Accept feedback gracefully

## How to Contribute

### Reporting Issues

1. Check existing issues to avoid duplicates
2. Use issue templates when available
3. Provide clear reproduction steps
4. Include relevant system information
5. Add logs or error messages when applicable

### Submitting Pull Requests

1. **Fork and Clone**
   ```bash
   git clone https://github.com/your-username/andy-llm.git
   cd andy-llm
   ```

2. **Create a Feature Branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

3. **Make Your Changes**
   - Follow existing code style and conventions
   - Add or update tests as needed
   - Update documentation if required

4. **Run Tests**
   ```bash
   dotnet test
   ```

5. **Commit Your Changes**
   ```bash
   git add .
   git commit -m "feat: add amazing feature"
   ```
   
   Follow conventional commits:
   - `feat:` new feature
   - `fix:` bug fix
   - `docs:` documentation changes
   - `test:` test additions or fixes
   - `refactor:` code refactoring
   - `perf:` performance improvements
   - `chore:` maintenance tasks

6. **Push and Create PR**
   ```bash
   git push origin feature/your-feature-name
   ```

### Development Setup

1. **Prerequisites**
   - .NET 8.0 SDK or later
   - Visual Studio 2022, VS Code, or JetBrains Rider
   - Git

2. **Build the Project**
   ```bash
   dotnet build
   ```

3. **Run Tests**
   ```bash
   # Run all tests
   dotnet test
   
   # Run with coverage
   dotnet test --collect:"XPlat Code Coverage"
   
   # Run specific test category
   dotnet test --filter "Category!=Integration"
   ```

4. **Code Style**
   - Use the provided .editorconfig
   - Run code formatting before committing:
     ```bash
     dotnet format
     ```

### Testing Guidelines

1. **Unit Tests**
   - Test individual components in isolation
   - Mock external dependencies
   - Aim for high code coverage
   - Keep tests fast and deterministic

2. **Integration Tests**
   - Test provider interactions
   - Use test configurations
   - Mark with `[Trait("Category", "Integration")]`

3. **Test Naming**
   - Use descriptive names: `MethodName_Scenario_ExpectedBehavior`
   - Example: `CompleteAsync_WithValidRequest_ReturnsResponse`

### Documentation

1. **Code Documentation**
   - Add XML comments to public APIs
   - Include examples where helpful
   - Document exceptions that may be thrown

2. **Markdown Documentation**
   - Update README.md for new features
   - Add to architecture docs for design changes
   - Keep examples current

### Adding New Providers

1. Implement `ILlmProvider` interface
2. Add configuration in `ProviderConfig`
3. Register in `LlmProviderFactory`
4. Add integration tests
5. Update documentation

### Performance Considerations

- Use async/await properly
- Implement cancellation tokens
- Consider memory allocations
- Profile before optimizing

## Review Process

1. All PRs require at least one review
2. CI checks must pass
3. Tests must maintain or increase coverage
4. Documentation must be updated

## Release Process

1. Maintainers handle releases
2. Follow semantic versioning
3. Update CHANGELOG.md
4. Tag releases in Git

## Getting Help

- Open an issue for bugs or features
- Start a discussion for questions
- Check documentation first
- Be patient and respectful

## Recognition

Contributors will be recognized in:
- Release notes
- Contributors list
- Project documentation

Thank you for contributing to Andy.Llm!