namespace SmartDocumentFinder.ComprehensiveTests

open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

module SearchEngineTests =
    
    type TestResult = {
        TestName: string
        Success: bool
        Message: string
        Duration: TimeSpan
    }
    
    let runTest (testName: string) (testFunc: unit -> Async<bool * string>) : Async<TestResult> =
        async {
            let startTime = DateTime.Now
            try
                let! (success, message) = testFunc()
                let duration = DateTime.Now - startTime
                return { TestName = testName; Success = success; Message = message; Duration = duration }
            with
            | ex ->
                let duration = DateTime.Now - startTime
                return { TestName = testName; Success = false; Message = ex.Message; Duration = duration }
        }
    
    let setupTestEnvironment () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_search_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        Directory.CreateDirectory(testDir) |> ignore
        let! _ = Database.initializeDatabase(dbPath)
        
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
        
        return (testDir, dbPath, processor, embeddingService, vectorStore)
    }
    
    let testBasicSearchEngine () = async {
        let! (testDir, dbPath, processor, _, vectorStore) = setupTestEnvironment()
        try
            let searchEngine = BasicSearchEngine(vectorStore, processor) :> ISearchEngine
            
            // Create a test query
            let query = {
                Id = QueryId (Guid.NewGuid())
                Text = "test query"
                Filters = Map.empty
                MaxResults = 5
                Timestamp = DateTime.Now
            }
            
            match! searchEngine.Search(query) with
            | Ok response ->
                Directory.Delete(testDir, true)
                return (true, $"✅ BasicSearchEngine: {response.Results.Length} results in {response.ProcessingTime.TotalMilliseconds:F1}ms")
            | Error err ->
                Directory.Delete(testDir, true)
                return (false, $"BasicSearchEngine failed: {err}")
        with
        | ex ->
            Directory.Delete(testDir, true)
            return (false, $"BasicSearchEngine exception: {ex.Message}")
    }
    
    let testBinarySearchEngine () = async {
        let! (testDir, dbPath, processor, embeddingService, vectorStore) = setupTestEnvironment()
        try
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Index some test documents
            let testDocs = TestData.createTestFiles testDir
            for doc in testDocs do
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> ()
                | Error err -> failwith $"Failed to index document: {err}"
            
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
                Directory.Delete(testDir, true)
                let hasResults = response.Results.Length > 0
                let allScoresNormalized = response.Results |> List.forall (fun r -> 
                    let (SearchResultScore score) = r.Score in score = 1.0)
                
                if hasResults && allScoresNormalized then
                    return (true, $"✅ BinarySearchEngine: {response.Results.Length} results, scores normalized")
                else
                    return (false, $"BinarySearchEngine issues: results={hasResults}, normalized={allScoresNormalized}")
            | Error err ->
                Directory.Delete(testDir, true)
                return (false, $"BinarySearchEngine search failed: {err}")
        with
        | ex ->
            Directory.Delete(testDir, true)
            return (false, $"BinarySearchEngine exception: {ex.Message}")
    }
    
    let testEnhancedSearchEngine () = async {
        let! (testDir, dbPath, processor, embeddingService, vectorStore) = setupTestEnvironment()
        try
            let contentLookup = ContentLookup(dbPath)
            let searchEngine = EnhancedSearchEngine(vectorStore, processor, embeddingService, contentLookup) :> ISearchEngine
            
            // Index some test documents
            let testDocs = TestData.createTestFiles testDir
            for doc in testDocs do
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> ()
                | Error err -> failwith $"Failed to index document: {err}"
            
            // Test search
            let query = {
                Id = QueryId (Guid.NewGuid())
                Text = "python programming"
                Filters = Map.empty
                MaxResults = 5
                Timestamp = DateTime.Now
            }
            
            match! searchEngine.Search(query) with
            | Ok response ->
                Directory.Delete(testDir, true)
                let hasResults = response.Results.Length > 0
                let hasContent = response.Results |> List.forall (fun r -> not (String.IsNullOrEmpty(r.ChunkContent)))
                
                if hasResults && hasContent then
                    return (true, $"✅ EnhancedSearchEngine: {response.Results.Length} results with content")
                else
                    return (false, $"EnhancedSearchEngine issues: results={hasResults}, content={hasContent}")
            | Error err ->
                Directory.Delete(testDir, true)
                return (false, $"EnhancedSearchEngine search failed: {err}")
        with
        | ex ->
            Directory.Delete(testDir, true)
            return (false, $"EnhancedSearchEngine exception: {ex.Message}")
    }
    
    let testSearchPerformance () = async {
        let! (testDir, dbPath, processor, embeddingService, vectorStore) = setupTestEnvironment()
        try
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Index test documents
            let testDocs = TestData.createTestFiles testDir
            for doc in testDocs do
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> ()
                | Error err -> failwith $"Failed to index document: {err}"
            
            // Test multiple queries and measure performance
            let queries = TestData.getTestQueries() |> List.map fst
            let mutable totalTime = TimeSpan.Zero
            let mutable successCount = 0
            
            for queryText in queries do
                let query = {
                    Id = QueryId (Guid.NewGuid())
                    Text = queryText
                    Filters = Map.empty
                    MaxResults = 5
                    Timestamp = DateTime.Now
                }
                
                let startTime = DateTime.Now
                match! searchEngine.Search(query) with
                | Ok response ->
                    totalTime <- totalTime + (DateTime.Now - startTime)
                    successCount <- successCount + 1
                | Error _ -> ()
            
            Directory.Delete(testDir, true)
            
            if successCount = queries.Length then
                let avgTime = totalTime.TotalMilliseconds / float successCount
                let isPerformant = avgTime < 50.0 // Should be under 50ms per query
                
                if isPerformant then
                    return (true, $"✅ Performance: {avgTime:F1}ms avg per query ({successCount} queries)")
                else
                    return (false, $"Performance issue: {avgTime:F1}ms avg per query (too slow)")
            else
                return (false, $"Performance test incomplete: {successCount}/{queries.Length} queries succeeded")
        with
        | ex ->
            Directory.Delete(testDir, true)
            return (false, $"Performance test exception: {ex.Message}")
    }
    
    let testSemanticAccuracy () = async {
        let! (testDir, dbPath, processor, embeddingService, vectorStore) = setupTestEnvironment()
        try
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Index test documents
            let testDocs = TestData.createTestFiles testDir
            for doc in testDocs do
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> ()
                | Error err -> failwith $"Failed to index document: {err}"
            
            // Test semantic accuracy with expected results
            let testQueries = TestData.getTestQueries()
            let mutable correctResults = 0
            let mutable totalQueries = 0
            let mutable messages = []
            
            for (queryText, expectedFiles) in testQueries do
                totalQueries <- totalQueries + 1
                let query = {
                    Id = QueryId (Guid.NewGuid())
                    Text = queryText
                    Filters = Map.empty
                    MaxResults = 10
                    Timestamp = DateTime.Now
                }
                
                match! searchEngine.Search(query) with
                | Ok response ->
                    let resultFiles = response.Results |> List.map (fun r -> 
                        let (DocumentPath path) = r.DocumentPath
                        Path.GetFileName(path))
                    
                    let hasExpectedResults = expectedFiles |> List.exists (fun expected -> 
                        resultFiles |> List.contains expected)
                    
                    if hasExpectedResults then
                        correctResults <- correctResults + 1
                        messages <- $"✅ '{queryText}': found expected results" :: messages
                    else
                        messages <- $"❌ '{queryText}': expected {expectedFiles}, got {resultFiles}" :: messages
                | Error err ->
                    messages <- $"❌ '{queryText}': search failed - {err}" :: messages
            
            Directory.Delete(testDir, true)
            
            let accuracy = float correctResults / float totalQueries
            let isAccurate = accuracy >= 0.7 // At least 70% accuracy
            
            let message = $"Accuracy: {correctResults}/{totalQueries} ({accuracy:P0})\n" + String.Join("\n", List.rev messages)
            
            return (isAccurate, message)
        with
        | ex ->
            Directory.Delete(testDir, true)
            return (false, $"Semantic accuracy test exception: {ex.Message}")
    }
    
    let runAllTests () = async {
        let tests = [
            ("Basic Search Engine", testBasicSearchEngine)
            ("Binary Search Engine", testBinarySearchEngine)
            ("Enhanced Search Engine", testEnhancedSearchEngine)
            ("Search Performance", testSearchPerformance)
            ("Semantic Accuracy", testSemanticAccuracy)
        ]
        
        let mutable results = []
        for (testName, testFunc) in tests do
            let! result = runTest testName testFunc
            results <- result :: results
            
        return List.rev results
    }
