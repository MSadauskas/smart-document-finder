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
    let searchResults = ObservableCollection<SearchResultViewModel>()
    
    let dbPath = CrossPlatform.getDefaultDatabasePath()
    let dbDir = System.IO.Path.GetDirectoryName(dbPath)
    let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
    let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
    let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
    let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
    let metadataStore = DocumentMetadataStore(dbPath)
    
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
    
    member this.SearchResults = searchResults
    
    member this.Initialize() =
        async {
            match! metadataStore.GetDocumentCount() with
            | Ok count -> this.ScanStatus <- $"Database: {count} documents indexed"
            | Error _ -> this.ScanStatus <- "Database: Ready to scan"
        } |> Async.Start
    
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
                    do! this.UpdateDocumentCount()
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
        async {
            this.StatusText <- "Searching..."
            searchResults.Clear()
            
            let query = {
                Id = QueryId (Guid.NewGuid())
                Text = searchText
                Filters = Map.empty
                MaxResults = 10
                Timestamp = DateTime.Now
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
