START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410152455_AddStoreDefaults') THEN
    ALTER TABLE public."Users" ADD "Currency" text DEFAULT 'CAD';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410152455_AddStoreDefaults') THEN
    ALTER TABLE public."Users" ADD "FederalTaxRate" numeric(5,3) NOT NULL DEFAULT 5.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410152455_AddStoreDefaults') THEN
    ALTER TABLE public."Users" ADD "ProvincialTaxRate" numeric(5,3) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410152455_AddStoreDefaults') THEN
    ALTER TABLE public."Users" ADD "TaxInclusive" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410152455_AddStoreDefaults') THEN
    ALTER TABLE public."Users" ADD "SubscriptionExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410152455_AddStoreDefaults') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260410152455_AddStoreDefaults', '9.0.4');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260421041805_AddStripeFields') THEN
    ALTER TABLE public."Users" ADD "StripeCustomerId" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260421041805_AddStripeFields') THEN
    ALTER TABLE public."Users" ADD "StripeSubscriptionId" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260421041805_AddStripeFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260421041805_AddStripeFields', '9.0.4');
    END IF;
END $EF$;
COMMIT;

