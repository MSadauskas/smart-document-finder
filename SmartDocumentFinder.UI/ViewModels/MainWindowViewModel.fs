namespace SmartDocumentFinder.UI.ViewModels

open System
open System.Collections.ObjectModel
open ReactiveUI
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

type SearchResultViewModel(title: string, content: string, score: float) =
    member _.Title = title
    member _.Content = content 
    member _.Score = $"Score: {score:F3}"

type MainWindowViewModel() =
    inherit ViewModelBase()
    
    let mutable searchText = ""
    let mutable folderPath = ""
    let mutable statusText = "Ready"
    let mutable scanStatus = ""
    let searchResults = ObservableCollection<SearchResultViewModel>()
    
    let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
    let embeddingService = TextEmbeddingService() :> IEmbeddingService
    let vectorStore = SqliteVectorStore("/home/mikas/.smartdoc/data.db") :> IVectorStore
    let searchEngine = EnhancedSearchEngine(vectorStore, processor, embeddingService, "/home/mikas/.smartdoc/data.db") :> ISearchEngine
    let metadataStore = DocumentMetadataStore("/home/mikas/.smartdoc/data.db")
    
    do
        System.IO.Directory.CreateDirectory("/home/mikas/.smartdoc") |> ignore
        async {
            let! _ = Database.initializeDatabase("/home/mikas/.smartdoc/data.db")
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
            let! _ = Database.initializeDatabase("/home/mikas/.smartdoc/data.db")
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
                    let relevantResults = response.Results 
                        |> List.filter (fun r -> let (SearchResultScore s) = r.Score in s > 0.2)
                        |> List.sortByDescending (fun r -> let (SearchResultScore s) = r.Score in s)
                        |> List.take (min 8 response.Results.Length)
                    
                    for result in relevantResults do
                        let (DocumentPath path) = result.DocumentPath
                        let fileName = System.IO.Path.GetFileName(path)
                        let preview = if result.ChunkContent.Length > 150 
                                     then result.ChunkContent.Substring(0, 150) + "..."
                                     else result.ChunkContent
                        let vm = SearchResultViewModel(fileName, preview, 0.0) // Hide score
                        searchResults.Add(vm)
                    this.StatusText <- $"Found {relevantResults.Length} relevant documents"
                | Error err ->
                    printfn "❌ UI Search error: %A" err
                    this.StatusText <- "Search failed"
            with ex ->
                printfn "💥 UI Search exception: %s" ex.Message
                this.StatusText <- "Search failed"
        } |> Async.Start
