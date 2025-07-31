open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

type TestResult = {
    TestName: string
    Success: bool
    Message: string
    Duration: TimeSpan
}

let runTest testName testFunc = async {
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

let printResult result =
    let status = if result.Success then "✅ PASS" else "❌ FAIL"
    printfn "%s %-50s (%6.1fms)" status result.TestName result.Duration.TotalMilliseconds
    if not result.Success then
        printfn "    %s" result.Message

// Test 1: PDF Processing Issue Investigation
let testPdfProcessing () = async {
    let testDir = Path.Combine(Path.GetTempPath(), "sdf_pdf_test_" + Guid.NewGuid().ToString("N")[..7])
    try
        Directory.CreateDirectory(testDir) |> ignore
        
        // Create a simple PDF-like file to test error handling
        let fakePdfPath = Path.Combine(testDir, "test.pdf")
        File.WriteAllText(fakePdfPath, "%PDF-1.4\nHello World")
        
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let docPath = DocumentPath fakePdfPath
        
        match! processor.ExtractText(docPath) with
        | Ok text ->
            Directory.Delete(testDir, true)
            return (true, $"PDF processing works: extracted {text.Length} chars")
        | Error (ProcessingError msg) ->
            Directory.Delete(testDir, true)
            return (true, $"PDF error handling works: {msg}")
        | Error err ->
            Directory.Delete(testDir, true)
            return (false, $"Unexpected error type: {err}")
    with
    | ex ->
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        return (false, $"Test failed: {ex.Message}")
}

// Test 2: Search Engine Comparison
let testSearchEngineComparison () = async {
    let testDir = Path.Combine(Path.GetTempPath(), "sdf_comparison_test_" + Guid.NewGuid().ToString("N")[..7])
    let dbPath = Path.Combine(testDir, "test.db")
    
    try
        Directory.CreateDirectory(testDir) |> ignore
        let! _ = Database.initializeDatabase(dbPath)
        
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
        
        // Test different search engines
        let basicEngine = BasicSearchEngine(vectorStore, processor) :> ISearchEngine
        let binaryEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
        
        let query = {
            Id = QueryId (Guid.NewGuid())
            Text = "test query"
            Filters = Map.empty
            MaxResults = 5
            Timestamp = DateTime.Now
        }
        
        let! basicResult = basicEngine.Search(query)
        let! binaryResult = binaryEngine.Search(query)
        
        Directory.Delete(testDir, true)
        
        match basicResult, binaryResult with
        | Ok basicResp, Ok binaryResp ->
            return (true, $"Both engines work: Basic={basicResp.Results.Length}, Binary={binaryResp.Results.Length}")
        | Error basicErr, Ok binaryResp ->
            return (true, $"Binary engine works, Basic failed: {binaryResp.Results.Length} results")
        | Ok basicResp, Error binaryErr ->
            return (false, $"Basic works but Binary failed: {binaryErr}")
        | Error basicErr, Error binaryErr ->
            return (false, $"Both engines failed: Basic={basicErr}, Binary={binaryErr}")
    with
    | ex ->
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        return (false, $"Test setup failed: {ex.Message}")
}

// Test 3: Cross-Platform Functionality
let testCrossPlatformFeatures () = async {
    let platformInfo = CrossPlatform.getPlatformInfo()
    let dbPath = CrossPlatform.getDefaultDatabasePath()
    let testDbPath = CrossPlatform.getTestDatabasePath()
    
    let testDir = Path.Combine(Path.GetTempPath(), "sdf_platform_test_" + Guid.NewGuid().ToString("N")[..7])
    let testFile = Path.Combine(testDir, "test.db")
    
    try
        CrossPlatform.ensureDirectoryExists(testFile)
        let dirExists = Directory.Exists(Path.GetDirectoryName(testFile))
        
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        
        let hasValidPaths = dbPath.Contains(".smartdoc") && testDbPath.Contains("test.db")
        
        if dirExists && hasValidPaths then
            return (true, $"Cross-platform OK: {platformInfo}, paths valid")
        else
            return (false, $"Cross-platform issues: dir={dirExists}, paths={hasValidPaths}")
    with
    | ex ->
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        return (false, $"Cross-platform test failed: {ex.Message}")
}

// Test 4: Semantic Search Accuracy
let testSemanticAccuracy () = async {
    let testDir = Path.Combine(Path.GetTempPath(), "sdf_semantic_test_" + Guid.NewGuid().ToString("N")[..7])
    let dbPath = Path.Combine(testDir, "test.db")
    
    try
        Directory.CreateDirectory(testDir) |> ignore
        let! _ = Database.initializeDatabase(dbPath)
        
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
        let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
        
        // Create test documents with known content
        let mlContent = "Machine learning and artificial intelligence algorithms for neural networks"
        let pythonContent = "Python programming tutorial with code examples and functions"
        let businessContent = "Financial business report with revenue and profit analysis"
        
        let testDocs = [
            ("ml.txt", mlContent)
            ("python.txt", pythonContent)
            ("business.txt", businessContent)
        ]
        
        // Index documents
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
            | Ok _ -> ()
            | Error err -> failwith $"Failed to index {filename}: {err}"
        
        // Test semantic queries
        let testQueries = [
            ("machine learning", "ml.txt")
            ("python programming", "python.txt")
            ("financial report", "business.txt")
        ]
        
        let mutable correctResults = 0
        let mutable totalQueries = 0
        
        for (queryText, expectedFile) in testQueries do
            totalQueries <- totalQueries + 1
            let query = {
                Id = QueryId (Guid.NewGuid())
                Text = queryText
                Filters = Map.empty
                MaxResults = 5
                Timestamp = DateTime.Now
            }
            
            match! searchEngine.Search(query) with
            | Ok response ->
                let hasExpectedResult = response.Results |> List.exists (fun r ->
                    let (DocumentPath path) = r.DocumentPath
                    Path.GetFileName(path) = expectedFile)
                
                if hasExpectedResult then
                    correctResults <- correctResults + 1
            | Error _ -> ()
        
        Directory.Delete(testDir, true)
        
        let accuracy = float correctResults / float totalQueries
        if accuracy >= 0.6 then
            return (true, $"Semantic accuracy: {correctResults}/{totalQueries} ({accuracy:P0})")
        else
            return (false, $"Poor semantic accuracy: {correctResults}/{totalQueries} ({accuracy:P0})")
    with
    | ex ->
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        return (false, $"Semantic test failed: {ex.Message}")
}

// Test 5: Performance Validation
let testPerformanceRequirements () = async {
    let testDir = Path.Combine(Path.GetTempPath(), "sdf_perf_test_" + Guid.NewGuid().ToString("N")[..7])
    let dbPath = Path.Combine(testDir, "test.db")
    
    try
        Directory.CreateDirectory(testDir) |> ignore
        let! _ = Database.initializeDatabase(dbPath)
        
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
        let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
        
        // Index a test document
        let testContent = "This is a test document for performance testing with machine learning content"
        let testFile = Path.Combine(testDir, "test.txt")
        File.WriteAllText(testFile, testContent)
        
        let doc = {
            Metadata = {
                Id = DocumentId (Guid.NewGuid())
                Path = DocumentPath testFile
                FileName = "test.txt"
                Size = int64 testContent.Length
                Created = DateTime.Now
                Modified = DateTime.Now
                Format = PlainText
                Hash = DocumentHash (testContent.GetHashCode().ToString())
            }
            State = NotProcessed
        }
        
        match! searchEngine.IndexDocument(doc) with
        | Ok _ ->
            // Test search performance
            let query = {
                Id = QueryId (Guid.NewGuid())
                Text = "machine learning"
                Filters = Map.empty
                MaxResults = 5
                Timestamp = DateTime.Now
            }
            
            let startTime = DateTime.Now
            match! searchEngine.Search(query) with
            | Ok response ->
                let searchTime = DateTime.Now - startTime
                Directory.Delete(testDir, true)
                
                let isPerformant = searchTime.TotalMilliseconds < 100.0
                if isPerformant then
                    return (true, $"Performance OK: {searchTime.TotalMilliseconds:F1}ms search time")
                else
                    return (false, $"Performance issue: {searchTime.TotalMilliseconds:F1}ms (too slow)")
            | Error err ->
                Directory.Delete(testDir, true)
                return (false, $"Performance test search failed: {err}")
        | Error err ->
            Directory.Delete(testDir, true)
            return (false, $"Performance test indexing failed: {err}")
    with
    | ex ->
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        return (false, $"Performance test failed: {ex.Message}")
}

[<EntryPoint>]
let main argv =
    printfn "╔═══════════════════════════════════════════════════════════════╗"
    printfn "║           SMART DOCUMENT FINDER - FEATURE VALIDATION         ║"
    printfn "╚═══════════════════════════════════════════════════════════════╝"
    printfn ""
    printfn "Testing key features mentioned in documentation..."
    printfn "Platform: %s" (CrossPlatform.getPlatformInfo())
    printfn ""
    
    let runAllTests () = async {
        let tests = [
            ("PDF Processing Investigation", testPdfProcessing)
            ("Search Engine Comparison", testSearchEngineComparison)
            ("Cross-Platform Features", testCrossPlatformFeatures)
            ("Semantic Search Accuracy", testSemanticAccuracy)
            ("Performance Requirements", testPerformanceRequirements)
        ]

        // Also run missing feature tests
        printfn ""
        printfn "Testing for missing/incomplete features..."
        let! missingFeatureResults = MissingFeatureTests.runMissingFeatureTests()

        printfn ""
        printfn "Missing Feature Analysis:"
        for (testName, success, message, duration) in missingFeatureResults do
            let status = if success then "✅ AVAILABLE" else "❌ MISSING"
            printfn "%s %-40s (%6.1fms)" status testName duration.TotalMilliseconds
            printfn "    %s" message
        
        let mutable results = []
        for (testName, testFunc) in tests do
            let! result = runTest testName testFunc
            results <- result :: results
            printResult result
        
        let results = List.rev results
        let totalTests = results.Length
        let passedTests = results |> List.filter (fun r -> r.Success) |> List.length
        let failedTests = totalTests - passedTests
        
        printfn ""
        printfn "═══════════════════════════════════════════════════════════════"
        printfn "SUMMARY: %d/%d tests passed (%.0f%%)" passedTests totalTests (float passedTests / float totalTests * 100.0)
        
        if failedTests > 0 then
            printfn ""
            printfn "Failed tests:"
            for result in results do
                if not result.Success then
                    printfn "  ❌ %s: %s" result.TestName result.Message
        
        return if failedTests = 0 then 0 else 1
    }

    let testLithuanianSupport () = async {
        printfn ""
        printfn "🇱🇹 TESTING LITHUANIAN LANGUAGE SUPPORT"
        printfn "========================================"

        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService

        printfn "Testing 'dokumentinis filmas' (documentary film):"
        match! embeddingService.GenerateEmbedding("dokumentinis filmas") with
        | Ok docVector ->
            let relatedTerms = ["documentary"; "film"; "movie"; "video"]
            let mutable maxSimilarity = 0.0

            for term in relatedTerms do
                match! embeddingService.GenerateEmbedding(term) with
                | Ok termVector ->
                    let similarity = VectorOperations.cosineSimilarity docVector termVector
                    maxSimilarity <- max maxSimilarity similarity
                    let status = if similarity > 0.45 then "✅ RELEVANT" else "❌ IRRELEVANT"
                    printfn "  %s %s: %.3f" status term similarity
                | Error _ ->
                    printfn "  ❌ %s: embedding failed" term

            if maxSimilarity > 0.45 then
                printfn "✅ Lithuanian support: 'dokumentinis filmas' should now work!"
            else
                printfn "❌ Lithuanian support: Still needs improvement (max similarity: %.3f)" maxSimilarity
        | Error err ->
            printfn "❌ Failed to generate Lithuanian embedding: %A" err
    }

    let runEverything () = async {
        let! mainResult = runAllTests ()
        let! _ = testLithuanianSupport ()
        return mainResult
    }

    try
        runEverything () |> Async.RunSynchronously
    with
    | ex ->
        printfn ""
        printfn "❌ Test execution failed: %s" ex.Message
        1
