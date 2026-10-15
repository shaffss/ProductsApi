namespace Products.Core.Entities;

// Transactional Outbox - zdarzenie zapisane w transakcji zmiany danych, wysyłane do Kafki osobnym procesem
public sealed class OutboxMessage
{
    public long Id { get; set; } // kolejność wysyłki
    public required string Type { get; set; } // typ zdarzenia z wersją schematu (nagłówek event-type), np. ProductPriceChanged.v1
    public required string Topic { get; set; }
    public required string MessageKey { get; set; } // klucz partycji Kafki - kolejność per produkt
    public required string Payload { get; set; } // JSON zdarzenia
    public string? TraceParent { get; set; } // W3C trace context żądania, które zapisało zdarzenie (śledzenie przez Kafkę)
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; } // nieudane wysyłki z błędem trwałym; po limicie wiadomość trafia do dead-letter
    public string? LastError { get; set; }
}
