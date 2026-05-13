namespace CirilloCash.Services;

public static class TransactionsStorage
{
    public const string FileName = "transactions.txt";

    public static string FilePath => Path.Combine(FileSystem.AppDataDirectory, FileName);

    public static bool Exists() => File.Exists(FilePath);

    public static async Task AppendLineAsync(string line)
    {
        Directory.CreateDirectory(FileSystem.AppDataDirectory);
        await File.AppendAllTextAsync(FilePath, line + Environment.NewLine);
    }

    public static async Task<string> ReadAllAsync()
    {
        if (!Exists())
        {
            return string.Empty;
        }
        return await File.ReadAllTextAsync(FilePath);
    }

    public static void Delete()
    {
        if (Exists())
        {
            File.Delete(FilePath);
        }
    }
}
