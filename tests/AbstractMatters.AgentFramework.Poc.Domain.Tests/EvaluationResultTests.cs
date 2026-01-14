using AbstractMatters.AgentFramework.Poc.Domain.Evaluation;
using AwesomeAssertions;

namespace AbstractMatters.AgentFramework.Poc.Domain.Tests;

public class EvaluationResultTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsEvaluationResult()
    {
        // Arrange
        var runId = "run-123";
        var scorerName = "ResponseRelevance";
        var score = 0.85;

        // Act
        var result = EvaluationResult.Create(runId, scorerName, score);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.RunId.Should().Be(runId);
        result.ScorerName.Should().Be(scorerName);
        result.Score.Should().Be(score);
        result.EvaluatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Create_WithInvalidScore_ThrowsArgumentOutOfRangeException(double score)
    {
        // Act & Assert
        var act = () => EvaluationResult.Create("run-123", "Scorer", score);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*score*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyRunId_ThrowsArgumentException(string? runId)
    {
        // Act & Assert
        var act = () => EvaluationResult.Create(runId!, "Scorer", 0.5);
        act.Should().Throw<ArgumentException>().WithMessage("*runId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyScorerName_ThrowsArgumentException(string? scorerName)
    {
        // Act & Assert
        var act = () => EvaluationResult.Create("run-123", scorerName!, 0.5);
        act.Should().Throw<ArgumentException>().WithMessage("*scorerName*");
    }

    [Fact]
    public void WithReasoning_AddsReasoningToResult()
    {
        // Arrange
        var result = EvaluationResult.Create("run-123", "Scorer", 0.85);
        var reasoning = "The response was relevant and addressed the user's question directly.";

        // Act
        var updatedResult = result.WithReasoning(reasoning);

        // Assert
        updatedResult.Reasoning.Should().Be(reasoning);
        result.Reasoning.Should().BeNull(); // immutable
    }

    [Fact]
    public void WithMetadata_AddsMetadataToResult()
    {
        // Arrange
        var result = EvaluationResult.Create("run-123", "Scorer", 0.85);

        // Act
        var updatedResult = result
            .WithMetadata("model", "gpt-4o")
            .WithMetadata("tokens", "256");

        // Assert
        updatedResult.Metadata.Should().HaveCount(2);
        updatedResult.Metadata["model"].Should().Be("gpt-4o");
        updatedResult.Metadata["tokens"].Should().Be("256");
    }

    [Fact]
    public void IsPassing_WithScoreAboveThreshold_ReturnsTrue()
    {
        // Arrange
        var result = EvaluationResult.Create("run-123", "Scorer", 0.85);

        // Act & Assert
        result.IsPassing(threshold: 0.8).Should().BeTrue();
    }

    [Fact]
    public void IsPassing_WithScoreBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var result = EvaluationResult.Create("run-123", "Scorer", 0.75);

        // Act & Assert
        result.IsPassing(threshold: 0.8).Should().BeFalse();
    }

    [Fact]
    public void IsPassing_WithDefaultThreshold_UsesPointEight()
    {
        // Arrange
        var passingResult = EvaluationResult.Create("run-123", "Scorer", 0.81);
        var failingResult = EvaluationResult.Create("run-123", "Scorer", 0.79);

        // Act & Assert
        passingResult.IsPassing().Should().BeTrue();
        failingResult.IsPassing().Should().BeFalse();
    }
}
