using System.IO;
using System.Text.Json;
using WinOptimizer.Models;

namespace WinOptimizer.Services;

public class RestoreManager
{
    private readonly string _historyFile;

    public RestoreManager()
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
                "history.json"
            );
    }

    public List<TweakHistoryEntry> LoadHistory()
    {
        if (!File.Exists(_historyFile))
        {
            return [];
        }

        string json =
            File.ReadAllText(_historyFile);

        return JsonSerializer.Deserialize<
                   List<TweakHistoryEntry>
               >(json)
               ?? [];
    }

    public void Add(
        TweakHistoryEntry entry)
    {
        List<TweakHistoryEntry> history =
            LoadHistory();

        history.Add(entry);

        Save(history);
    }

    public void Update(
        TweakHistoryEntry entry)
    {
        List<TweakHistoryEntry> history =
            LoadHistory();

        int index =
            history.FindIndex(
                item => item.Id == entry.Id
            );

        if (index == -1)
        {
            throw new InvalidOperationException(
                $"History entry '{entry.Id}' was not found."
            );
        }

        history[index] = entry;

        Save(history);
    }

    public TweakHistoryEntry? GetById(
        string id)
    {
        return LoadHistory()
            .FirstOrDefault(
                entry => entry.Id == id
            );
    }

    private void Save(
        List<TweakHistoryEntry> history)
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
}