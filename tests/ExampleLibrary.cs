namespace QuickPrompt;

// Deterministic sample content for behavior tests; independent of the shipped seed.
internal static class ExampleLibrary
{
    internal static Library Create()
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
