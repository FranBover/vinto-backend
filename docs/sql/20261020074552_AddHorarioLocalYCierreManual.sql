BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020074552_AddHorarioLocalYCierreManual'
)
BEGIN
    ALTER TABLE [Administradores] ADD [CerradoManualmente] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020074552_AddHorarioLocalYCierreManual'
)
BEGIN
    ALTER TABLE [Administradores] ADD [ZonaHoraria] nvarchar(64) NOT NULL DEFAULT N'America/Argentina/Buenos_Aires';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020074552_AddHorarioLocalYCierreManual'
)
BEGIN
    CREATE TABLE [HorariosLocal] (
        [Id] int NOT NULL IDENTITY,
        [AdministradorId] int NOT NULL,
        [DiaSemana] int NOT NULL,
        [Modo] nvarchar(10) NOT NULL,
        [Turno1Apertura] time NULL,
        [Turno1Cierre] time NULL,
        [Turno2Apertura] time NULL,
        [Turno2Cierre] time NULL,
        CONSTRAINT [PK_HorariosLocal] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_HorariosLocal_DiaSemana] CHECK ([DiaSemana] BETWEEN 0 AND 6),
        CONSTRAINT [CK_HorariosLocal_Modo] CHECK ([Modo] IN (N'Cerrado', N'24h', N'Turnos')),
        CONSTRAINT [FK_HorariosLocal_Administradores_AdministradorId] FOREIGN KEY ([AdministradorId]) REFERENCES [Administradores] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020074552_AddHorarioLocalYCierreManual'
)
BEGIN
    CREATE UNIQUE INDEX [IX_HorariosLocal_AdministradorId_DiaSemana] ON [HorariosLocal] ([AdministradorId], [DiaSemana]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020074552_AddHorarioLocalYCierreManual'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020074552_AddHorarioLocalYCierreManual', N'9.0.4');
END;

COMMIT;
GO

