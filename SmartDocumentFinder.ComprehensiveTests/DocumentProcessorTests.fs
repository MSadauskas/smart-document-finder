namespace SmartDocumentFinder.ComprehensiveTests

open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor

module DocumentProcessorTests =
    
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
    
    let testTextExtraction () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_test_" + Guid.NewGuid().ToString("N")[..7])
        try
            let testDocs = TestData.createTestFiles testDir
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            
            let mutable allSuccess = true
            let mutable messages = []
            
            for doc in testDocs do
                let (DocumentPath filePath) = doc.Metadata.Path
                let fullPath = Path.Combine(testDir, Path.GetFileName(filePath))
                let testPath = DocumentPath fullPath
                
                match! processor.ExtractText(testPath) with
                | Ok text ->
                    if String.IsNullOrWhiteSpace(text) then
                        allSuccess <- false
                        messages <- $"Empty text extracted from {filePath}" :: messages
                    else
                        messages <- $"✅ Extracted {text.Length} chars from {filePath}" :: messages
                | Error err ->
                    allSuccess <- false
                    messages <- $"❌ Failed to extract from {filePath}: {err}" :: messages
            
            TestData.cleanupTestFiles testDir
            return (allSuccess, String.Join("\n", List.rev messages))
        with
        | ex ->
            TestData.cleanupTestFiles testDir
            return (false, $"Test setup failed: {ex.Message}")
    }
    
    let testDocumentProcessing () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_test_" + Guid.NewGuid().ToString("N")[..7])
        try
            let testDocs = TestData.createTestFiles testDir
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            
            let mutable allSuccess = true
            let mutable messages = []
            
            for doc in testDocs do
                match! processor.ProcessDocument(doc) with
                | Ok chunks ->
                    if chunks.IsEmpty then
                        allSuccess <- false
                        messages <- $"No chunks generated for {doc.Metadata.Path}" :: messages
                    else
                        messages <- $"✅ Generated {chunks.Length} chunks for {doc.Metadata.Path}" :: messages
                | Error err ->
                    allSuccess <- false
                    messages <- $"❌ Failed to process {doc.Metadata.Path}: {err}" :: messages
            
            TestData.cleanupTestFiles testDir
            return (allSuccess, String.Join("\n", List.rev messages))
        with
        | ex ->
            TestData.cleanupTestFiles testDir
            return (false, $"Test setup failed: {ex.Message}")
    }
    
    let testChunkingBehavior () = async {
        let testContent = String.replicate 1000 "This is a test sentence. "
        let testDoc = TestData.createTestDocument testContent "large-test.txt"
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        
        match! processor.ProcessDocument(testDoc) with
        | Ok chunks ->
            let totalChars = chunks |> List.sumBy (fun chunk -> chunk.Content.Length)
            let hasOverlap = chunks.Length > 1
            
            if chunks.IsEmpty then
                return (false, "No chunks generated for large document")
            elif totalChars < testContent.Length / 2 then
                return (false, $"Too much content lost in chunking: {totalChars} < {testContent.Length / 2}")
            else
                return (true, $"✅ Generated {chunks.Length} chunks, {totalChars} total chars, overlap: {hasOverlap}")
        | Error err ->
            return (false, $"Failed to process large document: {err}")
    }
    
    let testUnsupportedFormats () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_test_" + Guid.NewGuid().ToString("N")[..7])
        try
            Directory.CreateDirectory(testDir) |> ignore
            
            // Create a fake unsupported file
            let unsupportedFile = Path.Combine(testDir, "test.xyz")
            File.WriteAllText(unsupportedFile, "This is an unsupported format")
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let testPath = DocumentPath unsupportedFile
            
            match! processor.ExtractText(testPath) with
            | Ok _ ->
                TestData.cleanupTestFiles testDir
                return (false, "Should have failed for unsupported format")
            | Error (UnsupportedFormat _) ->
                TestData.cleanupTestFiles testDir
                return (true, "✅ Correctly rejected unsupported format")
            | Error err ->
                TestData.cleanupTestFiles testDir
                return (false, $"Wrong error type for unsupported format: {err}")
        with
        | ex ->
            TestData.cleanupTestFiles testDir
            return (false, $"Test setup failed: {ex.Message}")
    }
    
    let testFileNotFound () = async {
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let nonExistentPath = DocumentPath "/nonexistent/file.txt"
        
        match! processor.ExtractText(nonExistentPath) with
        | Ok _ ->
            return (false, "Should have failed for non-existent file")
        | Error (FileNotFound _) ->
            return (true, "✅ Correctly handled non-existent file")
        | Error err ->
            return (false, $"Wrong error type for non-existent file: {err}")
    }
    
    let runAllTests () = async {
        let tests = [
            ("Text Extraction", testTextExtraction)
            ("Document Processing", testDocumentProcessing)
            ("Chunking Behavior", testChunkingBehavior)
            ("Unsupported Formats", testUnsupportedFormats)
            ("File Not Found", testFileNotFound)
        ]
        
        let mutable results = []
        for (testName, testFunc) in tests do
            let! result = runTest testName testFunc
            results <- result :: results
            
        return List.rev results
    }
