using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace QuickPrompt;

public sealed class LauncherItem
{
    public Prompt? Prompt { get; init; }
    public string? FilePath { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Identity => FilePath ?? Prompt!.Id.ToString();
    public string ActionLabel => FilePath is null ? "Copy" : "Open";
}

public sealed record DocumentScan(List<LauncherItem> Items, string? Warning = null);

public static class DocumentCatalog
{
    public static bool IsSupported(string path) => Path.GetExtension(path).ToLowerInvariant() is ".pdf" or ".jpg" or ".jpeg" or ".png" or ".html" or ".htm" or ".txt" or ".sln" or ".slnx" or ".lnk";

    public static DocumentScan Scan(string folder, bool recursive, CancellationToken cancellationToken = default)
    {
        var items = new List<LauncherItem>();
        try
        {
            if (!Path.IsPathFullyQualified(folder)) return new(items, "Choose an absolute folder path in Settings → Documents.");
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder)) return new(items, "Document folder unavailable. Check Settings → Documents.");
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recursive, IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Hidden
            };
            foreach (string path in Directory.EnumerateFiles(folder, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsSupported(path)) continue;
                bool shortcut = Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase);
                items.Add(new LauncherItem
                {
                    // Keep the .lnk path for ShellExecute so Windows handles its
                    // target, arguments and working directory exactly as Explorer does.
                    FilePath = path, Name = shortcut ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path),
                    Description = (shortcut ? "Shortcut" : Path.GetExtension(path).TrimStart('.').ToUpperInvariant()) + " · " + Path.GetRelativePath(folder, path)
                });
            }
            return new(items);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new(items, "Some documents could not be read. Check the folder and press F5.");
        }
    }
}

public static class LauncherSearch
{
    public const int DocumentsTab = 4;
    public static List<LauncherItem> Find(Library library, IEnumerable<LauncherItem> documents, int category, string query)
    {
        var items = new List<LauncherItem>();
        if (category != DocumentsTab)
            items.AddRange(PromptSearch.Find(library, category, query).Select(p => new LauncherItem { Prompt = p, Name = p.GetName(library.Settings.Language), Description = p.GetDescription(library.Settings.Language) }));
        string[] terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (category == DocumentsTab || terms.Length > 0)
            items.AddRange(documents.Where(p => terms.All(t => p.Name.Contains(t, StringComparison.OrdinalIgnoreCase))));
        return items.OrderByDescending(p => terms.Length > 0 && p.Name.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Identity, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
