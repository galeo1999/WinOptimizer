using System.Text.Json;
using WinOptimizer.Models;
using System.IO;
using System.Linq;

namespace WinOptimizer.Services;

public class StartupHistoryManager
{
    private readonly string _historyFile;

    public StartupHistoryManager()
    {
        string directory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                ),
                "WinOptimizer"
            );

        Directory.CreateDirectory(directory);

        _historyFile =
            Path.Combine(
                directory,
                "startup-history.json"
            );
    }

    public List<StartupHistoryEntry> LoadHistory()
    {
        if (!File.Exists(_historyFile))
        {
            return [];
        }

        string json =
            File.ReadAllText(_historyFile);

        return JsonSerializer.Deserialize<
                   List<StartupHistoryEntry>
               >(json)
               ?? [];
    }

    public void Add(
        StartupHistoryEntry entry)
    {
        List<StartupHistoryEntry> history =
            LoadHistory();

        history.Add(entry);

        Save(history);
    }

    public void Update(
        StartupHistoryEntry entry)
    {
        List<StartupHistoryEntry> history =
            LoadHistory();

        int index =
            history.FindIndex(
                item => item.Id == entry.Id
            );

        if (index == -1)
        {
            throw new InvalidOperationException(
                "Startup history entry was not found."
            );
        }

        history[index] = entry;

        Save(history);
    }

    private void Save(
        List<StartupHistoryEntry> history)
    {
        string json =
            JsonSerializer.Serialize(
                history,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }
            );

        File.WriteAllText(
            _historyFile,
            json
        );
    }
    public StartupHistoryEntry? GetById(
    string id)
{
    return LoadHistory()
        .FirstOrDefault(
            entry => entry.Id == id
        );
}
}