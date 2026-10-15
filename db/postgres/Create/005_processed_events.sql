-- Idempotentny konsument (inbox) - zdarzenia już obsłużone przez danego odbiorcę (grupę konsumentów Kafki)
CREATE TABLE IF NOT EXISTS "ProcessedEvents"
(
    "Consumer"       varchar(100) NOT NULL,
    "EventId"        uuid         NOT NULL,
    "ProcessedAtUtc" timestamptz  NOT NULL,

    CONSTRAINT "PK_ProcessedEvents" PRIMARY KEY ("Consumer", "EventId")
);

-- retencja: usuwanie starych wpisów
CREATE INDEX IF NOT EXISTS "IX_ProcessedEvents_ProcessedAtUtc"
    ON "ProcessedEvents" ("ProcessedAtUtc");
