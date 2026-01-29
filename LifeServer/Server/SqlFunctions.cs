using System.Data.SQLite;
using System.Reflection;

namespace Server;

public class SqlFunctions {

    public static SQLiteConnection CreateConnection()
    {
        SQLiteConnection sqlite_conn;
        // Try multiple locations for the database
        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";

        // Locations to check (in order):
        // 1. Same directory as executable (for deployment)
        // 2. Parent directory (legacy)
        // 3. Project root (for development - search upward)
        string[] candidates = {
            Path.Combine(exeDir, "life.sqlite"),
            Path.Combine(exeDir, "..", "life.sqlite"),
        };

        string? dbPath = null;
        foreach (var candidate in candidates) {
            if (File.Exists(candidate)) {
                dbPath = Path.GetFullPath(candidate);
                break;
            }
        }

        // Search upward for life.sqlite (development scenario)
        if (dbPath == null) {
            string? dir = exeDir;
            while (dir != null) {
                string candidate = Path.Combine(dir, "life.sqlite");
                if (File.Exists(candidate)) {
                    dbPath = candidate;
                    break;
                }
                dir = Path.GetDirectoryName(dir);
            }
        }

        if (dbPath == null) {
            dbPath = Path.Combine(exeDir, "life.sqlite"); // Fallback
        }

        // Create a new database connection:
        sqlite_conn = new SQLiteConnection($"DataSource={dbPath};New=False");
        // Open the connection:
        try
        {
            sqlite_conn.Open();
        }
        catch (Exception ex)
        {
            Console.WriteLine("SQLite.open failed: " + ex);
        }
        return sqlite_conn;
    }

    /// <summary>
    /// Runs database migrations to add new columns if they don't exist
    /// </summary>
    public static void RunMigrations(SQLiteConnection conn) {
        // Add best_of column to lobbies if it doesn't exist
        if (!ColumnExists(conn, "lobbies", "best_of")) {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "ALTER TABLE lobbies ADD COLUMN best_of INTEGER DEFAULT 1";
            cmd.ExecuteNonQuery();
            Console.WriteLine("Migration: Added best_of column to lobbies table");
        }
    }

    private static bool ColumnExists(SQLiteConnection conn, string tableName, string columnName) {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        return false;
    }

    public static List<int>? SqlGetDeckCards(SQLiteConnection conn, int deckId) {
        using var sqLiteCommand = conn.CreateCommand();
        sqLiteCommand.CommandText = "SELECT card_id " +
                                    "FROM deck_cards " +
                                    $"WHERE deck_id = {deckId}";
        using var sqLiteDataReader = sqLiteCommand.ExecuteReader();
        List<int> cards = new List<int>();
        if (sqLiteDataReader.HasRows) {
            while (sqLiteDataReader.Read()) {
                cards.Add(sqLiteDataReader.GetInt32(0));
            }
        }
        return cards;
    }
}
