open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

[<EntryPoint>]
let main argv =
    printfn "=== Smart Document Finder - Cross-Platform Test ==="
    printfn "🖥️  %s" (CrossPlatform.getPlatformInfo())
    
    let testWorkflow () = async {
        // Initialize with cross-platform paths
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SimpleEmbeddingService() :> IEmbeddingService
        let dbPath = CrossPlatform.getTestDatabasePath()
        let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
        let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
        
        printfn "📁 Database: %s" dbPath
        
        // Ensure directory exists (cross-platform)
        CrossPlatform.ensureDirectoryExists(dbPath)
        
        let! _ = Database.initializeDatabase(dbPath)
        
        // Test folder scanning with cross-platform path
        let testDocsPath = CrossPlatform.getTestDocsPath()
        printfn "📂 Test docs: %s" testDocsPath
        
        match! FolderScanner.scanFolder(testDocsPath) with
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
                | Error err -> 
                    printfn "❌ Search failed: %A" err
                    return false
            | Error err -> 
                printfn "❌ Indexing failed: %A" err
                return false
        | Error err -> 
            printfn "❌ Folder scan failed: %A" err
            return false
    }
    
    let success = testWorkflow () |> Async.RunSynchronously
    let resultText = if success then "SUCCESS" else "FAILED"
    printfn $"Result: {resultText}"
    if success then 0 else 1
