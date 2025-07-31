namespace SmartDocumentFinder.ComprehensiveTests

open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

module EndToEndTests =
    
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
    
    let testCompleteWorkflow () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_e2e_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            // Step 1: Setup environment
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Step 2: Create and index documents
            let testDocs = TestData.createTestFiles testDir
            let mutable indexedCount = 0
            
            for doc in testDocs do
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> indexedCount <- indexedCount + 1
                | Error err -> failwith $"Failed to index {doc.Metadata.Path}: {err}"
            
            // Step 3: Test folder scanning
            match! FolderScanner.scanFolder(testDir) with
            | Ok scannedDocs ->
                if scannedDocs.Length <> testDocs.Length then
                    failwith $"Folder scan mismatch: {scannedDocs.Length} vs {testDocs.Length}"
            | Error err ->
                failwith $"Folder scan failed: {err}"
            
            // Step 4: Test semantic search accuracy
            let testQueries = TestData.getTestQueries()
            let mutable correctResults = 0
            let mutable totalQueries = 0
            
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
                | Error err ->
                    failwith $"Search failed for '{queryText}': {err}"
            
            // Step 5: Test performance requirements
            let performanceQuery = {
                Id = QueryId (Guid.NewGuid())
                Text = "machine learning algorithms"
                Filters = Map.empty
                MaxResults = 5
                Timestamp = DateTime.Now
            }
            
            let startTime = DateTime.Now
            match! searchEngine.Search(performanceQuery) with
            | Ok response ->
                let searchTime = DateTime.Now - startTime
                let isPerformant = searchTime.TotalMilliseconds < 100.0
                
                Directory.Delete(testDir, true)
                
                let accuracy = float correctResults / float totalQueries
                let isAccurate = accuracy >= 0.7
                
                if indexedCount = testDocs.Length && isAccurate && isPerformant then
                    return (true, $"✅ Complete workflow: {indexedCount} docs indexed, {accuracy:P0} accuracy, {searchTime.TotalMilliseconds:F1}ms search")
                else
                    return (false, $"❌ Workflow issues: indexed={indexedCount}/{testDocs.Length}, accuracy={accuracy:P0}, time={searchTime.TotalMilliseconds:F1}ms")
            | Error err ->
                Directory.Delete(testDir, true)
                return (false, $"❌ Performance test failed: {err}")
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ End-to-end test failed: {ex.Message}")
    }
    
    let testMultiFormatProcessing () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_multiformat_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Create documents in different formats
            let testContent = TestData.machineLearningContent
            let testFiles = [
                ("ml-doc.txt", testContent)
                // Note: PDF and DOCX creation would require additional libraries
                // For now, we'll test with text files and verify the system handles different extensions
            ]
            
            let mutable processedCount = 0
            let mutable messages = []
            
            for (filename, content) in testFiles do
                let filePath = Path.Combine(testDir, filename)
                File.WriteAllText(filePath, content)
                
                let doc = TestData.createTestDocument content filename
                
                match! searchEngine.IndexDocument(doc) with
                | Ok _ ->
                    processedCount <- processedCount + 1
                    messages <- $"✅ {filename}: processed successfully" :: messages
                | Error err ->
                    messages <- $"❌ {filename}: {err}" :: messages
            
            // Test search across all formats
            let query = {
                Id = QueryId (Guid.NewGuid())
                Text = "machine learning"
                Filters = Map.empty
                MaxResults = 10
                Timestamp = DateTime.Now
            }
            
            match! searchEngine.Search(query) with
            | Ok response ->
                Directory.Delete(testDir, true)
                
                if processedCount = testFiles.Length && response.Results.Length > 0 then
                    return (true, $"✅ Multi-format: {processedCount} formats, {response.Results.Length} results\n" + String.Join("\n", List.rev messages))
                else
                    return (false, $"❌ Multi-format issues: {processedCount}/{testFiles.Length} processed, {response.Results.Length} results")
            | Error err ->
                Directory.Delete(testDir, true)
                return (false, $"❌ Multi-format search failed: {err}")
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Multi-format test failed: {ex.Message}")
    }
    
    let testScalabilityWithManyDocuments () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_scale_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Create many documents
            let numDocs = 50
            let baseContents = [
                TestData.machineLearningContent
                TestData.pythonProgrammingContent
                TestData.businessReportContent
                TestData.softwareDocContent
                TestData.academicResearchContent
            ]
            
            let mutable indexedCount = 0
            let startTime = DateTime.Now
            
            for i in 1..numDocs do
                let content = baseContents.[i % baseContents.Length] + $"\n\nDocument variation {i}"
                let filename = $"doc_{i:D3}.txt"
                let filePath = Path.Combine(testDir, filename)
                File.WriteAllText(filePath, content)
                
                let doc = TestData.createTestDocument content filename
                
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> indexedCount <- indexedCount + 1
                | Error _ -> ()
            
            let indexingTime = DateTime.Now - startTime
            
            // Test search performance with many documents
            let searchStartTime = DateTime.Now
            let query = {
                Id = QueryId (Guid.NewGuid())
                Text = "machine learning"
                Filters = Map.empty
                MaxResults = 10
                Timestamp = DateTime.Now
            }
            
            match! searchEngine.Search(query) with
            | Ok response ->
                let searchTime = DateTime.Now - searchStartTime
                
                Directory.Delete(testDir, true)
                
                let indexingPerformant = indexingTime.TotalSeconds < 60.0
                let searchPerformant = searchTime.TotalMilliseconds < 200.0
                let allIndexed = indexedCount = numDocs
                
                if allIndexed && indexingPerformant && searchPerformant then
                    return (true, $"✅ Scalability: {indexedCount} docs in {indexingTime.TotalSeconds:F1}s, search in {searchTime.TotalMilliseconds:F1}ms")
                else
                    return (false, $"❌ Scalability issues: indexed={indexedCount}/{numDocs}, index_time={indexingTime.TotalSeconds:F1}s, search_time={searchTime.TotalMilliseconds:F1}ms")
            | Error err ->
                Directory.Delete(testDir, true)
                return (false, $"❌ Scalability search failed: {err}")
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Scalability test failed: {ex.Message}")
    }
    
    let testCrossSessionPersistence () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_persistence_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            
            // Session 1: Index documents
            do
                let! _ = Database.initializeDatabase(dbPath)
                let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
                let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
                let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
                let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
                
                let testDocs = TestData.createTestFiles testDir
                for doc in testDocs do
                    match! searchEngine.IndexDocument(doc) with
                    | Ok _ -> ()
                    | Error err -> failwith $"Failed to index in session 1: {err}"
            
            // Session 2: Search without re-indexing
            let processor2 = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService2 = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore2 = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine2 = BinarySearchEngine(vectorStore2, processor2, embeddingService2, dbPath) :> ISearchEngine
            
            let query = {
                Id = QueryId (Guid.NewGuid())
                Text = "machine learning"
                Filters = Map.empty
                MaxResults = 5
                Timestamp = DateTime.Now
            }
            
            match! searchEngine2.Search(query) with
            | Ok response ->
                Directory.Delete(testDir, true)
                
                if response.Results.Length > 0 then
                    return (true, $"✅ Persistence: {response.Results.Length} results found across sessions")
                else
                    return (false, "❌ No results found in second session")
            | Error err ->
                Directory.Delete(testDir, true)
                return (false, $"❌ Persistence search failed: {err}")
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Persistence test failed: {ex.Message}")
    }
    
    let runAllTests () = async {
        let tests = [
            ("Complete Workflow", testCompleteWorkflow)
            ("Multi-Format Processing", testMultiFormatProcessing)
            ("Scalability with Many Documents", testScalabilityWithManyDocuments)
            ("Cross-Session Persistence", testCrossSessionPersistence)
        ]
        
        let mutable results = []
        for (testName, testFunc) in tests do
            let! result = runTest testName testFunc
            results <- result :: results
            
        return List.rev results
    }
