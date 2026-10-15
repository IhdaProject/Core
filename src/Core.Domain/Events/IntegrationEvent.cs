namespace Core.Domain.Events;

public record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreationDate { get; init; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }
}
