namespace PinnyNotes.Core.Migrations;

public class Schema4To5Migration : SchemaMigration
{
    public override int TargetSchemaVersion => 4;
    public override int ResultingSchemaVersion => 5;
    public override string UpdateQuery => $@"
        -- Update Notes
        -- -- Add CreatedAt
        ALTER TABLE Notes
        ADD COLUMN CreatedAt INTEGER DEFAULT 0;

        -- -- Add ModifiedAt
        ALTER TABLE Notes
        ADD COLUMN ModifiedAt INTEGER DEFAULT 0;

        -- -- Back fill existing notes with the current time (unix ms)
        UPDATE Notes
        SET
            CreatedAt = CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER),
            ModifiedAt = CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER);

        -- Update schema version
        UPDATE SchemaInfo
        SET Version = {ResultingSchemaVersion}
        WHERE Id = 0;
    ";
}
