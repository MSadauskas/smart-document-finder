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
        // Initialize with semantic embeddings
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
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
                
                // Test semantic search with different queries
                let testQueries = [
                    "machine learning"
                    "python programming"
                    "financial report"
                ]
                
                let mutable querySuccess = true
                for queryText in testQueries do
                    let query = {
                        Id = QueryId (Guid.NewGuid())
                        Text = queryText
                        Filters = Map.empty
                        MaxResults = 5
                        Timestamp = DateTime.Now
                    }
                    
                    printfn "\n🔍 Testing query: '%s'" queryText
                    match! searchEngine.Search(query) with
                    | Ok response ->
                        printfn "📊 Found %d relevant documents" response.Results.Length
                        for result in response.Results do
                            printfn "  ✅ Score: %.3f - %A" (let (SearchResultScore s) = result.Score in s) result.DocumentPath
                    | Error err -> 
                        printfn "❌ Query failed: %A" err
                        querySuccess <- false
                
                return querySuccess
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
