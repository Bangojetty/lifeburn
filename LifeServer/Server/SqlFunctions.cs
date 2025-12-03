using System.Data.SQLite;

namespace Server; 

public class SqlFunctions {
    
    public static SQLiteConnection CreateConnection()
    {
        SQLiteConnection sqlite_conn;
        // Create a new database connection:
        sqlite_conn = new SQLiteConnection("DataSource=../life.sqlite;New=False");
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
