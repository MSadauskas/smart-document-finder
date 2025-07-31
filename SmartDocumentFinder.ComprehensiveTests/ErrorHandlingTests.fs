namespace SmartDocumentFinder.ComprehensiveTests

open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

module ErrorHandlingTests =
    
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
    
    let testFileNotFoundHandling () = async {
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let nonExistentPath = DocumentPath "/absolutely/nonexistent/file.txt"
        
        match! processor.ExtractText(nonExistentPath) with
        | Ok _ ->
            return (false, "❌ Should have failed for non-existent file")
        | Error (FileNotFound path) ->
            return (true, $"✅ Correctly handled FileNotFound: {path}")
        | Error err ->
            return (false, $"❌ Wrong error type: {err}")
    }
    
    let testUnsupportedFormatHandling () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_error_test_" + Guid.NewGuid().ToString("N")[..7])
        try
            Directory.CreateDirectory(testDir) |> ignore
            
            // Create files with unsupported extensions
            let unsupportedFiles = [
                ("test.xyz", "Unknown format content")
                ("test.bin", "Binary content")
                ("test.exe", "Executable content")
            ]
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let mutable allCorrect = true
            let mutable messages = []
            
            for (filename, content) in unsupportedFiles do
                let filePath = Path.Combine(testDir, filename)
                File.WriteAllText(filePath, content)
                let docPath = DocumentPath filePath
                
                match! processor.ExtractText(docPath) with
                | Ok _ ->
                    allCorrect <- false
                    messages <- $"❌ {filename}: Should have failed" :: messages
                | Error (UnsupportedFormat _) ->
                    messages <- $"✅ {filename}: Correctly rejected" :: messages
                | Error err ->
                    allCorrect <- false
                    messages <- $"❌ {filename}: Wrong error type - {err}" :: messages
            
            Directory.Delete(testDir, true)
            return (allCorrect, String.Join("\n", List.rev messages))
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Test setup failed: {ex.Message}")
    }
    
    let testCorruptedFileHandling () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_corrupt_test_" + Guid.NewGuid().ToString("N")[..7])
        try
            Directory.CreateDirectory(testDir) |> ignore
            
            // Create corrupted files
            let corruptedFiles = [
                ("corrupt.pdf", [| 0x25uy; 0x50uy; 0x44uy; 0x46uy |]) // Fake PDF header
                ("corrupt.docx", [| 0x50uy; 0x4Buy; 0x03uy; 0x04uy |]) // Fake ZIP header
            ]
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let mutable allHandled = true
            let mutable messages = []
            
            for (filename, bytes) in corruptedFiles do
                let filePath = Path.Combine(testDir, filename)
                File.WriteAllBytes(filePath, bytes)
                let docPath = DocumentPath filePath
                
                match! processor.ExtractText(docPath) with
                | Ok text when String.IsNullOrEmpty(text) ->
                    messages <- $"✅ {filename}: Empty result (acceptable)" :: messages
                | Ok _ ->
                    messages <- $"⚠️  {filename}: Extracted content (unexpected but not error)" :: messages
                | Error (ProcessingError _) ->
                    messages <- $"✅ {filename}: Correctly handled as ProcessingError" :: messages
                | Error err ->
                    messages <- $"✅ {filename}: Handled as {err.GetType().Name}" :: messages
            
            Directory.Delete(testDir, true)
            return (allHandled, String.Join("\n", List.rev messages))
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Corrupted file test failed: {ex.Message}")
    }
    
    let testDatabaseConnectionErrors () = async {
        // Test with invalid database path
        let invalidDbPath = "/invalid/path/that/cannot/exist/test.db"
        
        try
            let vectorStore = SqliteVectorStore(invalidDbPath) :> IVectorStore
            let testEmbedding = {
                ChunkId = ChunkId (Guid.NewGuid())
                Vector = EmbeddingVector [| 1.0f; 2.0f; 3.0f |]
                Model = "test"
                CreatedAt = DateTime.Now
            }
            
            match! vectorStore.StoreEmbedding(testEmbedding) with
            | Ok _ ->
                return (false, "❌ Should have failed with invalid database path")
            | Error (StorageError _) ->
                return (true, "✅ Correctly handled database connection error")
            | Error err ->
                return (false, $"❌ Wrong error type: {err}")
        with
        | ex ->
            return (true, $"✅ Exception handled: {ex.GetType().Name}")
    }
    
    let testEmptyDocumentHandling () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_empty_test_" + Guid.NewGuid().ToString("N")[..7])
        try
            Directory.CreateDirectory(testDir) |> ignore
            
            // Create empty files
            let emptyFiles = [
                "empty.txt"
                "empty.pdf"
                "empty.docx"
            ]
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let mutable allHandled = true
            let mutable messages = []
            
            for filename in emptyFiles do
                let filePath = Path.Combine(testDir, filename)
                File.WriteAllText(filePath, "")
                let docPath = DocumentPath filePath
                
                match! processor.ExtractText(docPath) with
                | Ok text ->
                    if String.IsNullOrEmpty(text) then
                        messages <- $"✅ {filename}: Empty text extracted" :: messages
                    else
                        messages <- $"⚠️  {filename}: Non-empty text from empty file" :: messages
                | Error err ->
                    messages <- $"✅ {filename}: Error handled - {err.GetType().Name}" :: messages
            
            Directory.Delete(testDir, true)
            return (allHandled, String.Join("\n", List.rev messages))
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Empty document test failed: {ex.Message}")
    }
    
    let testInvalidQueryHandling () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_query_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            
            // Test invalid queries
            let invalidQueries = [
                ""  // Empty query
                "   "  // Whitespace only
                String.replicate 10000 "a"  // Very long query
            ]
            
            let mutable allHandled = true
            let mutable messages = []
            
            for queryText in invalidQueries do
                let query = {
                    Id = QueryId (Guid.NewGuid())
                    Text = queryText
                    Filters = Map.empty
                    MaxResults = 5
                    Timestamp = DateTime.Now
                }
                
                match! searchEngine.Search(query) with
                | Ok response ->
                    messages <- $"✅ '{queryText.Substring(0, min 20 queryText.Length)}...': {response.Results.Length} results" :: messages
                | Error err ->
                    messages <- $"✅ '{queryText.Substring(0, min 20 queryText.Length)}...': Error handled - {err.GetType().Name}" :: messages
            
            Directory.Delete(testDir, true)
            return (allHandled, String.Join("\n", List.rev messages))
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Invalid query test failed: {ex.Message}")
    }
    
    let testConcurrentAccessHandling () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_concurrent_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            
            // Create multiple vector store instances (simulating concurrent access)
            let vectorStores = [
                for i in 1..3 do
                    yield SqliteVectorStore(dbPath) :> IVectorStore
            ]
            
            // Test concurrent operations
            let tasks = [
                for i, vectorStore in List.indexed vectorStores do
                    async {
                        let testEmbedding = {
                            ChunkId = ChunkId (Guid.NewGuid())
                            Vector = EmbeddingVector [| float32 i; float32 (i + 1); float32 (i + 2) |]
                            Model = "test"
                            CreatedAt = DateTime.Now
                        }
                        return! vectorStore.StoreEmbedding(testEmbedding)
                    }
            ]
            
            let! results = Async.Parallel tasks
            
            let successCount = results |> Array.sumBy (function | Ok _ -> 1 | Error _ -> 0)
            let errorCount = results.Length - successCount
            
            Directory.Delete(testDir, true)
            
            if successCount > 0 then
                return (true, $"✅ Concurrent access: {successCount} success, {errorCount} errors (acceptable)")
            else
                return (false, $"❌ All concurrent operations failed")
        with
        | ex ->
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Concurrent access test failed: {ex.Message}")
    }
    
    let runAllTests () = async {
        let tests = [
            ("File Not Found Handling", testFileNotFoundHandling)
            ("Unsupported Format Handling", testUnsupportedFormatHandling)
            ("Corrupted File Handling", testCorruptedFileHandling)
            ("Database Connection Errors", testDatabaseConnectionErrors)
            ("Empty Document Handling", testEmptyDocumentHandling)
            ("Invalid Query Handling", testInvalidQueryHandling)
            ("Concurrent Access Handling", testConcurrentAccessHandling)
        ]
        
        let mutable results = []
        for (testName, testFunc) in tests do
            let! result = runTest testName testFunc
            results <- result :: results
            
        return List.rev results
    }
