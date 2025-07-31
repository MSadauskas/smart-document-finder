namespace SmartDocumentFinder.Core

open System

module LanguageDetection =

    // Comprehensive Lithuanian word indicators
    let private lithuanianIndicators = Set.ofList [
        // Very common Lithuanian words
        "ir"; "yra"; "kad"; "su"; "bei"; "kaip"; "šis"; "tas"; "į"; "iš"; "per"; "po"; "už"
        "ne"; "nėra"; "buvo"; "bus"; "turi"; "turėti"; "gali"; "galima"; "reikia"; "reikalinga"
        "labai"; "daug"; "mažai"; "gerai"; "blogai"; "naujas"; "senas"; "didelis"; "mažas"
        "lietuvos"; "lietuvoje"; "lietuvių"; "lietuviška"; "lietuva"; "lietuviškai"
        "darbo"; "darbas"; "veikla"; "sistema"; "metodas"; "būdas"; "procesas"
        "analizė"; "tyrimas"; "rezultatas"; "išvados"; "duomenys"; "informacija"
        "technologijos"; "sprendimas"; "problema"; "klausimas"; "atsakymas"

        // Words with Lithuanian characters (strong indicators)
        "ką"; "kur"; "kaip"; "kodėl"; "kuris"; "kuri"; "kurie"; "kurių"; "kurią"; "kurį"
        "šiuo"; "šią"; "šie"; "šios"; "šių"; "ši"; "šį"; "šiame"; "šioje"
        "tą"; "tų"; "tuo"; "tuos"; "tos"; "toje"; "tame"; "tomis"
        "apie"; "dėl"; "pagal"; "prie"; "prieš"; "nuo"; "iki"; "tarp"
        "žmonės"; "žmogus"; "žinoti"; "žinia"; "žodis"; "žodžiai"; "žiūrėti"
        "ačiū"; "prašau"; "prašom"; "labas"; "viso"; "gero"; "sveiki"
        "metai"; "mėnuo"; "diena"; "valanda"; "minutė"; "sekundė"; "laikas"
        "šalis"; "miestas"; "gatvė"; "namas"; "kambarys"; "vieta"; "erdvė"
        "mokykla"; "universitetas"; "studentas"; "mokytojas"; "mokslas"
        "įmonė"; "organizacija"; "institucija"; "valdžia"; "vyriausybė"
        "sąvoka"; "samprata"; "supratimas"; "pažinimas"; "žinojimas"
        "galėjimas"; "gebėjimas"; "įgūdžiai"; "patirtis"; "praktika"
        "tikslai"; "uždaviniai"; "planai"; "projektai"; "programos"
        "sritis"; "sritys"; "sektorius"; "šaka"; "kryptis"; "kryptys"
        "būtina"; "svarbu"; "reikšminga"; "aktualu"; "aktualus"
        "pažanga"; "plėtra"; "vystymasis"; "tobulinimas"; "gerinimas"
        "kokybė"; "efektyvumas"; "našumas"; "produktyvumas"; "rezultatyvumas"
        "bendradarbiavimas"; "partnerystė"; "santykiai"; "ryšiai"; "komunikacija"
        "inovacijos"; "naujovės"; "modernizavimas"; "skaitmenizavimas"
        "aplinka"; "gamta"; "ekologija"; "tvarumas"; "ateitis"

        // Business and technical terms in Lithuanian
        "verslas"; "ataskaita"; "tyrimai"; "projektas"; "programa"
        "dokumentinis"; "filmas"; "kinas"; "video"; "medija"
        "programavimas"; "kompiuteris"; "technologija"; "mokymasis"; "algoritmas"
        "duomenų"; "bazė"; "sistema"; "tinklas"; "internetas"; "svetainė"
        "programinė"; "įranga"; "aparatūra"; "technologinė"; "skaitmeninė"
        "automatizavimas"; "optimizavimas"; "integravimas"; "diegimas"
        "valdymas"; "administravimas"; "priežiūra"; "palaikymas"
        "saugumas"; "apsauga"; "privatumas"; "konfidencialumas"
        "testavimas"; "tikrinimas"; "vertinimas"; "analizavimas"
        "kūrimas"; "projektavimas"; "dizainas"; "architektūra"
    ]

    // Common English words that rarely appear in Lithuanian
    let private englishIndicators = Set.ofList [
        "the"; "and"; "or"; "but"; "with"; "from"; "this"; "that"; "these"; "those"
        "have"; "has"; "had"; "will"; "would"; "could"; "should"; "must"; "can"
        "business"; "report"; "analysis"; "revenue"; "profit"; "financial"; "investment"
        "technology"; "computer"; "programming"; "algorithm"; "software"; "hardware"
        "machine"; "learning"; "artificial"; "intelligence"; "data"; "database"
        "engineering"; "development"; "documentation"; "tutorial"; "guide"
        "introduction"; "advanced"; "basic"; "fundamental"; "essential"
        "market"; "quarterly"; "annual"; "monthly"; "performance"; "metrics"
        "management"; "administration"; "organization"; "structure"; "framework"
        "implementation"; "deployment"; "integration"; "optimization"; "automation"
        "security"; "privacy"; "protection"; "encryption"; "authentication"
        "testing"; "validation"; "verification"; "evaluation"; "assessment"
        "design"; "architecture"; "infrastructure"; "platform"; "environment"
        "solution"; "approach"; "methodology"; "strategy"; "technique"
        "research"; "study"; "investigation"; "experiment"; "observation"
        "process"; "procedure"; "workflow"; "pipeline"; "lifecycle"
        "quality"; "efficiency"; "productivity"; "effectiveness"; "performance"
        "collaboration"; "partnership"; "relationship"; "communication"; "interaction"
        "innovation"; "modernization"; "digitalization"; "transformation"
        "sustainability"; "environment"; "ecology"; "future"; "development"
    ]
    
    let detectLanguage (text: string) : Language =
        if String.IsNullOrWhiteSpace(text) then
            Unknown
        else
            let words = text.ToLowerInvariant().Split([|' '; '\t'; '\n'; '\r'; '.'; ','; ';'; '!'; '?'; '('; ')'; '['; ']'; '"'; '\''|], StringSplitOptions.RemoveEmptyEntries)
            let wordSet = Set.ofArray words

            let lithuanianMatches = Set.intersect wordSet lithuanianIndicators |> Set.count
            let englishMatches = Set.intersect wordSet englishIndicators |> Set.count

            // Enhanced detection logic
            // 1. Check for Lithuanian characters (strong indicator)
            let hasLithuanianChars =
                text.Contains("ą") || text.Contains("č") || text.Contains("ę") ||
                text.Contains("ė") || text.Contains("į") || text.Contains("š") ||
                text.Contains("ų") || text.Contains("ū") || text.Contains("ž")

            // 2. Calculate confidence scores
            let totalWords = max 1 words.Length
            let lithuanianScore = float lithuanianMatches / float totalWords
            let englishScore = float englishMatches / float totalWords

            // 3. Make decision with multiple criteria
            if hasLithuanianChars && lithuanianMatches > 0 then
                Lithuanian
            elif lithuanianScore > 0.05 && lithuanianMatches >= 2 then  // At least 5% Lithuanian words and 2+ matches
                Lithuanian
            elif englishScore > 0.1 && englishMatches >= 3 then  // At least 10% English words and 3+ matches
                English
            elif lithuanianMatches > englishMatches && lithuanianMatches > 0 then
                Lithuanian
            elif englishMatches > lithuanianMatches && englishMatches > 0 then
                English
            else
                Unknown

    let detectQueryLanguage (queryText: string) : Language =
        detectLanguage queryText

    let detectDocumentLanguage (content: string) : Language =
        // For documents, we can be more confident with more text
        let detected = detectLanguage content

        // If we can't detect from content, default to English for very short texts
        // but be more conservative for longer texts
        match detected with
        | Unknown when content.Length < 50 -> English  // Very short texts default to English
        | lang -> lang

    let languageToString (language: Language) : string =
        match language with
        | Lithuanian -> "lt"
        | English -> "en"
        | Unknown -> "unknown"

    let stringToLanguage (str: string) : Language =
        match str.ToLowerInvariant() with
        | "lt" | "lithuanian" -> Lithuanian
        | "en" | "english" -> English
        | _ -> Unknown
