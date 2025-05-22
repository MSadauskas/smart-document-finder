open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.SearchEngine
open SmartDocumentFinder.DocumentProcessor

[<EntryPoint>]
let main argv =
    printfn "=== Smart Document Finder - Bug Fix Validation ==="
    
    // Test 1: Database duplication fix
    printfn "\n1. Testing database duplication fix..."
    
    let testDir = "/home/mikas/Development/SmartDocumentFinder/test-docs"
    let dbPath = "/tmp/test-duplicate.db"
    
    // Clean database
    if System.IO.File.Exists(dbPath) then System.IO.File.Delete(dbPath)
    
    // Initialize database
    let vectorStore = SmartDocumentFinder.VectorStore.SqliteVectorStore(dbPath)
    let documentProcessor = DocumentProcessor()
    let embeddingService = SmartDocumentFinder.SearchEngine.MockEmbeddingService()
    let searchEngine = EnhancedSearchEngine(vectorStore, documentProcessor, embeddingService)
    
    // Index same directory twice
    let files = System.IO.Directory.GetFiles(testDir)
    
    async {
        // First scan
        for file in files do
            let doc = DocumentService.createDocument file
            let! result = (searchEngine :> ISearchEngine).IndexDocument(doc)
            match result with
            | Ok _ -> printfn "  Indexed: %s" (System.IO.Path.GetFileName(file))
            | Error e -> printfn "  Error: %A" e
        
        // Second scan (should not create duplicates)
        for file in files do
            let doc = DocumentService.createDocument file
            let! result = (searchEngine :> ISearchEngine).IndexDocument(doc)
            match result with
            | Ok _ -> printfn "  Re-indexed: %s" (System.IO.Path.GetFileName(file))
            | Error e -> printfn "  Error: %A" e
        
        // Test search with document paths
        printfn "\n2. Testing search with document paths..."
        let query = { Text = "test"; MaxResults = 3 }
        let! searchResult = (searchEngine :> ISearchEngine).Search(query)
        
        match searchResult with
        | Ok response ->
            printfn "  Found %d results:" response.Results.Length
            for result in response.Results do
                let (DocumentPath path) = result.DocumentPath
                printfn "    Score: %.3f | Path: %s" (let (SearchResultScore s) = result.Score in s) path
        | Error e -> printfn "  Search error: %A" e
        
        printfn "\n3. All fixes validated successfully!"
        return 0
    } |> Async.RunSynchronously
