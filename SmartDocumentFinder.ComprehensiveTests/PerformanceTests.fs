namespace SmartDocumentFinder.ComprehensiveTests

open System
open System.IO
open System.Diagnostics
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

module PerformanceTests =
    
    type TestResult = {
        TestName: string
        Success: bool
        Message: string
        Duration: TimeSpan
    }
    
    type PerformanceMetrics = {
        OperationsPerSecond: float
        AverageLatency: TimeSpan
        MemoryUsage: int64
        TotalOperations: int
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
    
    let measureMemory () =
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
        GC.GetTotalMemory(false)
    
    let testSearchPerformance () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_perf_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Index test documents
            let testDocs = TestData.createTestFiles testDir
            for doc in testDocs do
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> ()
                | Error err -> failwith $"Failed to index: {err}"
            
            // Performance test parameters
            let numQueries = 100
            let queries = [
                "machine learning algorithms"
                "python programming tutorial"
                "financial business report"
                "software development documentation"
                "academic research methodology"
            ]
            
            let startMemory = measureMemory()
            let stopwatch = Stopwatch.StartNew()
            
            let mutable successfulQueries = 0
            let mutable totalLatency = TimeSpan.Zero
            
            for i in 1..numQueries do
                let queryText = queries.[i % queries.Length]
                let query = {
                    Id = QueryId (Guid.NewGuid())
                    Text = queryText
                    Filters = Map.empty
                    MaxResults = 5
                    Timestamp = DateTime.Now
                }
                
                let queryStart = DateTime.Now
                match! searchEngine.Search(query) with
                | Ok _ ->
                    let queryLatency = DateTime.Now - queryStart
                    totalLatency <- totalLatency + queryLatency
                    successfulQueries <- successfulQueries + 1
                | Error _ -> ()
            
            stopwatch.Stop()
            let endMemory = measureMemory()
            
            Directory.Delete(testDir, true)
            
            if successfulQueries = numQueries then
                let avgLatency = totalLatency.TotalMilliseconds / float successfulQueries
                let queriesPerSecond = float successfulQueries / stopwatch.Elapsed.TotalSeconds
                let memoryIncrease = endMemory - startMemory
                
                let isPerformant = avgLatency < 50.0 && queriesPerSecond > 10.0
                
                if isPerformant then
                    return (true, $"✅ Search: {avgLatency:F1}ms avg, {queriesPerSecond:F1} q/s, {memoryIncrease / 1024L}KB mem")
                else
                    return (false, $"❌ Performance: {avgLatency:F1}ms avg, {queriesPerSecond:F1} q/s (too slow)")
            else
                return (false, $"❌ Only {successfulQueries}/{numQueries} queries succeeded")
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Performance test exception: {ex.Message}")
    }
    
    let testIndexingPerformance () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_index_perf_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Create multiple test documents
            let numDocs = 20
            let testContents = [
                TestData.machineLearningContent
                TestData.pythonProgrammingContent
                TestData.businessReportContent
                TestData.softwareDocContent
                TestData.academicResearchContent
            ]
            
            let testDocs = [
                for i in 1..numDocs do
                    let content = testContents.[i % testContents.Length]
                    let filename = $"test_doc_{i}.txt"
                    let fullPath = Path.Combine(testDir, filename)
                    File.WriteAllText(fullPath, content)
                    yield TestData.createTestDocument content filename
            ]
            
            let startMemory = measureMemory()
            let stopwatch = Stopwatch.StartNew()
            
            let mutable successfulIndexes = 0
            
            for doc in testDocs do
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> successfulIndexes <- successfulIndexes + 1
                | Error _ -> ()
            
            stopwatch.Stop()
            let endMemory = measureMemory()
            
            Directory.Delete(testDir, true)
            
            if successfulIndexes = numDocs then
                let avgIndexTime = stopwatch.Elapsed.TotalMilliseconds / float successfulIndexes
                let docsPerSecond = float successfulIndexes / stopwatch.Elapsed.TotalSeconds
                let memoryIncrease = endMemory - startMemory
                
                let isPerformant = avgIndexTime < 1000.0 && docsPerSecond > 1.0
                
                if isPerformant then
                    return (true, $"✅ Indexing: {avgIndexTime:F1}ms avg, {docsPerSecond:F1} docs/s, {memoryIncrease / 1024L}KB mem")
                else
                    return (false, $"❌ Indexing performance: {avgIndexTime:F1}ms avg, {docsPerSecond:F1} docs/s (too slow)")
            else
                return (false, $"❌ Only {successfulIndexes}/{numDocs} documents indexed successfully")
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Indexing performance test exception: {ex.Message}")
    }
    
    let testVectorOperationsPerformance () = async {
        let vectorSize = 384
        let numVectors = 1000
        let numQueries = 100
        
        // Generate test data
        let random = Random(42)
        let queryVector = EmbeddingVector (Array.init vectorSize (fun _ -> float32 (random.NextDouble() * 2.0 - 1.0)))
        let testVectors = [
            for i in 1..numVectors do
                let id = ChunkId (Guid.NewGuid())
                let vector = EmbeddingVector (Array.init vectorSize (fun _ -> float32 (random.NextDouble() * 2.0 - 1.0)))
                yield (id, vector)
        ]
        
        let startMemory = measureMemory()
        let stopwatch = Stopwatch.StartNew()
        
        // Test similarity calculations
        for i in 1..numQueries do
            let results = VectorOperations.findTopSimilar queryVector testVectors 10
            if results.Length <> 10 then
                failwith "Unexpected result count"
        
        stopwatch.Stop()
        let endMemory = measureMemory()
        
        let avgLatency = stopwatch.Elapsed.TotalMilliseconds / float numQueries
        let operationsPerSecond = float numQueries / stopwatch.Elapsed.TotalSeconds
        let memoryIncrease = endMemory - startMemory
        
        let isPerformant = avgLatency < 10.0 && operationsPerSecond > 50.0
        
        if isPerformant then
            return (true, $"✅ Vector ops: {avgLatency:F1}ms avg, {operationsPerSecond:F0} ops/s, {memoryIncrease / 1024L}KB mem")
        else
            return (false, $"❌ Vector performance: {avgLatency:F1}ms avg, {operationsPerSecond:F0} ops/s (too slow)")
    }
    
    let testMemoryUsage () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_memory_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            let initialMemory = measureMemory()
            
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            let afterInitMemory = measureMemory()
            
            // Index documents and measure memory growth
            let testDocs = TestData.createTestFiles testDir
            for doc in testDocs do
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> ()
                | Error err -> failwith $"Failed to index: {err}"
            
            let afterIndexingMemory = measureMemory()
            
            // Perform searches and measure memory
            let queries = TestData.getTestQueries() |> List.map fst
            for queryText in queries do
                let query = {
                    Id = QueryId (Guid.NewGuid())
                    Text = queryText
                    Filters = Map.empty
                    MaxResults = 5
                    Timestamp = DateTime.Now
                }
                match! searchEngine.Search(query) with
                | Ok _ -> ()
                | Error _ -> ()
            
            let finalMemory = measureMemory()
            
            Directory.Delete(testDir, true)
            
            let initIncrease = afterInitMemory - initialMemory
            let indexIncrease = afterIndexingMemory - afterInitMemory
            let searchIncrease = finalMemory - afterIndexingMemory
            
            let totalIncrease = finalMemory - initialMemory
            let isReasonable = totalIncrease < 50L * 1024L * 1024L // Less than 50MB
            
            if isReasonable then
                return (true, $"✅ Memory: init +{initIncrease/1024L}KB, index +{indexIncrease/1024L}KB, search +{searchIncrease/1024L}KB")
            else
                return (false, $"❌ Memory usage too high: {totalIncrease/1024L/1024L}MB total")
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Memory test exception: {ex.Message}")
    }
    
    let runAllTests () = async {
        let tests = [
            ("Search Performance", testSearchPerformance)
            ("Indexing Performance", testIndexingPerformance)
            ("Vector Operations Performance", testVectorOperationsPerformance)
            ("Memory Usage", testMemoryUsage)
        ]
        
        let mutable results = []
        for (testName, testFunc) in tests do
            let! result = runTest testName testFunc
            results <- result :: results
            
        return List.rev results
    }
