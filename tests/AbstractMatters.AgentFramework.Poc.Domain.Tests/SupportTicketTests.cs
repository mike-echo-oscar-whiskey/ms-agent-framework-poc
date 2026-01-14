using AbstractMatters.AgentFramework.Poc.Domain.Tickets;
using AwesomeAssertions;

namespace AbstractMatters.AgentFramework.Poc.Domain.Tests;

public class SupportTicketTests
{
    [Fact]
    public void Create_WithValidContent_ReturnsTicket()
    {
        // Arrange
        var content = "I have a billing issue with my last invoice";
        var customerId = "cust-123";

        // Act
        var ticket = SupportTicket.Create(content, customerId);

        // Assert
        ticket.Content.Should().Be(content);
        ticket.CustomerId.Should().Be(customerId);
        ticket.Id.Should().NotBeEmpty();
        ticket.Status.Should().Be(TicketStatus.New);
        ticket.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyContent_ThrowsArgumentException(string? content)
    {
        // Act & Assert
        var act = () => SupportTicket.Create(content!, "cust-123");
        act.Should().Throw<ArgumentException>().WithMessage("*content*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyCustomerId_ThrowsArgumentException(string? customerId)
    {
        // Act & Assert
        var act = () => SupportTicket.Create("Some content", customerId!);
        act.Should().Throw<ArgumentException>().WithMessage("*customerId*");
    }

    [Fact]
    public void Classify_WithValidCategory_UpdatesTicket()
    {
        // Arrange
        var ticket = SupportTicket.Create("Billing issue", "cust-123");

        // Act
        ticket.Classify(SupportCategory.Billing, confidence: 0.95);

        // Assert
        ticket.Category.Should().Be(SupportCategory.Billing);
        ticket.ClassificationConfidence.Should().Be(0.95);
        ticket.Status.Should().Be(TicketStatus.Classified);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Classify_WithInvalidConfidence_ThrowsArgumentOutOfRangeException(double confidence)
    {
        // Arrange
        var ticket = SupportTicket.Create("Billing issue", "cust-123");

        // Act & Assert
        var act = () => ticket.Classify(SupportCategory.Billing, confidence);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*confidence*");
    }

    [Fact]
    public void SetPriority_WithValidPriority_UpdatesTicket()
    {
        // Arrange
        var ticket = SupportTicket.Create("Urgent billing issue", "cust-123");
        ticket.Classify(SupportCategory.Billing, 0.9);

        // Act
        ticket.SetPriority(TicketPriority.Urgent);

        // Assert
        ticket.Priority.Should().Be(TicketPriority.Urgent);
    }

    [Fact]
    public void AssignToAgent_WithValidAgentId_UpdatesTicket()
    {
        // Arrange
        var ticket = SupportTicket.Create("Technical issue", "cust-123");
        ticket.Classify(SupportCategory.Technical, 0.85);
        var agentId = "billing-specialist-agent";

        // Act
        ticket.AssignToAgent(agentId);

        // Assert
        ticket.AssignedAgentId.Should().Be(agentId);
        ticket.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public void Resolve_WithResponse_CompletesTicket()
    {
        // Arrange
        var ticket = SupportTicket.Create("Technical issue", "cust-123");
        ticket.Classify(SupportCategory.Technical, 0.85);
        ticket.AssignToAgent("tech-agent");
        var response = "Here's how to fix your issue...";

        // Act
        ticket.Resolve(response);

        // Assert
        ticket.Response.Should().Be(response);
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
