namespace PinnyNotes.Core.Migrations;

public class Schema5To6Migration : SchemaMigration
{
    public override int TargetSchemaVersion => 5;
    public override int ResultingSchemaVersion => 6;
    public override string UpdateQuery => $@"
        -- Update Settings
        -- -- Add Notes_ConfirmDelete
        ALTER TABLE Settings
        ADD COLUMN Notes_ConfirmDelete INTEGER DEFAULT 1;

        -- Update schema version
        UPDATE SchemaInfo
        SET Version = {ResultingSchemaVersion}
        WHERE Id = 0;
    ";
}
