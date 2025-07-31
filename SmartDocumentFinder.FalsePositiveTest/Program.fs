open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

[<EntryPoint>]
let main argv =
    printfn "🔍 TESTING FALSE POSITIVE REDUCTION"
    printfn "===================================="
    
    let testFalsePositives () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_false_positive_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Create test documents that should NOT match "dokumentinis"
            let testDocs = [
                ("business-report.txt", "This is a business and financial report covering market analysis, revenue projections, and investment opportunities.")
                ("ml-doc.txt", "This is a test document about machine learning and artificial intelligence. It contains information about neural networks and deep learning algorithms.")
                ("python-tutorial.txt", "Python Programming Tutorial: Advanced Data Structures and Algorithms. This comprehensive guide covers Python programming fundamentals.")
                ("software-doc.txt", "Software engineering best practices include clean code, testing, and documentation. This document covers various programming principles.")
                ("documentary-film.txt", "This is a documentary film about nature and wildlife. It shows various animals in their natural habitat and discusses environmental conservation.")
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
                    }
                    State = NotProcessed
                }
                
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> printfn "✅ Indexed: %s" filename
                | Error err -> printfn "❌ Failed to index %s: %A" filename err
            
            printfn ""
            printfn "Testing Lithuanian queries for false positives:"
            printfn "=============================================="
            
            let testQueries = [
                ("dokumentinis", "Should only match documentary-film.txt")
                ("filmas", "Should only match documentary-film.txt")
                ("dokumentinis filmas", "Should only match documentary-film.txt")
                ("programavimas", "Should only match python-tutorial.txt and software-doc.txt")
                ("verslas", "Should only match business-report.txt")
            ]
            
            for (queryText, expectedBehavior) in testQueries do
                printfn ""
                printfn "🔍 Query: '%s' (%s)" queryText expectedBehavior
                
                let query = {
                    Id = QueryId (Guid.NewGuid())
                    Text = queryText
                    Filters = Map.empty
                    MaxResults = 10
                    Timestamp = DateTime.Now
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
                        printfn "   ⚠️  No results found - might be too restrictive"
                    elif response.Results.Length > 3 then
                        printfn "   ❌ Too many results - likely false positives"
                    else
                        printfn "   ✅ Reasonable number of results"
                        
                | Error err ->
                    printfn "   ❌ Search failed: %A" err
            
            Directory.Delete(testDir, true)
            
        with
        | ex ->
            if Directory.Exists(testDir) then Directory.Delete(testDir, true)
            printfn "❌ Test failed: %s" ex.Message
    }
    
    try
        testFalsePositives () |> Async.RunSynchronously
        0
    with
    | ex ->
        printfn "❌ Test execution failed: %s" ex.Message
        1
