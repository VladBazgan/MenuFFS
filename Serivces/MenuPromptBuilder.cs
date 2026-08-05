namespace MenuFFS.Services;

public sealed class MenuPromptBuilder
{
    public string BuildSystemPrompt(string sourceLanguage, string targetLanguage)
    {
        var sourceInstruction = sourceLanguage == "Detectare automată"
            ? "Detectează automat limba sau limbile meniului."
            : $"Meniul este în principal în limba {sourceLanguage}.";

        return $$"""
            Ești MenuFFS, un traducător specializat în meniuri de restaurant.
            {{sourceInstruction}}
            Tradu conținutul în limba {{targetLanguage}}.

            Reguli obligatorii:
            - Analizează direct imaginea ca intrare vizuală atunci când este furnizată; nu descrie procesul tehnic.
            - Returnează exclusiv Markdown valid, fără blocuri ```markdown și fără introduceri conversaționale.
            - Păstrează structura meniului: categorii, preparate, descrieri, variante și note.
            - Păstrează exact toate prețurile, monedele, gramajele, volumele și numerele vizibile.
            - Nu inventa ingrediente, prețuri, alergeni sau explicații care nu rezultă din meniu.
            - Pentru o denumire locală greu de tradus, păstrează denumirea originală și adaugă o explicație scurtă în limba țintă.
            - Marchează orice text imposibil de citit cu „[text neclar]”. Nu ghici.
            - Menționează alergenii doar dacă sunt scriși explicit sau rezultă fără ambiguitate din ingredientele enumerate; etichetează inferențele drept „posibil”.
            - Folosește titlu de nivel 1 pentru meniu, titluri de nivel 2 pentru categorii și titluri de nivel 3 pentru preparate.
            - Scrie prețul pe un rând separat în forma **Preț:** valoare.
            - Nu transforma meniul într-un tabel decât dacă sursa este deja un tabel și tabelul ajută claritatea.
            """;
    }

    public string BuildUserPrompt(bool hasImage, string? menuText)
    {
        if (hasImage && !string.IsNullOrWhiteSpace(menuText))
        {
            return $$"""
                Tradu și structurează meniul din imaginea atașată. Folosește și următorul text introdus de utilizator drept context suplimentar:

                {{menuText.Trim()}}
                """;
        }

        if (hasImage)
        {
            return "Tradu și structurează complet meniul din imaginea atașată. Citește direct toate zonele vizibile ale imaginii și păstrează ordinea logică a meniului.";
        }

        return $$"""
            Tradu și structurează următorul text de meniu:

            {{menuText?.Trim()}}
            """;
    }
}
