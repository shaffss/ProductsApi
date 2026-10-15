namespace Products.Persistence;

// Sekcja "Retention" konfiguracji: jak długo trzymać dane techniczne
public sealed class RetentionOptions
{
    public const string Section = "Retention";

    public bool Enabled { get; set; } = true;
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);
    public int BatchSize { get; set; } = 1000;

    // wysłane wiadomości (diagnostyka); dead-letter zostaje do ręcznej obsługi
    public TimeSpan OutboxMessages { get; set; } = TimeSpan.FromDays(7);

    // co najmniej retencja topicu Kafki (domyślnie 7 dni) - dłużej Kafka nie dostarczy ponownie starego zdarzenia
    public TimeSpan ProcessedEvents { get; set; } = TimeSpan.FromDays(7);

    // okno ponowień klienta; po nim ten sam klucz utworzy nowy produkt
    public TimeSpan IdempotencyKeys { get; set; } = TimeSpan.FromDays(1);
}
