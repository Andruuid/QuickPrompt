using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickPrompt;

public sealed class Prompt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Category { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    // Read old translated labels only to migrate and back them up. New prompts use shared labels.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NameDe { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DescriptionDe { get; set; }
    public string ContentDe { get; set; } = "";
    public bool NonBilingual { get; set; }

    public bool HasLanguage(string language) => !string.IsNullOrWhiteSpace(language == "de" ? ContentDe : Content);
    public string AvailableLanguage(string language) => HasLanguage(language) ? language : language == "de" ? "en" : "de";
    public string GetContent(string language) => language == "de" ? ContentDe : Content;
    public string GetName(string language) => string.IsNullOrWhiteSpace(Name) ? NameDe ?? "" : Name;
    public string GetDescription(string language) => Description;
    public bool RequiresLanguageWarning(string language) => !NonBilingual && !HasLanguage(language);
    public bool HasValidTranslations() => !string.IsNullOrWhiteSpace(GetName("en")) && Description is not null && Content is not null && ContentDe is not null
        && (HasLanguage("en") || HasLanguage("de")) && (!NonBilingual || HasLanguage("en") != HasLanguage("de"));
}

public sealed class Preferences
{
    public string Language { get; set; } = "en";
    public string Hotkey { get; set; } = "Alt+Space";
    public bool RememberCategory { get; set; } = true;
    public bool SearchAllCategories { get; set; } = true;
    public int LastCategory { get; set; }
    public string DocumentsFolder { get; set; } = @"C:\Launchy";
    public bool IncludeDocumentSubfolders { get; set; } = true;
    public string[] Categories { get; set; } = ["General", "Writing", "Code", "Research"];
    // Category numbers stay stable. Deleted slots have empty names and are absent from this order.
    public int[] CategoryOrder { get; set; } = [0, 1, 2, 3];
}

public sealed class Library
{
    public int Version { get; set; } = 1;
    public Preferences Settings { get; set; } = new();
    public List<Prompt> Prompts { get; set; } = [];

    public static Library CreateDefault()
    {
        using var stream = typeof(Library).Assembly.GetManifestResourceStream("QuickPrompt.Assets.SeedLibrary.json")
            ?? throw new InvalidDataException("The bundled seed library is missing.");
        var library = JsonSerializer.Deserialize<Library>(stream)
            ?? throw new InvalidDataException("The bundled seed library is empty.");
        LibraryStore.Validate(library);
        return library;
    }
}

public static class PromptSearch
{
    public static List<Prompt> Find(Library library, int category, string query)
    {
        string[] terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return library.Prompts.Where(p => p.Category == category || (terms.Length > 0 && library.Settings.SearchAllCategories))
            .Where(p => terms.All(t => p.GetName(library.Settings.Language).Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => terms.Length > 0 && p.GetName(library.Settings.Language).StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p.GetName(library.Settings.Language), StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class LibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string FilePath { get; }
    public string BackupDirectory => Path.Combine(Path.GetDirectoryName(FilePath)!, "bak");
    public string? LoadWarning { get; private set; }
    public LibraryStore(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPrompt");
        FilePath = Path.Combine(directory, "prompts.json");
    }

    public Library Load()
    {
        if (!File.Exists(FilePath))
        {
            var fresh = Library.CreateDefault();
            Save(fresh);
            return fresh;
        }
        try { return MigrateSharedLabels(Read(FilePath)); }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            string preserved = FilePath + ".damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            File.Copy(FilePath, preserved);
            if (File.Exists(FilePath + ".bak"))
            {
                // Fail visibly if the backup is also unreadable; never overwrite both copies.
                var recovered = Read(FilePath + ".bak");
                File.Copy(FilePath + ".bak", FilePath, true);
                LoadWarning = "Your prompt file could not be read. The last backup was restored. The damaged file was preserved at:\n" + preserved;
                return MigrateSharedLabels(recovered);
            }
            throw new InvalidDataException("Your prompt file could not be read. It has been preserved at " + preserved, ex);
        }
    }

    private Library MigrateSharedLabels(Library library)
    {
        if (!library.Prompts.Any(p => p.NameDe is not null || p.DescriptionDe is not null)) return library;
        foreach (var prompt in library.Prompts)
        {
            // A legacy German-only prompt may have no English label; preserve its only label.
            if (string.IsNullOrWhiteSpace(prompt.Name))
            {
                prompt.Name = prompt.NameDe ?? "";
                if (string.IsNullOrWhiteSpace(prompt.Description)) prompt.Description = prompt.DescriptionDe ?? "";
            }
            prompt.NameDe = null;
            prompt.DescriptionDe = null;
        }
        // Save archives the untouched disk version before removing its translated labels.
        Save(library);
        return library;
    }

    private static Library Read(string path)
    {
        var library = JsonSerializer.Deserialize<Library>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("Empty library.");
        Validate(library);
        return library;
    }

    public static void Validate(Library library)
    {
        if (library.Version != 1 || library.Settings is null || library.Prompts is null || library.Settings.Categories is not { Length: 4 })
            throw new InvalidDataException("The library must have version 1 and four category slots.");
        var order = library.Settings.CategoryOrder;
        if (order is null || order.Length > 4 || order.Any(id => id < 0 || id > 3) || order.Distinct().Count() != order.Length
            || Enumerable.Range(0, 4).Any(id => order.Contains(id) ? string.IsNullOrWhiteSpace(library.Settings.Categories[id]) : !string.IsNullOrEmpty(library.Settings.Categories[id])))
            throw new InvalidDataException("Category order must list every active category exactly once; deleted category slots must be empty.");
        if (library.Prompts.Any(p => p is null || !order.Contains(p.Category) || !p.HasValidTranslations()) || library.Prompts.Select(p => p.Id).Distinct().Count() != library.Prompts.Count)
            throw new InvalidDataException("The library contains an invalid prompt or a prompt in a deleted category.");
        if (library.Settings.Language is not ("en" or "de")) library.Settings.Language = "en";
        if (library.Settings.LastCategory != LauncherSearch.DocumentsTab && !order.Contains(library.Settings.LastCategory))
            library.Settings.LastCategory = order.FirstOrDefault(LauncherSearch.DocumentsTab);
        if (string.IsNullOrWhiteSpace(library.Settings.DocumentsFolder)) library.Settings.DocumentsFolder = @"C:\Launchy";
    }

    public void ApplySettings(Library library, Preferences settings)
    {
        var previousSettings = library.Settings;
        var previousPrompts = library.Prompts;
        library.Settings = settings;
        library.Prompts = previousPrompts.Where(p => settings.CategoryOrder is not null && settings.CategoryOrder.Contains(p.Category)).ToList();
        try { Save(library); }
        catch { library.Settings = previousSettings; library.Prompts = previousPrompts; throw; }
    }

    public void Save(Library library)
    {
        Validate(library);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        if (File.Exists(FilePath))
        {
            // Read the saved state, never the already-edited in-memory objects.
            var previous = Read(FilePath);
            if (!previous.Settings.Categories.SequenceEqual(library.Settings.Categories)
                || !previous.Settings.CategoryOrder.SequenceEqual(library.Settings.CategoryOrder)
                || !JsonSerializer.SerializeToUtf8Bytes(previous.Prompts).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(library.Prompts)))
                Backup(previous, library);
        }
        string temp = FilePath + ".tmp";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(library, JsonOptions);
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
    }

    private void Backup(Library previous, Library updated)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var removed = previous.Prompts.Where(p => !updated.Prompts.Any(current => current.Id == p.Id)).ToList();
        var removedCategories = previous.Settings.CategoryOrder.Where(id => !updated.Settings.CategoryOrder.Contains(id)).ToArray();
        string reason = removedCategories.Length > 0 ? $"Before deleting {removedCategories.Length} categor{(removedCategories.Length == 1 ? "y" : "ies")} and {removed.Count} prompt{(removed.Count == 1 ? "" : "s")}"
            : removed.Count > 0 ? $"Before deleting {removed.Count} prompt(s)"
            : !previous.Settings.Categories.SequenceEqual(updated.Settings.Categories) || !previous.Settings.CategoryOrder.SequenceEqual(updated.Settings.CategoryOrder)
                ? "Before changing categories"
                : previous.Prompts.Any(p => p.NameDe is not null || p.DescriptionDe is not null) && updated.Prompts.All(p => p.NameDe is null && p.DescriptionDe is null)
                    ? "Before migrating to shared English titles and descriptions" : "Before changing prompts";
        var backup = new
        {
            BackupVersion = 1,
            CreatedAt = timestamp,
            Reason = reason,
            RemovedCategories = removedCategories.Select(id => new { Id = id, Name = previous.Settings.Categories[id], Prompts = previous.Prompts.Where(p => p.Category == id).ToArray() }).ToArray(),
            RemovedPrompts = removed.Select(p => new { p.Id, CategoryId = p.Category, CategoryName = previous.Settings.Categories[p.Category], p.Name, p.NameDe }).ToArray(),
            Library = previous
        };
        string stem = Path.Combine(BackupDirectory, $"{timestamp:yyyy-MM-dd_HH-mm-ss.fffffff'Z'}_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(BackupDirectory);
            WriteBackupFile(stem + ".json", JsonSerializer.SerializeToUtf8Bytes(backup, JsonOptions));
            var text = new StringBuilder();
            text.AppendLine("QuickPrompt library backup").AppendLine($"Created at (UTC): {timestamp:O}").AppendLine($"Reason: {reason}");
            foreach (int category in previous.Settings.CategoryOrder)
            {
                text.AppendLine().AppendLine($"Category: {previous.Settings.Categories[category]} (ID: {category})");
                if (removedCategories.Contains(category)) text.AppendLine("Category deleted by this change (all prompts below are included in this backup)");
                foreach (var prompt in previous.Prompts.Where(p => p.Category == category))
                {
                    text.AppendLine().AppendLine($"Prompt ID: {prompt.Id}");
                    text.AppendLine($"Non-bilingual: {prompt.NonBilingual}");
                    if (removed.Any(p => p.Id == prompt.Id)) text.AppendLine("Deleted by this change");
                    foreach (string language in new[] { "en", "de" })
                    {
                        if (!prompt.HasLanguage(language)) continue;
                        text.AppendLine(language == "de" ? "--- Deutsch (DE) ---" : "--- English (EN) ---");
                        string name = language == "de" && !string.IsNullOrWhiteSpace(prompt.NameDe) ? prompt.NameDe : prompt.GetName(language);
                        string description = language == "de" && prompt.DescriptionDe is not null ? prompt.DescriptionDe : prompt.GetDescription(language);
                        text.AppendLine($"Name: {name}").AppendLine($"Description: {description}");
                        text.AppendLine("Prompt:").Append(prompt.GetContent(language)).AppendLine().AppendLine("--- End of prompt ---");
                    }
                }
            }
            WriteBackupFile(stem + ".txt", Encoding.UTF8.GetBytes(text.ToString()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException("The change was not saved because its backup could not be completed in " + BackupDirectory + ". " + ex.Message, ex);
        }
    }

    private static void WriteBackupFile(string path, byte[] bytes)
    {
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        File.Move(temporary, path);
    }
}
