-- Produkty
CREATE TABLE IF NOT EXISTS "Products"
(
    "Id"           uuid           NOT NULL DEFAULT uuidv7(),
    "Name"         varchar(200)   NOT NULL,
    "Price"        numeric(18, 2) NOT NULL,
    "Currency"     varchar(3)     NOT NULL,
    "CreatedAtUtc" timestamptz    NOT NULL DEFAULT now(),
    "UpdatedAtUtc" timestamptz    NOT NULL DEFAULT now(),
    "Version"      integer        NOT NULL DEFAULT 1, -- zwiększana przy każdej zmianie

    CONSTRAINT "PK_Products" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Products_Currencies_Currency" FOREIGN KEY ("Currency") REFERENCES "Currencies" ("Code"),
    CONSTRAINT "CK_Products_Name_NotEmpty" CHECK (TRIM("Name") <> ''),
    CONSTRAINT "CK_Products_Price_Positive" CHECK ("Price" > 0)
);
