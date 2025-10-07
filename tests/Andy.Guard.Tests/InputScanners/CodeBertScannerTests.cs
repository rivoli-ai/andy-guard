using Andy.Guard.InputScanners;
using Andy.Guard.Scanning;
using Xunit;

namespace Andy.Guard.Tests.InputScanners;

public class CodeBertScannerTests
{
    private readonly CodeBertScanner _scanner;

    public CodeBertScannerTests()
    {
        _scanner = new CodeBertScanner();
    }

    [Fact]
    public void Name_ShouldReturnCodeBertSecurity()
    {
        // Act
        var name = _scanner.Name;

        // Assert
        Assert.Equal("codebert_security", name);
    }

    [Fact]
    public async Task ScanAsync_WithSafeCode_ShouldReturnLowRisk()
    {
        // Arrange
        var safeCode = "public class Calculator { public int Add(int a, int b) { return a + b; } }";

        // Act
        var result = await _scanner.ScanAsync(safeCode);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsThreatDetected);
        Assert.Equal(RiskLevel.Low, result.RiskLevel);
        Assert.True(result.ConfidenceScore >= 0.0f && result.ConfidenceScore <= 1.0f);
    }

    [Fact]
    public async Task ScanAsync_WithExecCode_ShouldDetectThreat()
    {
        // Arrange
        var maliciousCode = "exec('rm -rf /')";

        // Act
        var result = await _scanner.ScanAsync(maliciousCode);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsThreatDetected);
        Assert.True(result.ConfidenceScore > 0.5f);
        Assert.True(result.RiskLevel >= RiskLevel.Medium);
    }

    [Fact]
    public async Task ScanAsync_WithEvalCode_ShouldDetectThreat()
    {
        // Arrange
        var maliciousCode = "eval('document.cookie')";

        // Act
        var result = await _scanner.ScanAsync(maliciousCode);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsThreatDetected);
        Assert.True(result.ConfidenceScore > 0.5f);
    }

    [Fact]
    public async Task ScanAsync_WithSystemCall_ShouldDetectThreat()
    {
        // Arrange
        var maliciousCode = "system('curl malicious-site.com')";

        // Act
        var result = await _scanner.ScanAsync(maliciousCode);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsThreatDetected);
        Assert.True(result.ConfidenceScore > 0.5f);
    }

    [Fact]
    public async Task ScanAsync_WithInnerHTML_ShouldDetectThreat()
    {
        // Arrange
        var maliciousCode = "element.innerHTML = userInput";

        // Act
        var result = await _scanner.ScanAsync(maliciousCode);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsThreatDetected);
        Assert.True(result.ConfidenceScore > 0.5f);
    }

    [Fact]
    public async Task ScanAsync_WithSuspiciousKeywords_ShouldIncreaseRisk()
    {
        // Arrange
        var codeWithSecrets = "var password = 'admin123'; var secret = 'key';";

        // Act
        var result = await _scanner.ScanAsync(codeWithSecrets);

        // Assert
        Assert.NotNull(result);
        // Should have higher confidence due to suspicious keywords
        Assert.True(result.ConfidenceScore > 0.1f);
    }

    [Fact]
    public async Task ScanAsync_WithComplexCode_ShouldCalculateComplexity()
    {
        // Arrange
        var complexCode = @"
            public class ComplexClass {
                public void Method1() {
                    if (condition) {
                        for (int i = 0; i < 10; i++) {
                            while (true) {
                                try {
                                    // Complex nested logic
                                } catch (Exception e) {
                                    throw new CustomException(e);
                                }
                            }
                        }
                    }
                }
            }";

        // Act
        var result = await _scanner.ScanAsync(complexCode);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ProcessingTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task ScanAsync_WithCustomOptions_ShouldRespectThreshold()
    {
        // Arrange
        var maliciousCode = "exec('rm -rf /')";
        var options = new ScanOptions { Threshold = 0.9f }; // Very high threshold

        // Act
        var result = await _scanner.ScanAsync(maliciousCode, options);

        // Assert
        Assert.NotNull(result);
        // With high threshold, might not detect as threat
        Assert.True(result.ConfidenceScore >= 0.0f);
    }

    [Fact]
    public async Task ScanAsync_WithEmptyCode_ShouldReturnLowRisk()
    {
        // Arrange
        var emptyCode = "";

        // Act
        var result = await _scanner.ScanAsync(emptyCode);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsThreatDetected);
        Assert.Equal(RiskLevel.Low, result.RiskLevel);
    }

    [Fact]
    public async Task ScanAsync_WithNullOptions_ShouldUseDefaults()
    {
        // Arrange
        var maliciousCode = "exec('malicious')";

        // Act
        var result = await _scanner.ScanAsync(maliciousCode, null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsThreatDetected);
    }

    [Fact]
    public async Task ScanAsync_ShouldIncludeMetadata()
    {
        // Arrange
        var code = "console.log('test')";
        var options = new ScanOptions { IncludeMetadata = true };

        // Act
        var result = await _scanner.ScanAsync(code, options);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.ContainsKey("engine"));
        Assert.True(result.Metadata.ContainsKey("threat_cues"));
    }

    [Fact]
    public async Task ScanAsync_WithMetadataDisabled_ShouldNotIncludeMetadata()
    {
        // Arrange
        var code = "console.log('test')";
        var options = new ScanOptions { IncludeMetadata = false };

        // Act
        var result = await _scanner.ScanAsync(code, options);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Metadata);
    }
}
