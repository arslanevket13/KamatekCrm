using Npgsql;
using System;

string[] passwords = { "1313", "123456" };
foreach (var pwd in passwords)
{
    var connString = $"Host=localhost;Port=5432;Database=postgres;Username=postgres;Password={pwd}";
    try
    {
        using var conn = new NpgsqlConnection(connString);
        conn.Open();
        Console.WriteLine($"SUCCESS: Connected with password '{pwd}'");
        return;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED: Password '{pwd}' - {ex.Message}");
    }
}
Console.WriteLine("Could not connect with any known password.");
