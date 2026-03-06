# Contributing to Screen2MD Enterprise

Thank you for your interest in contributing to Screen2MD Enterprise! This document provides guidelines for contributing to the project.

## Development Setup

### Prerequisites

- .NET 8.0 SDK or later
- Git
- Docker (optional, for containerized development)
- Visual Studio 2022, VS Code, or JetBrains Rider

### Getting Started

```bash
# 1. Fork the repository
# 2. Clone your fork
git clone https://github.com/YOUR_USERNAME/screen2md.git
cd screen2md

# 3. Install tools
dotnet tool restore

# 4. Build the solution
dotnet build Screen2MD-v3.sln

# 5. Run tests
dotnet test
```

## Development Workflow

### 1. Create a Branch

```bash
git checkout -b feature/your-feature-name
# or
git checkout -b fix/issue-description
```

Branch naming conventions:
- `feature/description` - New features
- `fix/description` - Bug fixes
- `docs/description` - Documentation updates
- `refactor/description` - Code refactoring

### 2. Make Changes

- Write clean, maintainable code
- Follow the coding standards (see CODING_STANDARDS.md)
- Add/update tests as needed
- Update documentation

### 3. Test Your Changes

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet-coverage collect "dotnet test" -f xml -o coverage.xml

# Run specific category
dotnet test --filter "Category=Unit"
```

### 4. Commit Changes

Follow conventional commits format:

```
feat: add new feature X
fix: resolve issue with Y
docs: update API documentation
refactor: simplify logic in Z
test: add tests for feature X
chore: update dependencies
```

Commit message structure:
```
type(scope): subject

body (optional)

footer (optional)
```

### 5. Push and Create PR

```bash
git push origin feature/your-feature-name
```

Then create a Pull Request on GitHub.

## Pull Request Guidelines

### Before Submitting

- [ ] All tests pass locally
- [ ] Code coverage maintained or improved
- [ ] No compiler warnings
- [ ] Code formatted with `dotnet format`
- [ ] Documentation updated
- [ ] CHANGELOG.md updated (if applicable)

### PR Description Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing performed

## Checklist
- [ ] Code follows style guidelines
- [ ] Self-review completed
- [ ] Comments added for complex logic
- [ ] Documentation updated
```

### Review Process

1. Automated checks must pass (CI/CD)
2. At least one maintainer approval required
3. Address review comments
4. Squash commits if requested

## Testing Guidelines

### Unit Tests

- Place in `tests/{Project}.Tests/`
- Name: `{Class}Tests.cs`
- Use xUnit framework
- Follow Arrange-Act-Assert pattern

```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var service = new MyService();
    
    // Act
    var result = service.DoSomething();
    
    // Assert
    Assert.Equal(expected, result);
}
```

### Integration Tests

- Test component interactions
- Use test containers when possible
- Clean up resources after tests

### Performance Tests

- Use `Category=Performance` trait
- Set explicit timeout
- Document baseline expectations

## Documentation

### Code Documentation

- Public APIs must have XML documentation
- Complex algorithms need inline comments
- Use `<summary>`, `<param>`, `<returns>` tags

### User Documentation

- Update README.md for user-facing changes
- Update docs/ for detailed guides
- Include examples

## Code Review

### As a Reviewer

- Be constructive and respectful
- Focus on code, not the person
- Approve when satisfied, request changes otherwise
- Check for:
  - Correctness
  - Performance implications
  - Security considerations
  - Test coverage
  - Documentation

### As an Author

- Respond to all comments
- Explain rationale when disagreeing
- Make requested changes promptly
- Thank reviewers

## Reporting Issues

### Bug Reports

Include:
- Clear description
- Steps to reproduce
- Expected vs actual behavior
- Environment details (OS, version)
- Logs or error messages

### Feature Requests

Include:
- Use case description
- Proposed solution
- Alternatives considered
- Willingness to contribute

## Security

- Never commit secrets or credentials
- Report security issues privately
- Follow secure coding practices
- See SECURITY.md for details

## Community

- Be respectful and inclusive
- Help others in discussions
- Share knowledge
- Give credit where due

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Questions?

- GitHub Discussions: https://github.com/screen2md/discussions
- Discord: https://discord.gg/screen2md
- Email: dev@screen2md.ai
