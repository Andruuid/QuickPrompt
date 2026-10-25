using System.Text.Json;
using QuickPrompt;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); checks++;
}
var library = Library.CreateDefault();
string template = "Read {FileName}, explain {Topic}, then revisit {FileName}.";
Check(PromptTemplate.GetNames(template).SequenceEqual(new[] { "FileName", "Topic" }), "Placeholders follow first appearance and repeated names are asked once");
string literalBraces = "{fileName} {File Name} {File_Name} {File1} {} {{FileName}} {\"Topic\": 1} {\nTopic\n} { Topic }";
Check(PromptTemplate.GetNames(literalBraces).Count == 0, "Only complete single capitalized letter-only names become placeholders");
Check(PromptTemplate.GetNames("{A}{Topic}{FileName}").SequenceEqual(new[] { "A", "Topic", "FileName" }), "Single-word and adjacent PascalCase placeholders are recognized");
var values = new Dictionary<string, string> { ["FileName"] = "C:\\notes\\a.txt\r\n  second line\t✨\r\n", ["Topic"] = "Literal {FileName} and $1 ${Topic}" };
string filled = PromptTemplate.Fill(template, values);
Check(filled == "Read " + values["FileName"] + ", explain " + values["Topic"] + ", then revisit " + values["FileName"] + ".", "Values preserve exact multiline whitespace, Unicode and replacement-like text without recursive expansion");
Check(PromptTemplate.Fill(literalBraces, values) == literalBraces, "Code and non-placeholder brace content remain unchanged");
Check(PromptTemplate.Fill("Text with no placeholders.\n", values) == "Text with no placeholders.\n", "Plain prompts copy unchanged");
Check(PromptTemplate.Fill("{Missing} {Topic}", values) == "{Missing} " + values["Topic"], "Unprovided names remain literal");
Check(library.Settings.Categories.Length == 4, "Four default categories");
Check(PromptSearch.Find(library, 0, "").Count == 3, "Empty search is scoped to category");
Check(PromptSearch.Find(library, 0, "SUMMARIZE").Single().Name == "Summarize", "Search is case insensitive");
Check(PromptSearch.Find(library, 0, "think clear").Single().Name == "Clear thinking", "Every query term matches the title in any order");
Check(PromptSearch.Find(library, 0, "constructively").Count == 0, "Body-only matches do not appear in title search");
Check(PromptSearch.Find(library, 0, "rough").Count == 0, "Description-only matches do not appear in title search");
Check(PromptSearch.Find(library, 0, "email").Single().Name == "Draft an email", "Default search finds titles across categories");
Check(PromptSearch.Find(library, 0, "i").Count > 1, "A single typed letter immediately returns matching titles");
Check(PromptSearch.Find(library, 0, "email missing").Count == 0, "Unmatched additional text removes results");
library.Settings.SearchAllCategories = false;
Check(PromptSearch.Find(library, 0, "review code").Count == 0, "Optional category-only search is respected");
library.Settings.SearchAllCategories = true;
Check(PromptSearch.Find(library, 0, "  ").Count == 3, "Whitespace query stays in current category");
var custom = new Prompt { Name = "Unicode ✨", Description = "日本語 · Grüße", Content = "First line\n\n  indented\ttext\nLast line\n", Category = 3 };
library.Prompts.Add(custom);
string directory = Path.Combine(Path.GetTempPath(), "QuickPrompt.Tests." + Guid.NewGuid().ToString("N"));
try
{
    var store = new LibraryStore(directory);
    store.Save(library);
    var reloaded = store.Load();
    Check(reloaded.Prompts.Single(p => p.Id == custom.Id).Content == custom.Content, "Exact multiline content survives save/reload");
    Check(reloaded.Prompts.Single(p => p.Id == custom.Id).Description == custom.Description, "Unicode metadata survives save/reload");
    reloaded.Settings.Categories[3] = "My project";
    reloaded.Settings.LastCategory = 3;
    store.Save(reloaded);
    Check(store.Load().Settings.Categories[3] == "My project", "Renamed categories persist");
    Check(store.Load().Settings.LastCategory == 3, "Last category persists");
    Check(File.Exists(store.FilePath + ".bak"), "Atomic save retains previous library backup");
    File.WriteAllText(store.FilePath, "{ broken json");
    var recovered = store.Load();
    Check(recovered.Prompts.Any(p => p.Id == custom.Id), "Damaged primary recovers from backup");
    Check(store.LoadWarning is not null && Directory.GetFiles(directory, "*.damaged-*").Length == 1, "Recovery warns and preserves damaged file");
    var empty = new Library();
    store.Save(empty);
    Check(store.Load().Prompts.Count == 0, "An intentionally empty library is not reseeded");
    var bad = new Library { Settings = new Preferences { Categories = ["Only one"] } };
    bool rejected = false;
    try { store.Save(bad); } catch (InvalidDataException) { rejected = true; }
    Check(rejected && store.Load().Prompts.Count == 0, "Invalid state cannot overwrite saved data");
    File.WriteAllText(store.FilePath, "{}");
    File.WriteAllText(store.FilePath + ".bak", "invalid backup");
    // {} is a valid empty library with defaults; test unsupported format explicitly.
    File.WriteAllText(store.FilePath, JsonSerializer.Serialize(new { Version = 99 }));
    bool failedSafely = false;
    try { store.Load(); } catch (JsonException) { failedSafely = true; }
    Check(failedSafely && File.ReadAllText(store.FilePath).Contains("99"), "Unrecoverable library fails without overwriting files");
}
finally { Directory.Delete(directory, true); }
string languageDirectory = Path.Combine(Path.GetTempPath(), "QuickPrompt.Languages.Tests." + Guid.NewGuid().ToString("N"));
try
{
    var legacy = JsonSerializer.Deserialize<Library>("""{"Version":1,"Prompts":[{"Name":"Legacy","Content":"Original text\n"}]}""")!;
    LibraryStore.Validate(legacy);
    Check(legacy.Settings.Language == "en" && legacy.Prompts[0].ContentDe == "", "Legacy libraries default to English without inventing a German translation");
    Check(legacy.Prompts[0].AvailableLanguage("de") == "en", "English-only prompts fall back when German is selected");
    var germanOnly = new Prompt { NameDe = "Nur Deutsch", DescriptionDe = "Beschreibung", ContentDe = "  Grüße\n{Datei}\n" };
    legacy.Prompts.Add(germanOnly);
    Check(germanOnly.HasValidTranslations() && germanOnly.AvailableLanguage("en") == "de", "German-only prompts are valid and fall back for English");
    Check(!germanOnly.HasLanguage("en") && germanOnly.GetName("en") == "Nur Deutsch", "Fallback title remains visible without claiming an English version exists");
    legacy.Settings.Language = "de";
    Check(PromptSearch.Find(legacy, 0, "deutsch").Single() == germanOnly, "Legacy German-only prompts remain searchable by their only title");
    Check(LauncherSearch.Find(legacy, [], 0, "Legacy").Single().Name == "Legacy", "Untranslated prompts remain searchable in selected language");
    var store = new LibraryStore(languageDirectory);
    store.Save(legacy);
    var saved = store.Load();
    Check(saved.Settings.Language == "de" && saved.Prompts[1].ContentDe == germanOnly.ContentDe && saved.Prompts[1].Content == "", "Selected language and exact German-only content survive restart");
    var bilingual = Library.CreateDefault();
    Check(bilingual.Prompts.All(p => p.HasLanguage("en") && p.HasLanguage("de") && p.HasValidTranslations()), "All nine new example prompts include complete English and German versions");
    bilingual.Settings.Language = "de";
    var result = LauncherSearch.Find(bilingual, [], 0, "summar").Single();
    Check(result.Name == "Summarize" && result.Description == bilingual.Prompts[1].Description, "German mode uses the same English title and description");
    bilingual.Prompts[0].ContentDe = "Lies {Datei}.\n\n  Grüße\t\n";
    store.Save(bilingual);
    Check(store.Load().Prompts[0].ContentDe == bilingual.Prompts[0].ContentDe && store.Load().Prompts[0].Content == bilingual.Prompts[0].Content, "Both language versions retain independent exact text");
    Check(PromptTemplate.Fill(bilingual.Prompts[0].GetContent("de"), new Dictionary<string, string> { ["Datei"] = "Notizen" }) == "Lies Notizen.\n\n  Grüße\t\n", "German placeholders substitute in the German template");
    var invalid = new Library { Prompts = [new Prompt { ContentDe = "Body without a shared title" }] };
    bool rejected = false;
    try { store.Save(invalid); } catch (InvalidDataException) { rejected = true; }
    Check(rejected && store.Load().Prompts.Count == 9, "A missing shared title cannot overwrite the saved library");
    Check(!new Prompt().HasValidTranslations() && !new Prompt { NameDe = "Whitespace", ContentDe = " \n" }.HasValidTranslations(), "Empty and whitespace-only prompts are rejected in both languages");
    bilingual.Settings.Language = "unsupported";
    LibraryStore.Validate(bilingual);
    Check(bilingual.Settings.Language == "en", "Unknown language preference safely defaults to English");
}
finally { if (Directory.Exists(languageDirectory)) Directory.Delete(languageDirectory, true); }
string docsDirectory = Path.Combine(Path.GetTempPath(), "QuickPrompt.Documents.Tests." + Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(Path.Combine(docsDirectory, "Nested"));
    foreach (string name in new[] { "Report.PDF", "Photo.jpg", "Photo.jpeg", "Diagram.png", "Guide.html", "Guide.htm", "Notes.txt", "Project.SLN", "Project.slnx", "Ignore.exe", "Fake.pdf.exe", "Link.LNK" })
        File.WriteAllText(Path.Combine(docsDirectory, name), "Test fixture");
    File.WriteAllText(Path.Combine(docsDirectory, "Nested", "Report.PDF"), "Nested fixture");
    var shallow = DocumentCatalog.Scan(docsDirectory, false);
    Check(shallow.Items.Count == 10 && shallow.Warning is null, "Only supported document extensions are indexed, ignoring case");
    Check(shallow.Items.Any(p => p.Name == "Notes.txt") && shallow.Items.Any(p => p.Name == "Project.SLN"), "Text files and Visual Studio solutions are indexed");
    Check(shallow.Items.Any(p => p.Name == "Project.slnx"), "XML solution files are indexed");
    var link = shallow.Items.Single(p => p.Name == "Link");
    Check(link.FilePath == Path.Combine(docsDirectory, "Link.LNK") && link.Description.StartsWith("Shortcut · ") && link.ActionLabel == "Open", "Shortcuts display Explorer-style names and retain their original launch path");
    var recursive = DocumentCatalog.Scan(docsDirectory, true);
    Check(recursive.Items.Count == 11, "Subfolders are included when enabled");
    Check(recursive.Items.Count(p => p.Name == "Report.PDF") == 2 && recursive.Items.Select(p => p.Identity).Distinct().Count() == 11, "Duplicate filenames retain distinct paths");
    Check(recursive.Items.Any(p => p.Description.Contains("Nested")), "Subfolder path distinguishes duplicate filenames");
    var defaults = Library.CreateDefault();
    Check(defaults.Settings.DocumentsFolder == @"C:\Launchy", "Default document directory is C:\\Launchy");
    Check(LauncherSearch.Find(defaults, recursive.Items, 0, "report pdf").Count == 2, "Filename search finds documents from a prompt tab");
    Check(LauncherSearch.Find(defaults, recursive.Items, 4, "").Count == 11, "Documents tab lists all indexed files without a query");
    Check(LauncherSearch.Find(defaults, recursive.Items, 0, "link").Single().FilePath == link.FilePath, "Shortcuts can be found from the main search");
    Check(LauncherSearch.Find(defaults, recursive.Items, 0, "").All(p => p.Prompt is not null), "Empty prompt tabs still show only their prompts");
    Check(LauncherSearch.Find(defaults, recursive.Items, 4, "summarize").Count == 0, "Documents tab excludes prompts");
    var mixed = LauncherSearch.Find(defaults, recursive.Items, 0, "e");
    Check(mixed.Any(p => p.ActionLabel == "Copy") && mixed.Any(p => p.ActionLabel == "Open"), "Mixed results expose the correct action");
    File.Delete(Path.Combine(docsDirectory, "Guide.htm"));
    Check(DocumentCatalog.Scan(docsDirectory, true).Items.Count == 10, "Rescan removes deleted files");
    Check(DocumentCatalog.Scan(Path.Combine(docsDirectory, "missing"), true).Warning is not null, "Missing document folder produces a useful status");
    Check(DocumentCatalog.Scan("relative-folder", true).Items.Count == 0, "Relative catalog paths are rejected");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    bool cancelled = false;
    try { DocumentCatalog.Scan(docsDirectory, true, cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled, "An obsolete catalog scan can be cancelled");
    var oldSettings = JsonSerializer.Deserialize<Preferences>("{\"Hotkey\":\"Alt+Space\"}")!;
    Check(oldSettings.DocumentsFolder == @"C:\Launchy" && oldSettings.IncludeDocumentSubfolders, "Existing settings receive document defaults");
    var store = new LibraryStore(Path.Combine(docsDirectory, "state"));
    defaults.Settings.DocumentsFolder = docsDirectory;
    defaults.Settings.IncludeDocumentSubfolders = false;
    defaults.Settings.LastCategory = 4;
    store.Save(defaults);
    var saved = store.Load();
    Check(saved.Settings.DocumentsFolder == docsDirectory && !saved.Settings.IncludeDocumentSubfolders && saved.Settings.LastCategory == 4, "Folder, scan preference and Documents tab survive restart");
}
finally { Directory.Delete(docsDirectory, true); }
string orderDirectory = Path.Combine(Path.GetTempPath(), "QuickPrompt.Order.Tests." + Guid.NewGuid().ToString("N"));
try
{
    var defaults = JsonSerializer.Deserialize<Preferences>("{}")!;
    Check(defaults.CategoryOrder.SequenceEqual(new[] { 0, 1, 2, 3 }), "Existing settings keep the original category order");
    var ordered = Library.CreateDefault();
    var originalMembership = ordered.Prompts.ToDictionary(p => p.Id, p => p.Category);
    ordered.Settings.CategoryOrder = [2, 0, 3, 1];
    ordered.Settings.LastCategory = 3;
    ordered.Settings.Language = "de";
    ordered.Settings.Categories[2] = "Development";
    var store = new LibraryStore(orderDirectory);
    store.Save(ordered);
    var saved = store.Load();
    Check(saved.Settings.CategoryOrder.SequenceEqual(new[] { 2, 0, 3, 1 }) && saved.Settings.Categories[2] == "Development", "Display order and renamed category persist independently of category identity");
    Check(saved.Prompts.All(p => p.Category == originalMembership[p.Id]) && saved.Settings.LastCategory == 3, "Reordering retains prompt membership and remembered category identity");
    Check(saved.Settings.Language == "de" && saved.Prompts.All(p => p.HasLanguage("de")), "Category ordering preserves bilingual prompt data and language preference");
    foreach (int[]? invalid in new int[]?[] { [0, 0, 2, 3], [0, 1, 2], [0, 1, 2, 4], null })
    {
        ordered.Settings.CategoryOrder = invalid!;
        bool rejected = false;
        try { store.Save(ordered); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && store.Load().Settings.CategoryOrder.SequenceEqual(new[] { 2, 0, 3, 1 }), "Invalid category permutation cannot overwrite saved order");
    }
}
finally { if (Directory.Exists(orderDirectory)) Directory.Delete(orderDirectory, true); }
string backupDirectory = Path.Combine(Path.GetTempPath(), "QuickPrompt.Backup.Tests." + Guid.NewGuid().ToString("N"));
try
{
    var store = new LibraryStore(backupDirectory);
    var source = new Library
    {
        Prompts =
        [
            new Prompt { Category = 2, Name = "Deleted prompt", Content = "Original\n\n  English\t\n", NameDe = "Gelöschter Prompt", DescriptionDe = "Grüße", ContentDe = "Ursprünglich\r\n  Deutsch {Datei}\r\n" },
            new Prompt { Category = 1, Name = "Keep", Content = "Keep this" },
            new Prompt { Category = 2, Name = "Keep too", Content = "Keep this too" }
        ]
    };
    source.Settings.Categories[2] = "Development";
    store.Save(source);
    Check(!Directory.Exists(store.BackupDirectory), "Initial library creation does not create a misleading empty backup");
    var deleted = source.Prompts[0];
    source.Prompts.RemoveAt(0);
    store.Save(source);
    string firstJson = Directory.GetFiles(store.BackupDirectory, "*.json").Single();
    string firstText = Directory.GetFiles(store.BackupDirectory, "*.txt").Single();
    string firstContents = File.ReadAllText(firstJson);
    using (var backup = JsonDocument.Parse(firstContents))
    {
        var root = backup.RootElement;
        Check(root.GetProperty("CreatedAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow && root.GetProperty("Reason").GetString()!.Contains("deleting"), "Deletion backup includes an ISO timestamp and reason");
        Check(root.GetProperty("RemovedPrompts")[0].GetProperty("Id").GetGuid() == deleted.Id && root.GetProperty("RemovedPrompts")[0].GetProperty("CategoryName").GetString() == "Development", "Deleted prompt metadata includes original ID and category name");
        var oldPrompt = root.GetProperty("Library").GetProperty("Prompts")[0];
        Check(oldPrompt.GetProperty("Content").GetString() == deleted.Content && oldPrompt.GetProperty("ContentDe").GetString() == deleted.ContentDe, "JSON backup retains exact English and German content, whitespace and placeholders");
        Check(root.GetProperty("Library").GetProperty("Prompts").GetArrayLength() == 3 && store.Load().Prompts.Count == 2, "Backup holds the full previous library while deletion is saved");
    }
    string text = File.ReadAllText(firstText);
    Check(text.Contains("Development (ID: 2)") && text.Contains("Gelöschter Prompt") && text.Contains(deleted.ContentDe) && text.Contains("Deleted by this change"), "Readable text backup includes category, language, Unicode and exact deleted prompt text");
    source.Settings.Language = "de";
    source.Settings.LastCategory = 1;
    store.Save(source);
    store.Save(source);
    Check(Directory.GetFiles(store.BackupDirectory, "*.json").Length == 1, "Preference-only and unchanged saves do not flood backup history");
    source.Settings.Categories[2] = "Code";
    store.Save(source);
    var categoryBackup = Directory.GetFiles(store.BackupDirectory, "*.json").Single(path => path != firstJson);
    using (var backup = JsonDocument.Parse(File.ReadAllText(categoryBackup)))
        Check(backup.RootElement.GetProperty("Library").GetProperty("Settings").GetProperty("Categories")[2].GetString() == "Development", "Category changes preserve the old labels and category prompts");
    source.Prompts[0].Content = "Changed text";
    store.Save(source);
    source.Prompts.Clear();
    store.Save(source);
    Check(Directory.GetFiles(store.BackupDirectory, "*.json").Length == 4 && Directory.GetFiles(store.BackupDirectory, "*.txt").Length == 4, "Edits and bulk deletion each create a permanent JSON/text pair");
    Check(File.ReadAllText(firstJson) == firstContents, "Later saves never rotate or overwrite older deletion backups");
    Check(Directory.GetFiles(store.BackupDirectory, "*.tmp").Length == 0, "Successful backups leave only complete final files");

    var blockedStore = new LibraryStore(Path.Combine(backupDirectory, "blocked"));
    var blocked = Library.CreateDefault();
    blockedStore.Save(blocked);
    string savedBefore = File.ReadAllText(blockedStore.FilePath);
    File.WriteAllText(blockedStore.BackupDirectory, "A file blocks creation of the backup folder");
    blocked.Prompts.RemoveAt(0);
    bool rejected = false;
    try { blockedStore.Save(blocked); } catch (IOException ex) { rejected = ex.Message.Contains("backup"); }
    Check(rejected && File.ReadAllText(blockedStore.FilePath) == savedBefore && blockedStore.Load().Prompts.Count == 9, "A failed backup blocks deletion and leaves the saved library untouched");

    var invalidStore = new LibraryStore(Path.Combine(backupDirectory, "invalid"));
    var invalid = Library.CreateDefault();
    invalidStore.Save(invalid);
    invalid.Prompts[0].Content = "";
    invalid.Prompts[0].ContentDe = "";
    bool invalidRejected = false;
    try { invalidStore.Save(invalid); } catch (InvalidDataException) { invalidRejected = true; }
    Check(invalidRejected && !Directory.Exists(invalidStore.BackupDirectory), "Rejected invalid edits do not create backups or replace data");
}
finally { if (Directory.Exists(backupDirectory)) Directory.Delete(backupDirectory, true); }
string sharedDirectory = Path.Combine(Path.GetTempPath(), "QuickPrompt.SharedLabels.Tests." + Guid.NewGuid().ToString("N"));
try
{
    var single = new Prompt { Name = "Use anywhere", Description = "Shared description", Content = "Exact English\n" };
    Check(!single.NonBilingual && single.RequiresLanguageWarning("de"), "Non-bilingual is off by default and ordinary missing translations still warn");
    single.NonBilingual = true;
    Check(single.HasValidTranslations() && !single.RequiresLanguageWarning("de") && single.GetContent(single.AvailableLanguage("de")) == "Exact English\n", "English non-bilingual prompts are valid and silently use English in German mode");
    single.Content = ""; single.ContentDe = "Nur Deutsch\n";
    Check(single.HasValidTranslations() && !single.RequiresLanguageWarning("en") && single.AvailableLanguage("en") == "de", "German non-bilingual prompts silently use German in English mode");
    single.Content = "Two versions";
    Check(!single.HasValidTranslations(), "Non-bilingual prompts cannot be saved with two versions");
    single.NonBilingual = false;
    Check(single.HasValidTranslations() && single.GetName("de") == single.GetName("en") && single.GetDescription("de") == single.GetDescription("en"), "Bilingual bodies use one shared title and description");
    var store = new LibraryStore(sharedDirectory);
    var legacy = new Library
    {
        Prompts =
        [
            new Prompt { Name = "English title", Description = "English description", NameDe = "Alter deutscher Titel", DescriptionDe = "Alte Beschreibung", Content = "English\n\n", ContentDe = "Deutsch\r\n  {Datei}\r\n" },
            new Prompt { NameDe = "Einziger Titel", DescriptionDe = "Einzige Beschreibung", ContentDe = "Nur deutscher Inhalt" },
            new Prompt { Name = "English with no description", DescriptionDe = "Do not use as English", Content = "Body" }
        ]
    };
    Directory.CreateDirectory(sharedDirectory);
    File.WriteAllText(store.FilePath, JsonSerializer.Serialize(legacy));
    var loaded = store.Load();
    Check(loaded.Prompts[0].Name == "English title" && loaded.Prompts[0].Description == "English description" && loaded.Prompts.All(p => p.NameDe is null && p.DescriptionDe is null), "Migration retains English labels and removes separate German labels");
    Check(loaded.Prompts[0].Id == legacy.Prompts[0].Id && loaded.Prompts[0].Content == legacy.Prompts[0].Content && loaded.Prompts[0].ContentDe == legacy.Prompts[0].ContentDe, "Label migration preserves prompt identity and both exact bodies");
    Check(loaded.Prompts[1].Name == "Einziger Titel" && loaded.Prompts[1].Description == "Einzige Beschreibung", "Legacy German-only prompts retain their only existing labels as shared labels");
    Check(loaded.Prompts[2].Description == "", "An existing English title with no description does not inherit a German description");
    Check(!File.ReadAllText(store.FilePath).Contains("NameDe") && !File.ReadAllText(store.FilePath).Contains("DescriptionDe"), "Removed German label fields are omitted from the live JSON");
    using (var backup = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(store.BackupDirectory, "*.json").Single())))
        Check(backup.RootElement.GetProperty("Library").GetProperty("Prompts")[0].GetProperty("NameDe").GetString() == "Alter deutscher Titel", "Migration archives deleted German titles before saving shared labels");
    Check(File.ReadAllText(Directory.GetFiles(store.BackupDirectory, "*.txt").Single()).Contains("Alte Beschreibung"), "Migration's readable backup retains old German descriptions");
    store.Load();
    Check(Directory.GetFiles(store.BackupDirectory, "*.json").Length == 1, "Shared-label migration runs only once");
    loaded.Prompts[1].NonBilingual = true;
    store.Save(loaded);
    Check(store.Load().Prompts[1].NonBilingual, "Non-bilingual flag survives saving and restarting");
    loaded.Settings.Language = "de";
    Check(LauncherSearch.Find(loaded, [], 0, "English title").Single().Description == "English description", "Title search and descriptions remain English in German mode");
    loaded.Prompts[0].NonBilingual = true;
    loaded.Prompts[0].Content = "";
    store.Save(loaded);
    Check(store.Load().Prompts[0].ContentDe == legacy.Prompts[0].ContentDe && store.Load().Prompts[0].NonBilingual, "Conversion to non-bilingual saves only the chosen German body");
    Check(Directory.GetFiles(store.BackupDirectory, "*.json").Select(File.ReadAllText).Any(json =>
    {
        using var archived = JsonDocument.Parse(json);
        var first = archived.RootElement.GetProperty("Library").GetProperty("Prompts")[0];
        return !first.GetProperty("NonBilingual").GetBoolean() && first.GetProperty("Content").GetString() == legacy.Prompts[0].Content && first.GetProperty("ContentDe").GetString() == legacy.Prompts[0].ContentDe;
    }), "Converting to one language preserves both previous bodies in permanent history");

    var blockedStore = new LibraryStore(Path.Combine(sharedDirectory, "blocked"));
    Directory.CreateDirectory(Path.GetDirectoryName(blockedStore.FilePath)!);
    string raw = JsonSerializer.Serialize(legacy);
    File.WriteAllText(blockedStore.FilePath, raw);
    File.WriteAllText(blockedStore.BackupDirectory, "Folder blocked");
    bool rejected = false;
    try { blockedStore.Load(); } catch (IOException) { rejected = true; }
    Check(rejected && File.ReadAllText(blockedStore.FilePath) == raw, "Migration cannot remove German labels if its backup fails");
}
finally { if (Directory.Exists(sharedDirectory)) Directory.Delete(sharedDirectory, true); }
Console.WriteLine($"\n{checks} checks passed.");
