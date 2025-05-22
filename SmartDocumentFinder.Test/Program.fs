open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

[<EntryPoint>]
let main argv =
    printfn "=== Smart Document Finder - Final Test ==="
    
    let testWorkflow () = async {
        // Initialize
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SimpleEmbeddingService() :> IEmbeddingService
        let vectorStore = SqliteVectorStore("/tmp/final-test.db") :> IVectorStore
        let searchEngine = BinarySearchEngine(vectorStore, processor) :> ISearchEngine
        
        let! _ = Database.initializeDatabase("/tmp/final-test.db")
        
        // Test folder scanning
        printfn "📁 Scanning folder..."
        match! FolderScanner.scanFolder("/home/mikas/Development/SmartDocumentFinder/test-docs") with
        | Ok documents ->
            printfn $"✅ Found {documents.Length} documents"
            
            // Index documents
            match! FolderScanner.indexDocuments searchEngine documents with
            | Ok indexed ->
                printfn $"✅ Indexed {indexed} documents"
                
                // Test search
                let query = {
                    Id = QueryId (Guid.NewGuid())
                    Text = "machine learning"
                    Filters = Map.empty
                    MaxResults = 5
                    Timestamp = DateTime.Now
                }
                
                match! searchEngine.Search(query) with
                | Ok response ->
                    printfn $"🔍 Search results: {response.Results.Length}"
                    for result in response.Results do
                        printfn $"  - Score: {(let (SearchResultScore s) = result.Score in s):F3}"
                    return true
                | Error _ -> return false
            | Error _ -> return false
        | Error _ -> return false
    }
    
    let success = testWorkflow () |> Async.RunSynchronously
    let resultText = if success then "SUCCESS" else "FAILED"
    printfn $"Result: {resultText}"
    if success then 0 else 1
