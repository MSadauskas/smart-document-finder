open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

[<EntryPoint>]
let main argv =
    printfn "=== Semantic Search Tests ==="
    
    let testQueries () = async {
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        let dbPath = CrossPlatform.getTestDatabasePath()
        let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
        let searchEngine = BinarySearchEngine(vectorStore, DocumentService.DocumentProcessor(), embeddingService, dbPath) :> ISearchEngine
        
        let queries = ["python programming"; "financial report"; "artificial intelligence"]
        
        for queryText in queries do
            let query = { Id = QueryId (Guid.NewGuid()); Text = queryText; Filters = Map.empty; MaxResults = 5; Timestamp = DateTime.Now }
            printfn "\n🔍 Query: '%s'" queryText
            match! searchEngine.Search(query) with
            | Ok response -> printfn "📊 Found %d relevant docs" response.Results.Length
            | Error _ -> printfn "❌ Failed"
        return true
    }
    
    testQueries () |> Async.RunSynchronously |> ignore
    0
