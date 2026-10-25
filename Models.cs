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
    // Category numbers are stable prompt identities; this array only changes their display order.
    public int[] CategoryOrder { get; set; } = [0, 1, 2, 3];
}

public sealed class Library
{
    public int Version { get; set; } = 1;
    public Preferences Settings { get; set; } = new();
    public List<Prompt> Prompts { get; set; } = [];

    public static Library CreateDefault()
    {
        var library = new Library
        {
        Prompts =
        [
            new() { Name = "Clear thinking", Description = "Turn a rough idea into a practical plan", Content = "Help me think through the following idea. Identify the goal, key assumptions, tradeoffs, and the smallest useful next step. Ask clarifying questions if necessary.\n\nMy idea:\n" },
            new() { Name = "Summarize", Description = "The main points, decisions, and next steps", Content = "Summarize the following text in plain language. Include the key points, decisions, and actionable next steps. Preserve important details and flag uncertainty.\n\nText:\n" },
            new() { Name = "Second opinion", Description = "Find assumptions and overlooked alternatives", Content = "Review my reasoning critically and constructively. Identify weak assumptions, missing evidence, and alternative explanations. Then suggest how I could improve it.\n\nMy reasoning:\n" },
            new() { Category = 1, Name = "Make it clearer", Description = "Polish a draft without losing your voice", Content = "Rewrite the following draft for clarity and concision. Keep my meaning and tone. Use simple language, remove repetition, and do not invent facts.\n\nDraft:\n" },
            new() { Category = 1, Name = "Draft an email", Description = "A short, friendly email with a clear ask", Content = "Draft a concise, friendly email using the context below. Include a useful subject line and make the next action clear.\n\nContext:\n" },
            new() { Category = 2, Name = "Review code", Description = "Look for bugs, edge cases, and maintainability", Content = "Review this code. Prioritize concrete bugs, security issues, and missing edge cases. Explain each finding and suggest a minimal fix. Avoid speculative or purely stylistic changes.\n\nCode:\n" },
            new() { Category = 2, Name = "Debug a problem", Description = "Work from symptoms to a testable explanation", Content = "Help me debug this problem. Separate observations from assumptions, propose likely causes, and suggest the smallest test to distinguish them.\n\nExpected behavior:\n\nActual behavior:\n\nRelevant code or logs:\n" },
            new() { Category = 3, Name = "Research brief", Description = "Evidence, open questions, and reliable sources", Content = "Research the topic below. Explain the main findings, compare relevant perspectives, and separate established evidence from uncertainty. Cite reliable sources and do not invent references.\n\nTopic:\n" },
            new() { Category = 3, Name = "Compare options", Description = "A balanced comparison against your criteria", Content = "Compare these options against my criteria. Explain the tradeoffs, highlight missing information, and give a reasoned recommendation.\n\nOptions:\n\nCriteria:\n" }
        ]
        };
        string[] german =
        [
            "Hilf mir, die folgende Idee zu durchdenken. Benenne das Ziel, die wichtigsten Annahmen, Vor- und Nachteile und den kleinsten sinnvollen nächsten Schritt. Stelle bei Bedarf Rückfragen.\n\nMeine Idee:\n",
            "Fasse den folgenden Text in einfacher Sprache zusammen. Nenne die wichtigsten Punkte, Entscheidungen und konkreten nächsten Schritte. Bewahre wichtige Details und weise auf Unsicherheiten hin.\n\nText:\n",
            "Prüfe meine Überlegungen kritisch und konstruktiv. Zeige schwache Annahmen, fehlende Belege und alternative Erklärungen auf. Schlage anschließend Verbesserungen vor.\n\nMeine Überlegungen:\n",
            "Überarbeite den folgenden Entwurf klar und prägnant. Bewahre meine Aussage und meinen Ton. Verwende einfache Sprache, entferne Wiederholungen und erfinde keine Fakten.\n\nEntwurf:\n",
            "Entwirf anhand des folgenden Kontexts eine kurze, freundliche E-Mail. Füge einen passenden Betreff hinzu und mache den nächsten Schritt deutlich.\n\nKontext:\n",
            "Prüfe diesen Code. Konzentriere dich auf konkrete Fehler, Sicherheitsprobleme und fehlende Grenzfälle. Erkläre jeden Befund und schlage eine minimale Korrektur vor. Vermeide spekulative oder rein stilistische Änderungen.\n\nCode:\n",
            "Hilf mir, dieses Problem zu debuggen. Trenne Beobachtungen von Annahmen, schlage wahrscheinliche Ursachen vor und nenne den kleinsten Test, um sie zu unterscheiden.\n\nErwartetes Verhalten:\n\nTatsächliches Verhalten:\n\nRelevanter Code oder Logs:\n",
            "Recherchiere das folgende Thema. Erkläre die wichtigsten Erkenntnisse, vergleiche relevante Perspektiven und trenne gesicherte Belege von Unsicherheit. Zitiere zuverlässige Quellen und erfinde keine Referenzen.\n\nThema:\n",
            "Vergleiche diese Optionen anhand meiner Kriterien. Erkläre Vor- und Nachteile, zeige fehlende Informationen auf und gib eine begründete Empfehlung.\n\nOptionen:\n\nKriterien:\n"
        ];
        for (int i = 0; i < german.Length; i++)
        {
            library.Prompts[i].ContentDe = german[i];
        }
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
        if (library.Version != 1 || library.Settings is null || library.Prompts is null || library.Settings.Categories is not { Length: 4 } || library.Settings.Categories.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("The library must have version 1 and four named categories.");
        if (library.Prompts.Any(p => p is null || p.Category < 0 || p.Category > 3 || !p.HasValidTranslations()) || library.Prompts.Select(p => p.Id).Distinct().Count() != library.Prompts.Count)
            throw new InvalidDataException("The library contains an invalid prompt.");
        if (library.Settings.CategoryOrder is not { Length: 4 } || !library.Settings.CategoryOrder.Order().SequenceEqual(new[] { 0, 1, 2, 3 }))
            throw new InvalidDataException("Category order must contain each of the four categories exactly once.");
        if (library.Settings.Language is not ("en" or "de")) library.Settings.Language = "en";
        library.Settings.LastCategory = Math.Clamp(library.Settings.LastCategory, 0, 4);
        if (string.IsNullOrWhiteSpace(library.Settings.DocumentsFolder)) library.Settings.DocumentsFolder = @"C:\Launchy";
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
        string reason = removed.Count > 0 ? $"Before deleting {removed.Count} prompt(s)"
            : !previous.Settings.Categories.SequenceEqual(updated.Settings.Categories) || !previous.Settings.CategoryOrder.SequenceEqual(updated.Settings.CategoryOrder)
                ? "Before changing categories"
                : previous.Prompts.Any(p => p.NameDe is not null || p.DescriptionDe is not null) && updated.Prompts.All(p => p.NameDe is null && p.DescriptionDe is null)
                    ? "Before migrating to shared English titles and descriptions" : "Before changing prompts";
        var backup = new
        {
            BackupVersion = 1,
            CreatedAt = timestamp,
            Reason = reason,
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
