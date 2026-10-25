# QuickPrompt

A minimal Windows prompt launcher built with .NET 10 and WPF. Keep up to four categories of LLM prompts one shortcut away. Everything is stored locally; the app makes no network requests.

## Run

**Recommended:** run `artifacts/installer/QuickPrompt-0.4.0-win-x64.msi`. The installer supports Windows 10 22H2 and Windows 11 on x64 PCs. It includes .NET and installs for the current Windows account without administrator rights or an internet connection. Installation goes to `%LOCALAPPDATA%\Programs\QuickPrompt`. It adds a Start-menu entry, an entry in Installed apps, and startup registration with `--hidden`. The final setup page can start QuickPrompt in the tray immediately.

For a portable copy, open `artifacts/QuickPrompt/QuickPrompt.exe`. Closing or dismissing the launcher keeps it running; right-click and choose **Exit** to quit.

To install on another PC, copy just the MSI to that PC and double-click it. Each PC keeps its own library. To transfer prompts, exit QuickPrompt on both PCs and copy `%LOCALAPPDATA%\QuickPrompt\prompts.json` to the same location in the other user's profile. Document files and shortcut targets must exist on the destination PC; change the document folder in Settings if needed.

Close QuickPrompt before upgrades or uninstalling. Uninstall through Windows **Settings → Apps → Installed apps**. Your prompt library, backups and document folders are preserved. Disable automatic startup through **Settings → Apps → Startup** if desired. The MSI supports repair and major upgrades; each future release must increase the three-part product version. The current package is not code-signed.

For Logi Options+ Actions Ring, use **Open Application → QuickPrompt** after installing. Restart Options+ if it has cached its app list. Choose the ordinary **QuickPrompt** entry; **QuickPrompt (Autostart)** starts silently and is intended for Windows sign-in. The installer places that background shortcut in the current user's Startup folder. Alternatively use the **Alt+Space** keyboard-shortcut action. The Start-menu shortcut and the running process use the same Windows application ID, `QuickPrompt.Desktop`.

**Alt+Space** opens and closes the launcher. If another application (such as Launchy) owns that shortcut, QuickPrompt explains the conflict and uses an available alternative for that session. Close the conflicting application, then open **Settings → General** and select **Alt+Space**. QuickPrompt never closes another launcher automatically.

The compact launcher centers in the work area of the monitor containing your pointer and stays above normal application windows. Its height follows the matching results, with up to six rows before scrolling and a small empty-state message when nothing matches. Drag the top header to move it anywhere on screen. Searching and changing results keep its position while open; each time you reopen it, it centers again on the monitor containing your pointer. It hides when you click elsewhere, press Escape, press the shortcut again, or copy a prompt. Windows secure desktops and exclusive full-screen applications control their own display priority.

## Keyboard guide

| Key | Action |
| --- | --- |
| Alt+Space | Show / hide (unless a fallback shortcut is active) |
| I / K, or ↑ / ↓ | Previous / next prompt |
| J / L | Previous / next category |
| Tab / Shift+Tab | Next / previous category |
| Alt+1 … Alt+5 | Jump to category / Documents |
| / or Ctrl+F | Focus search |
| Ctrl+Backspace in search | Clear search and return to navigation |
| Enter | Copy the selected prompt or open the selected document, then hide |
| F5 | Refresh the document list |
| Ctrl+N | Add a prompt |
| F2 | Edit the selected prompt |
| Ctrl+Enter in editor | Save the prompt |
| Enter in placeholder popup | Next value, or copy the completed prompt on the last value |
| Shift+Enter in placeholder popup | Insert a new line |
| Escape | Hide launcher, or cancel editor/settings |
| Ctrl+, | Settings |

The launcher opens with the search field focused: start typing immediately, including I/J/K/L. Matching prompt titles appear on every keystroke across all active categories, ignoring case. Arrow keys navigate results, and Enter copies the selected prompt. Clicking a prompt or pressing Ctrl+Backspace switches to list navigation, where I/J/K/L navigate prompts and categories. **Settings → General** can limit title searches to the active category. With an empty query, the list shows the active category.

Click a category to switch. Double-click a prompt to copy it. **+ New prompt** opens an editor for name, description, category, and multiline prompt text. F2 opens the same editor for existing prompts, including a Delete button with confirmation. The right-click menu includes **Settings** and **Exit**. Manage your categories in **Settings → Categories**.

Add placeholders such as `{FileName}` or `{Topic}` to fill them in when you use a prompt. Names must start with an uppercase ASCII letter and contain letters only, with no spaces, numbers, or punctuation. Enter or double-click opens a popup for each distinct name in first-appearance order. Repeated names reuse the same value. Paste multiline text or use **Shift+Enter** for a new line. The popup and launcher stay visible above normal windows while you click elsewhere or Alt+Tab to copy text; your current field and earlier values are preserved. **Enter** advances, then copies the completed prompt and closes the launcher after the last value. **Escape** or the title-bar **×** cancels without changing the clipboard. Values are required and used only for that invocation; the saved prompt keeps its placeholders. Other brace content (including JSON, `{lowerCase}`, and `{{FileName}}`) stays literal. QuickPrompt continues running in the tray as usual.

In **Settings → Categories**, drag a row by its dotted handle to reorder your prompt categories, or use its up/down buttons. You can rename categories in the same rows. **Save settings** applies the order; **Cancel** discards changes. Prompts keep their category when it moves. The launcher, Tab/J/L navigation, Alt+1 … Alt+4 shortcuts, and the editor's category picker follow the saved order. Documents stays last (Alt+5).

Each row in **Settings → Categories** has a **Delete** button. Confirm the category name and prompt count, then click **Save settings** to commit. A permanent JSON and text backup is completed before the category and all its prompts are removed. If backup or save fails, the live category and prompts are restored; **Cancel** discards pending deletions. Remaining categories keep their prompt identities and order. You can remove every prompt category and continue using Documents. Use **+ Add category** to create a new category in a free slot (up to four); save pending deletions before reusing their slots. Adding a category never restores previously deleted prompts. Documents cannot be deleted and remains available through Alt+5.

## Prompt languages

Use the small **EN ↔ DE** switch at the top center to choose the prompt text's language. English is the default; the selected language is remembered. Each prompt has one shared English title and description, so titles, descriptions, and title search stay the same in both modes. Category names, document results, and app controls keep their existing labels.

The prompt editor has **EN · English** and **DE · Deutsch** tabs for the body only. Add a shared English title and prompt text in at least one language; the shared description is optional. Adding German text reuses the title and description already entered. Switching tabs preserves unsaved body text. Each body can have its own placeholders. Delete removes the whole prompt.

**Non-bilingual — use one version in both languages** is off by default. Check it for a prompt intended to stay in only one language. The selected body is used in either launcher language without a missing-language warning. If the selected tab is empty, the editor switches to the existing version. The other language tab is disabled while checked; uncheck to edit a second version. Saving a previously bilingual prompt in this mode keeps only the selected body, with the earlier versions preserved in the permanent backup folder. Toggling the checkbox without saving discards no text.

When a version is missing, **Prompt not in this language** appears before anything is copied. Choose **Use English** or **Use German** to acknowledge and copy the available version, or **Cancel** to keep the clipboard unchanged. **Edit now** opens the missing language with its prompt field focused for pasting. After saving, press Enter on the prompt to use the new version.

Existing prompt bodies are never automatically translated. On the first launch of this version, separate legacy German titles and descriptions are removed after a permanent JSON/text backup. Existing English labels become the shared labels; if a legacy prompt has only German labels, those are preserved as shared labels so it remains identifiable. New libraries include English and German bodies for all nine example prompts, each with a shared English title and description.

## Documents

Place your files in `C:\Launchy`, or choose a different folder in **Settings → Documents → Browse**. Supported files are **PDF, JPG/JPEG, PNG, HTML/HTM, TXT, SLN/SLNX, and Windows shortcuts (.lnk)**. Text files and Visual Studio solutions open in their Windows default applications, just like other documents. Shortcuts are displayed without the `.lnk` suffix and open through Windows, preserving their target, arguments, and working directory, including targets outside the catalog folder. The **Open folder** button opens that folder in Explorer (and creates it if needed). Subfolders are included by default; turn this off in Settings if desired. Hidden/system files and filesystem directory links are skipped; normal `.lnk` shortcuts to folders are supported.

Type a filename in the main search to find documents alongside matching prompts, or select **Documents** (Alt+5) to browse only files. Each result says **Open** or **Copy** so you can tell which action Enter will perform. File extensions and relative paths help distinguish results. Hover a document to see its full path. Enter or double-click opens a document in its Windows default application and hides QuickPrompt; it does not change the clipboard. F2 edits prompts only.

The folder is scanned in the background at startup, whenever you open the launcher, and after saving Settings. Press **F5** or choose **Refresh documents** in the right-click menu to rescan immediately. Files are indexed by filename, not their contents. Missing folders and failures to open files show a message. No file is copied into the prompt library.

## Storage

Your library is saved at `%LOCALAPPDATA%\QuickPrompt\prompts.json`. Each save replaces the file atomically and keeps the previous version in `prompts.json.bak`. If the primary JSON file is damaged, QuickPrompt preserves it and attempts recovery from the backup. If neither file can be read, it reports the problem without replacing your data.

Before saving a prompt deletion, addition, edit, or category change, QuickPrompt also saves the **entire previous library** in `%LOCALAPPDATA%\QuickPrompt\bak`. Each backup has a unique UTC timestamp in its filename and comes as both readable UTF-8 `.txt` and structured `.json`. It includes category names and IDs, prompt IDs, all English and German text, the backup time, and the reason for the change. Deleted prompts are identified explicitly. These historical backups are never automatically overwritten or removed. Switching language, navigating categories, or saving unchanged settings does not create extra history. If either backup file cannot be completed, the change is blocked and the original library is preserved.

Open the folder through **Settings → About → Open backups**. To recover an individual prompt, open its text backup and paste the English/German sections into a new prompt. To restore an entire library, exit QuickPrompt from its tray menu, then save the JSON backup's `Library` object as `prompts.json` (keep a copy of the current file first). The JSON wrapper contains backup metadata and must not be copied wholesale over the live library. Category deletion archives the removed category explicitly under `RemovedCategories`, including all its prompts and language versions, in addition to the full previous library.

The first launch includes nine editable example prompts. MSI installations start automatically in the tray at sign-in. Portable copies can be launched with `--hidden` to start in the tray; a second hidden launch does not open an existing instance.

## Build and verify

Requires the .NET 10 SDK on Windows.

```powershell
dotnet build QuickPrompt.csproj -c Release
dotnet run --project tests/QuickPrompt.Tests.csproj -c Release
dotnet run --project tests/Ui/QuickPrompt.Ui.Tests.csproj -c Release
dotnet publish QuickPrompt.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/QuickPrompt
pwsh -File installer/Build-Installer.ps1
```

The dependency-free test runner covers placeholder parsing and exact substitution, search, content persistence, categories, backup recovery, and invalid data protection. The Windows WPF integration runner checks popup navigation, cancellation, clipboard retries, final copy/hide behavior, and keeping both windows visible during focus changes. For manual testing with a separate library, launch with `--data-dir C:\path\to\test-library`. Optional `--diagnostics` writes launcher visibility and focus events beside that library; it does not log prompt contents.

The MSI build script installs the pinned WiX 4.0.6 tool and matching UI extension into workspace-local folders, publishes the application, includes runtime notices, runs MSI validation, and emits the MSI plus a SHA-256 checksum under `artifacts/installer`. The initial build needs NuGet access. Keep the UpgradeCode and component GUIDs in `installer/QuickPrompt.wxs` stable between releases. Installer payload and app data use separate folders; never add the library or document folder to MSI removal rules.
