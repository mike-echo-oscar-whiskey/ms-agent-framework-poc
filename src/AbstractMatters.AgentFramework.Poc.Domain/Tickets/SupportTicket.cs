namespace AbstractMatters.AgentFramework.Poc.Domain.Tickets;

public class SupportTicket
{
    public Guid Id { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string CustomerId { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public SupportCategory? Category { get; private set; }
    public double? ClassificationConfidence { get; private set; }
    public TicketPriority? Priority { get; private set; }
    public string? AssignedAgentId { get; private set; }
    public string? Response { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    private SupportTicket() { }

    public static SupportTicket Create(string content, string customerId)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Ticket content cannot be empty.", nameof(content));
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer ID cannot be empty.", nameof(customerId));

        return new SupportTicket
        {
            Id = Guid.NewGuid(),
            Content = content,
            CustomerId = customerId,
            Status = TicketStatus.New,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Classify(SupportCategory category, double confidence)
    {
        if (confidence < 0 || confidence > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");

        Category = category;
        ClassificationConfidence = confidence;
        Status = TicketStatus.Classified;
    }

    public void SetPriority(TicketPriority priority)
    {
        Priority = priority;
    }

    public void AssignToAgent(string agentId)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("Agent ID cannot be empty.", nameof(agentId));

        AssignedAgentId = agentId;
        Status = TicketStatus.InProgress;
    }

    public void Resolve(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            throw new ArgumentException("Response cannot be empty.", nameof(response));

        Response = response;
        Status = TicketStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }
}
