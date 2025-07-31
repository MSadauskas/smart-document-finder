open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

[<EntryPoint>]
let main argv =
    printfn "🌍 TESTING LANGUAGE-AWARE SEARCH"
    printfn "================================="
    
    let testLanguageAwareSearch () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_language_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Create test documents in different languages
            let testDocs = [
                ("lithuanian-doc.txt", "Dokumentinis filmas apie gamtą ir gyvūnus. Šis filmas parodo įvairius gyvūnus jų natūralioje aplinkoje.")
                ("english-doc.txt", "This is a business and financial report covering market analysis, revenue projections, and investment opportunities.")
                ("programming-doc.txt", "Python programming tutorial about machine learning algorithms and data structures.")
            ]
            
            // Index all documents
            for (filename, content) in testDocs do
                let filePath = Path.Combine(testDir, filename)
                File.WriteAllText(filePath, content)
                
                let doc = {
                    Metadata = {
                        Id = DocumentId (Guid.NewGuid())
                        Path = DocumentPath filePath
                        FileName = filename
                        Size = int64 content.Length
                        Created = DateTime.Now
                        Modified = DateTime.Now
                        Format = PlainText
                        Hash = DocumentHash (content.GetHashCode().ToString())
                        Language = LanguageDetection.detectDocumentLanguage content
                    }
                    State = NotProcessed
                }
                
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> printfn "✅ Indexed: %s (Language: %A)" filename doc.Metadata.Language
                | Error err -> printfn "❌ Failed to index %s: %A" filename err
            
            printfn ""
            printfn "Testing language-aware queries:"
            printfn "==============================="
            
            let testQueries = [
                ("dokumentinis", Some Lithuanian, "Should only find Lithuanian documents")
                ("business", Some English, "Should only find English documents")
                ("programming", Some English, "Should only find English programming docs")
                ("dokumentinis", None, "Should find all documents (no language filter)")
            ]
            
            for (queryText, languageFilter, description) in testQueries do
                printfn ""
                printfn "🔍 Query: '%s' | Language: %A" queryText languageFilter
                printfn "   %s" description
                
                let query = {
                    Id = QueryId (Guid.NewGuid())
                    Text = queryText
                    Filters = Map.empty
                    MaxResults = 10
                    Timestamp = DateTime.Now
                    Language = languageFilter
                }
                
                match! searchEngine.Search(query) with
                | Ok response ->
                    printfn "   Results: %d documents found" response.Results.Length
                    
                    for result in response.Results do
                        let (DocumentPath path) = result.DocumentPath
                        let filename = Path.GetFileName(path)
                        let (SearchResultScore score) = result.Score
                        printfn "   ✅ %s (score: %.3f)" filename score
                    
                    if response.Results.Length = 0 then
                        printfn "   ℹ️  No results found"
                        
                | Error err ->
                    printfn "   ❌ Search failed: %A" err
            
            Directory.Delete(testDir, true)
            
        with
        | ex ->
            if Directory.Exists(testDir) then Directory.Delete(testDir, true)
            printfn "❌ Test failed: %s" ex.Message
    }
    
    try
        testLanguageAwareSearch () |> Async.RunSynchronously
        printfn ""
        printfn "🎯 Language-aware search test completed!"
        0
    with
    | ex ->
        printfn "❌ Test execution failed: %s" ex.Message
        1
