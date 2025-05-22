module TestSearch

open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.SearchEngine
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore

[<EntryPoint>]
let main argv =
    let dbPath = "/home/mikas/.smartdoc/data.db"
    System.IO.Directory.CreateDirectory("/home/mikas/.smartdoc") |> ignore
    
    async {
        printfn "=== SEARCH DEBUG ==="
        
        // Init
        let! _ = Database.initializeDatabase dbPath
        let processor = DocumentService.DocumentProcessor()
        let embedding = SimpleEmbeddingService()  
        let vectorStore = SqliteVectorStore(dbPath)
        let searchEngine = EnhancedSearchEngine(vectorStore, processor, embedding)
        
        // Index one doc
        let testFile = Array.head (System.IO.Directory.GetFiles("/home/mikas/Development/SmartDocumentFinder/test-docs"))
        let doc = DocumentService.createDocument testFile
        
        match! (searchEngine :> ISearchEngine).IndexDocument(doc) with
        | Ok _ -> printfn "✅ Indexed: %s" (System.IO.Path.GetFileName(testFile))
        | Error e -> printfn "❌ Index fail: %A" e
        
        // Check DB
        use conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}")
        conn.Open()
        
        let countDocs = (new Microsoft.Data.Sqlite.SqliteCommand("SELECT COUNT(*) FROM documents", conn)).ExecuteScalar() :?> int64
        let countChunks = (new Microsoft.Data.Sqlite.SqliteCommand("SELECT COUNT(*) FROM chunks", conn)).ExecuteScalar() :?> int64
        let countEmbeddings = (new Microsoft.Data.Sqlite.SqliteCommand("SELECT COUNT(*) FROM embeddings", conn)).ExecuteScalar() :?> int64
        
        printfn "DB: %d docs, %d chunks, %d embeddings" countDocs countChunks countEmbeddings
        
        // Search
        let query = {
            Id = QueryId (Guid.NewGuid())
            Text = "test"
            Filters = Map.empty
            MaxResults = 5
            Timestamp = DateTime.Now
        }
        
        match! (searchEngine :> ISearchEngine).Search(query) with
        | Ok response ->
            printfn "🔍 Found %d results" response.Results.Length
            for result in response.Results do
                let (SearchResultScore score) = result.Score
                printfn "  Score: %.3f | Content: %s" score (result.ChunkContent.Substring(0, min 50 result.ChunkContent.Length))
        | Error e -> printfn "❌ Search fail: %A" e
        
        return 0
    } |> Async.RunSynchronously
