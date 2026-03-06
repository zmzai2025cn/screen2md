# Coding Standards - Screen2MD Enterprise

This document defines the coding standards and best practices for the Screen2MD Enterprise project.

## General Principles

1. **Readability First**: Code is read more often than written
2. **Consistency**: Follow existing patterns in the codebase
3. **Simplicity**: Favor simple solutions over clever ones
4. **Testability**: Design for testability from the start

## Naming Conventions

### Classes and Interfaces

```csharp
// Classes: PascalCase, noun or noun phrase
public class CaptureService { }
public class ScreenCaptureEngine { }

// Interfaces: PascalCase with 'I' prefix
public interface ICaptureService { }
public interface IImageProcessor { }

// Abstract classes: PascalCase, often with 'Base' suffix
public abstract class CaptureEngineBase { }
```

### Methods

```csharp
// PascalCase, verb or verb phrase
public void CaptureScreen() { }
public async Task<Image> ProcessAsync() { }

// Boolean methods: prefix with 'Is', 'Has', 'Can'
public bool IsAvailable() { }
public bool HasChanges() { }
public bool CanCapture() { }
```

### Properties and Fields

```csharp
// Public properties: PascalCase
public string OutputDirectory { get; set; }

// Private fields: _camelCase with underscore prefix
private readonly string _outputDirectory;
private int _captureCount;

// Constants: UPPER_SNAKE_CASE
public const int DEFAULT_INTERVAL = 30;
private const string CONFIG_FILE = "config.json";

// Static readonly: PascalCase
public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
```

### Variables and Parameters

```csharp
// Local variables: camelCase
var captureService = new CaptureService();
int retryCount = 3;

// Method parameters: camelCase
public void Process(Image image, int quality)
```

## Code Organization

### File Structure

```
MyClass.cs
├── using statements
├── namespace declaration
├── class/interface declaration
│   ├── constants
│   ├── fields
│   ├── constructor
│   ├── public properties
│   ├── public methods
│   ├── internal methods
│   ├── protected methods
│   └── private methods
```

### Region Organization (Optional)

```csharp
public class MyService
{
    #region Constants
    private const int MaxRetries = 3;
    #endregion

    #region Fields
    private readonly ILogger _logger;
    #endregion

    #region Constructor
    public MyService(ILogger logger)
    {
        _logger = logger;
    }
    #endregion

    #region Public Methods
    public void DoWork() { }
    #endregion

    #region Private Methods
    private void Helper() { }
    #endregion
}
```
## Formatting

### Indentation and Spacing

```csharp
// Use 4 spaces for indentation (no tabs)
// Insert final newline
// Trim trailing whitespace

// Braces: Allman style (on new line)
public void Method()
{
    // code
}

// Single statement braces: always use
if (condition)
{
    DoSomething();
}

// Not allowed
if (condition) DoSomething();
```

### Line Length and Breaking

```csharp
// Maximum line length: 120 characters

// Break long parameter lists
public void Method(
    string parameter1,
    string parameter2,
    string parameter3)
{
    // code
}

// Break long expressions
var result = someObject
    .Method()
    .AnotherMethod()
    .FinalMethod();
```

## Language Features

### Async/Await

```csharp
// Always use async/await for asynchronous operations
public async Task<Result> GetDataAsync()
{
    var data = await _repository.GetAsync();
    return data;
}

// Avoid .Result or .Wait()
// BAD: var result = GetDataAsync().Result;
// GOOD: var result = await GetDataAsync();

// Use ConfigureAwait(false) in library code
await Task.Delay(100).ConfigureAwait(false);
```

### Null Handling

```csharp
// Use null-conditional operator
var name = person?.Name;

// Use null-coalescing operator
var displayName = name ?? "Unknown";

// Use pattern matching
if (obj is string s)
{
    // use s
}

// Use nullable reference types
public string? GetOptionalName() { }
```

### LINQ

```csharp
// Use meaningful variable names in LINQ
var result = items
    .Where(item => item.IsActive)
    .Select(item => new { item.Name, item.Value })
    .OrderBy(x => x.Name)
    .ToList();

// Avoid complex LINQ in performance-critical code
// Use explicit loops when needed
```

## Error Handling

### Exceptions

```csharp
// Use specific exceptions
throw new ArgumentNullException(nameof(parameter));
throw new InvalidOperationException("Service not initialized");

// Always include inner exception
 catch (Exception ex)
{
    throw new ServiceException("Operation failed", ex);
}

// Don't catch generic exceptions unless logging
// BAD:
// catch (Exception) { }

// GOOD:
// catch (Exception ex)
// {
//     _logger.LogError(ex, "Unexpected error");
//     throw;
// }
```

### Result Pattern (for expected failures)

```csharp
public class OperationResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string? Error { get; }
    
    public static OperationResult<T> Ok(T value) => 
        new(true, value, null);
    
    public static OperationResult<T> Fail(string error) => 
        new(false, default, error);
}
```

## Documentation

### XML Documentation

```csharp
/// <summary>
/// Captures the screen and saves to the specified directory.
/// </summary>
/// <param name="outputPath">The directory path for saving screenshots.</param>
/// <param name="options">Optional capture configuration.</param>
/// <returns>The capture result containing file paths and metadata.</returns>
/// <exception cref="ArgumentNullException">Thrown when outputPath is null.</exception>
/// <exception cref="DirectoryNotFoundException">Thrown when outputPath doesn't exist.</exception>
public CaptureResult Capture(
    string outputPath, 
    CaptureOptions? options = null)
```

### Inline Comments

```csharp
// Good: Explain why, not what
// Compensate for screen DPI scaling to ensure correct capture size
var scaledWidth = (int)(width * dpiScale);

// Bad: States the obvious
// Increment counter by 1
counter++;

// Good: Explain complex algorithm
// Use exponential backoff to avoid overwhelming the system
var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryCount));
```
## Testing

### Test Naming

```csharp
// Pattern: MethodName_StateUnderTest_ExpectedBehavior
[Fact]
public void CaptureAsync_WithInvalidPath_ShouldReturnFailure()

[Fact]
public void ProcessImage_WithValidInput_ShouldReturnCorrectResult()

[Theory]
[InlineData(100)]
[InlineData(200)]
public void Calculate_WithVariousInputs_ShouldReturnExpected(int input)
```

### Test Structure

```csharp
[Fact]
public async Task CaptureAsync_WithValidOptions_ShouldSucceed()
{
    // Arrange
    var service = CreateService();
    var options = new CaptureOptions { Quality = 95 };
    
    // Act
    var result = await service.CaptureAsync(options);
    
    // Assert
    Assert.True(result.Success);
    Assert.NotEmpty(result.FilePath);
}
```

### Test Categories

```csharp
[Fact]
[Trait("Category", "Unit")]
public void UnitTest() { }

[Fact]
[Trait("Category", "Integration")]
public void IntegrationTest() { }

[Fact]
[Trait("Category", "Performance")]
public void PerformanceTest() { }
```
## Performance

### General Guidelines

```csharp
// Use StringBuilder for string concatenation in loops
var sb = new StringBuilder();
foreach (var item in items)
{
    sb.Append(item.Name);
}

// Use async I/O for file operations
await File.WriteAllTextAsync(path, content);

// Avoid LINQ in hot paths
// Use explicit loops instead

// Use Span<T> for memory-efficient operations
public void Process(Span<byte> data) { }

// Use object pooling for high-frequency allocations
private static readonly ObjectPool<StringBuilder> _pool = 
    new DefaultObjectPoolProvider().CreateStringBuilderPool();
```

## Security

### Input Validation

```csharp
public void ProcessPath(string path)
{
    // Validate input
    if (string.IsNullOrWhiteSpace(path))
        throw new ArgumentException("Path cannot be empty", nameof(path));
    
    // Prevent path traversal
    var fullPath = Path.GetFullPath(path);
    var basePath = Path.GetFullPath(_baseDirectory);
    
    if (!fullPath.StartsWith(basePath))
        throw new SecurityException("Invalid path");
}
```
### Sensitive Data

```csharp
// Never log sensitive information
_logger.LogInformation("Processing user {UserId}", user.Id); // OK
// _logger.LogInformation("Password: {Password}", password); // NEVER

// Use secure storage for secrets
private readonly ISecureStorage _secureStorage;
```
## Tooling

### Required Tools

```bash
# Install global tools
dotnet tool install --global dotnet-format
dotnet tool install --global dotnet-coverage
dotnet tool install --global dotnet-reportgenerator-globaltool
```

### Pre-commit Checks

```bash
#!/bin/bash
# .git/hooks/pre-commit

dotnet format --verify-no-changes || exit 1
dotnet build --no-restore || exit 1
dotnet test --filter "Category=Unit" --no-build || exit 1
```
## Enforcement

- **IDE**: Use EditorConfig (provided in repository)
- **Build**: Treat warnings as errors in Release
- **CI**: Automated format checking in GitHub Actions
- **Review**: Code review checklist includes style compliance

## References

- [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/inside-a-program/coding-conventions)
- [Framework Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/)
- [Clean Code](https://www.amazon.com/Clean-Code-Handbook-Software-Craftsmanship/dp/0132350882)
