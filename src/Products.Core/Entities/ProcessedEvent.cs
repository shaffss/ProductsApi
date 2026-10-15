namespace Products.Core.Entities;

// Idempotentny konsument (inbox): zdarzenie już obsłużone przez danego odbiorcę.
// Consumer w kluczu - kilku odbiorców (grup konsumentów) na jednej bazie obsługuje to samo zdarzenie niezależnie.
public sealed class ProcessedEvent
{
    public required string Consumer { get; set; }
    public Guid EventId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}
