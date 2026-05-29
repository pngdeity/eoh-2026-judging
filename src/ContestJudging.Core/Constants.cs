namespace ContestJudging.Core
{
    public static class Constants
    {
        public const string DatabaseFileName = "contest.db";
        public const string DefaultConnectionString = "Data Source=contest.db;foreign keys=true";
        public const string BackupStorageKey = "db_backup";
        public const string SchemaVersionStorageKey = "db_schema_version";

        public const int MaxBackupSizeBytes = 5 * 1024 * 1024;
        public const int SqliteHeaderLength = 16;
        public const int MinimumDatabaseFileSize = 100;

        public const double DivisorFloor = 1e-15;
    }
}
