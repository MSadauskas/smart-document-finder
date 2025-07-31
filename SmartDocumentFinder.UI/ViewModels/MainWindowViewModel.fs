namespace SmartDocumentFinder.UI.ViewModels

open System
open System.Collections.ObjectModel
open ReactiveUI
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

type SearchResultViewModel(title: string, content: string, documentPath: string, score: float) =
    inherit ViewModelBase()
    
    let mutable isExpanded = false
    
    member _.Title = title
    member _.Content = content 
    member _.DocumentPath = documentPath
    member _.Score = $"Score: {score:F3}"
    member this.IsExpanded 
        with get() = isExpanded
        and set(value) = 
            this.RaiseAndSetIfChanged(&isExpanded, value) |> ignore
            this.RaisePropertyChanged("PreviewContent")
    member _.PreviewContent = 
        // Normalize line breaks first
        let normalizedContent = content.Replace("\r\n", "\n").Replace("\r", "\n")
        
        if isExpanded then 
            // Show full content when expanded
            normalizedContent
        else 
            // Show condensed preview
            if normalizedContent.Length > 200 then 
                let trimmed = normalizedContent.Substring(0, 200)
                // Try to break at a word boundary
                let lastSpace = trimmed.LastIndexOf(' ')
                if lastSpace > 150 then trimmed.Substring(0, lastSpace) + "..."
                else trimmed + "..."
            else normalizedContent
    member this.ToggleExpanded() =
        this.IsExpanded <- not this.IsExpanded
    
    member _.OpenDocument() =
        try
            if System.IO.File.Exists(documentPath) then
                let psi = System.Diagnostics.ProcessStartInfo()
                psi.FileName <- documentPath
                psi.UseShellExecute <- true
                System.Diagnostics.Process.Start(psi) |> ignore
            else
                printfn "File not found: %s" documentPath
        with ex ->
            printfn "Error opening document: %s" ex.Message
    
    member _.ShowInFinder() =
        try
            if System.IO.File.Exists(documentPath) then
                let psi = System.Diagnostics.ProcessStartInfo()
                if System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) then
                    psi.FileName <- "open"
                    psi.Arguments <- $"-R \"{documentPath}\""
                elif System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) then
                    psi.FileName <- "explorer"
                    psi.Arguments <- $"/select,\"{documentPath}\""
                else // Linux
                    psi.FileName <- "xdg-open"
                    psi.Arguments <- $"\"{System.IO.Path.GetDirectoryName(documentPath)}\""
                psi.UseShellExecute <- true
                System.Diagnostics.Process.Start(psi) |> ignore
            else
                printfn "File not found: %s" documentPath
        with ex ->
            printfn "Error showing in finder: %s" ex.Message

type MainWindowViewModel() =
    inherit ViewModelBase()
    
    let mutable searchText = ""
    let mutable folderPath = ""
    let mutable statusText = "Ready"
    let mutable scanStatus = ""
    let mutable indexStatus = "Loading index info..."
    let mutable documentCount = 0
    let mutable languageBreakdown = ""
    let mutable sourcePaths = ""
    let mutable selectedLanguage = "All Languages"
    let searchResults = ObservableCollection<SearchResultViewModel>()
    let availableLanguages = ObservableCollection<string>(["All Languages"; "English"; "Lithuanian"])
    
    let dbPath = CrossPlatform.getDefaultDatabasePath()
    let dbDir = System.IO.Path.GetDirectoryName(dbPath)
    let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
    let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
    let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
    let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
    let metadataStore = DocumentMetadataStore(dbPath)
    let indexInfo = DocumentIndexInfo(dbPath)
    
    do
        System.IO.Directory.CreateDirectory(dbDir) |> ignore
        async {
            let! _ = Database.initializeDatabase(dbPath)
            ()
        } |> Async.Start
    
    member this.SearchText 
        with get() = searchText
        and set(value) = 
            this.RaiseAndSetIfChanged(&searchText, value) |> ignore
    
    member this.FolderPath
        with get() = folderPath
        and set(value) = 
            this.RaiseAndSetIfChanged(&folderPath, value) |> ignore
    
    member this.StatusText 
        with get() = statusText
        and set(value) = 
            this.RaiseAndSetIfChanged(&statusText, value) |> ignore
    
    member this.ScanStatus
        with get() = scanStatus
        and set(value) =
            this.RaiseAndSetIfChanged(&scanStatus, value) |> ignore

    member this.IndexStatus
        with get() = indexStatus
        and set(value) =
            this.RaiseAndSetIfChanged(&indexStatus, value) |> ignore

    member this.DocumentCount
        with get() = documentCount
        and set(value) =
            this.RaiseAndSetIfChanged(&documentCount, value) |> ignore

    member this.LanguageBreakdown
        with get() = languageBreakdown
        and set(value) =
            this.RaiseAndSetIfChanged(&languageBreakdown, value) |> ignore

    member this.SourcePaths
        with get() = sourcePaths
        and set(value) =
            this.RaiseAndSetIfChanged(&sourcePaths, value) |> ignore

    member this.SelectedLanguage
        with get() = selectedLanguage
        and set(value) =
            this.RaiseAndSetIfChanged(&selectedLanguage, value) |> ignore

    member this.AvailableLanguages = availableLanguages

    member this.SearchResults = searchResults
    
    member this.Initialize() =
        async {
            do! this.LoadIndexStatus()
        } |> Async.Start

    member private this.LoadIndexStatus() =
        async {
            try
                printfn "🔄 Loading index status..."
                match! indexInfo.GetIndexStats() with
                | Ok stats ->
                    printfn "✅ Index stats loaded successfully"
                    this.DocumentCount <- stats.TotalDocuments
                    this.ScanStatus <- $"Database: {stats.TotalDocuments} documents indexed"

                    // Format language breakdown
                    let langText =
                        stats.LanguageBreakdown
                        |> Map.toList
                        |> List.map (fun (lang, count) ->
                            let langName =
                                match lang with
                                | "lt" -> "🇱🇹 Lithuanian"
                                | "en" -> "🇬🇧 English"
                                | _ -> lang
                            $"{langName}: {count}")
                        |> String.concat " • "

                    this.LanguageBreakdown <- if langText = "" then "No documents" else langText

                    // Format source paths
                    let pathText =
                        if stats.SourcePaths.IsEmpty then
                            "No sources indexed"
                        else
                            stats.SourcePaths
                            |> List.take (min 3 stats.SourcePaths.Length)
                            |> List.map System.IO.Path.GetFileName
                            |> String.concat " • "
                            |> fun text -> if stats.SourcePaths.Length > 3 then text + $" (+{stats.SourcePaths.Length - 3} more)" else text

                    this.SourcePaths <- pathText

                    // Format index status
                    let statusText =
                        match stats.LastIndexed with
                        | Some lastTime ->
                            let timeAgo = DateTime.Now - lastTime
                            if timeAgo.TotalDays < 1.0 then
                                let timeStr = lastTime.ToString("HH:mm")
                                $"Last indexed: {timeStr} today"
                            elif timeAgo.TotalDays < 7.0 then
                                let timeStr = lastTime.ToString("ddd HH:mm")
                                $"Last indexed: {timeStr}"
                            else
                                let timeStr = lastTime.ToString("MMM dd")
                                $"Last indexed: {timeStr}"
                        | None -> "No documents indexed yet"

                    this.IndexStatus <- statusText

                | Error err ->
                    printfn "❌ Error loading index stats: %A" err
                    this.ScanStatus <- "Database: Ready to scan"
                    this.IndexStatus <- "Unable to load index info"
                    this.LanguageBreakdown <- "Unknown"
                    this.SourcePaths <- "Unknown"
            with
            | ex ->
                printfn "💥 Exception loading index status: %s" ex.Message
                this.ScanStatus <- "Database: Ready to scan"
                this.IndexStatus <- "Error loading index info"
                this.LanguageBreakdown <- "Error"
                this.SourcePaths <- "Error"
        }
    
    member this.ScanFolder() =
        async {
            let! _ = Database.initializeDatabase(dbPath)
            this.ScanStatus <- "Scanning..."
            
            match! FolderScanner.scanFolder folderPath with
            | Ok documents ->
                this.ScanStatus <- $"Found {documents.Length} documents. Indexing..."
                
                match! FolderScanner.indexDocuments searchEngine documents with
                | Ok indexed ->
                    this.ScanStatus <- $"Indexed {indexed} documents"
                    do! this.LoadIndexStatus()  // Refresh all index info
                | Error _ ->
                    this.ScanStatus <- "Indexing failed"
            | Error _ ->
                this.ScanStatus <- "Scan failed"
        } |> Async.Start
    
    member private this.UpdateDocumentCount() =
        async {
            match! metadataStore.GetDocumentCount() with
            | Ok count ->
                this.ScanStatus <- $"Database: {count} documents indexed"
            | Error _ ->
                this.ScanStatus <- "Database: Unable to count documents"
        }
    
    member this.Search() =
        this.SearchDocuments()

    member this.SearchDocuments() =
        async {
            this.StatusText <- "Searching..."
            searchResults.Clear()
            
            // Create language filter based on selection
            let languageFilter =
                match selectedLanguage with
                | "All Languages" -> Map.empty
                | "English" -> Map.add "language" "en" Map.empty
                | "Lithuanian" -> Map.add "language" "lt" Map.empty
                | _ -> Map.empty

            let query = {
                Id = QueryId (Guid.NewGuid())
                Text = searchText
                Filters = languageFilter
                MaxResults = 10
                Timestamp = DateTime.Now
                Language = Some (LanguageDetection.detectQueryLanguage searchText)
            }
            
            try
                match! searchEngine.Search(query) with
                | Ok response ->
                    printfn "✅ UI Search success: %d results" response.Results.Length
                    let relevantResults = 
                        response.Results 
                        |> List.filter (fun r -> let (SearchResultScore s) = r.Score in s > 0.2)
                        |> List.sortByDescending (fun r -> let (SearchResultScore s) = r.Score in s)
                        |> List.take (min 8 response.Results.Length)
                    
                    for result in relevantResults do
                        let (DocumentPath path) = result.DocumentPath
                        let fileName = System.IO.Path.GetFileName(path)
                        // Pass full content to ViewModel with actual score
                        let (SearchResultScore actualScore) = result.Score
                        let vm = SearchResultViewModel(fileName, result.ChunkContent, path, actualScore)
                        searchResults.Add(vm)
                    this.StatusText <- $"Found {relevantResults.Length} relevant documents"
                | Error err ->
                    printfn "❌ UI Search error: %A" err
                    this.StatusText <- "Search failed"
            with ex ->
                printfn "💥 UI Search exception: %s" ex.Message
                this.StatusText <- "Search failed"
        } |> Async.Start
