module TestRunner

open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.SearchEngine
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore

[<EntryPoint>]
let main argv =
    printfn "=== DB Clean Test ==="
    
    let testDir = "/home/mikas/Development/SmartDocumentFinder/test-docs"
    let dbPath = "/home/mikas/.smartdoc/data.db"
    
    System.IO.Directory.CreateDirectory("/home/mikas/.smartdoc") |> ignore
    
    async {
        // Init DB
        match! Database.initializeDatabase dbPath with
        | Ok _ -> printfn "✅ DB init"
        | Error e -> 
            printfn "❌ DB fail: %A" e
            return 1
        
        let vectorStore = SqliteVectorStore(dbPath)
        let documentProcessor = DocumentService.DocumentProcessor()
        let embeddingService = SimpleEmbeddingService()
        let searchEngine = EnhancedSearchEngine(vectorStore, documentProcessor, embeddingService)
        
        let files = System.IO.Directory.GetFiles(testDir)
        
        // First scan
        printfn "=== SCAN 1 ==="
        for file in files do
            let doc = DocumentService.createDocument file
            let! result = (searchEngine :> ISearchEngine).IndexDocument(doc)
            match result with
            | Ok _ -> printfn "  ✅ %s" (System.IO.Path.GetFileName(file))
            | Error e -> printfn "  ❌ %A" e
        
        // Second scan
        printfn "=== SCAN 2 ==="
        for file in files do
            let doc = DocumentService.createDocument file
            let! result = (searchEngine :> ISearchEngine).IndexDocument(doc)
            match result with
            | Ok _ -> printfn "  ✅ %s" (System.IO.Path.GetFileName(file))
            | Error e -> printfn "  ❌ %A" e
        
        // Search test
        printfn "=== SEARCH ==="
        let query = { 
            Id = QueryId (Guid.NewGuid())
            Text = "test"
            Filters = Map.empty
            MaxResults = 5
            Timestamp = DateTime.Now
        }
        let! searchResult = (searchEngine :> ISearchEngine).Search(query)
        
        match searchResult with
        | Ok response ->
            printfn "Found %d results:" response.Results.Length
            for result in response.Results do
                let (DocumentPath path) = result.DocumentPath
                let (SearchResultScore score) = result.Score
                printfn "  📄 %.3f | %s" score (System.IO.Path.GetFileName(path))
        | Error e -> 
            printfn "❌ Search fail: %A" e
            return 1
        
        printfn "✅ Done"
        return 0
    } |> Async.RunSynchronously
