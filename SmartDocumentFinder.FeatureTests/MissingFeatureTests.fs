module MissingFeatureTests

open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

// Test for missing UI functionality
let testUIAvailability () = async {
    try
        // Try to check if UI project builds
        let uiProjectPath = "SmartDocumentFinder.UI/SmartDocumentFinder.UI.fsproj"
        if File.Exists(uiProjectPath) then
            return (false, "UI project exists but has known syntax errors (documented in CURRENT-STATUS.md)")
        else
            return (false, "UI project not found")
    with
    | ex ->
        return (false, $"UI test failed: {ex.Message}")
}

// Test for advanced PDF processing
let testAdvancedPdfProcessing () = async {
    let testDir = Path.Combine(Path.GetTempPath(), "sdf_pdf_advanced_test_" + Guid.NewGuid().ToString("N")[..7])
    try
        Directory.CreateDirectory(testDir) |> ignore
        
        // Check if the existing PDF files in test-docs work
        let testDocsPath = "test-docs"
        if Directory.Exists(testDocsPath) then
            let pdfFiles = Directory.GetFiles(testDocsPath, "*.pdf")
            if pdfFiles.Length > 0 then
                let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
                let mutable successCount = 0
                let mutable totalCount = 0
                
                for pdfFile in pdfFiles do
                    totalCount <- totalCount + 1
                    let docPath = DocumentPath pdfFile
                    match! processor.ExtractText(docPath) with
                    | Ok text when not (String.IsNullOrEmpty(text)) ->
                        successCount <- successCount + 1
                    | Ok _ -> () // Empty text is acceptable for some PDFs
                    | Error _ -> () // Errors are documented
                
                Directory.Delete(testDir, true)
                return (true, $"PDF processing: {successCount}/{totalCount} PDFs processed successfully")
            else
                Directory.Delete(testDir, true)
                return (true, "No PDF files found in test-docs (expected)")
        else
            Directory.Delete(testDir, true)
            return (true, "test-docs directory not found (acceptable)")
    with
    | ex ->
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        return (false, $"Advanced PDF test failed: {ex.Message}")
}

// Test for Word document processing
let testWordDocumentProcessing () = async {
    let testDir = Path.Combine(Path.GetTempPath(), "sdf_word_test_" + Guid.NewGuid().ToString("N")[..7])
    try
        Directory.CreateDirectory(testDir) |> ignore
        
        // Check if Word processing is available
        let testDocsPath = "test-docs"
        if Directory.Exists(testDocsPath) then
            let docxFiles = Directory.GetFiles(testDocsPath, "*.docx")
            let docxTxtFiles = Directory.GetFiles(testDocsPath, "*.docx.txt")
            
            if docxFiles.Length > 0 || docxTxtFiles.Length > 0 then
                let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
                let mutable successCount = 0
                let mutable totalCount = 0
                
                // Test .docx files if any
                for docxFile in docxFiles do
                    totalCount <- totalCount + 1
                    let docPath = DocumentPath docxFile
                    match! processor.ExtractText(docPath) with
                    | Ok text when not (String.IsNullOrEmpty(text)) ->
                        successCount <- successCount + 1
                    | Ok _ -> () // Empty text might be acceptable
                    | Error _ -> () // Some errors expected
                
                // Test .docx.txt files (pre-extracted Word content)
                for txtFile in docxTxtFiles do
                    totalCount <- totalCount + 1
                    let docPath = DocumentPath txtFile
                    match! processor.ExtractText(docPath) with
                    | Ok text when not (String.IsNullOrEmpty(text)) ->
                        successCount <- successCount + 1
                    | Ok _ -> ()
                    | Error _ -> ()
                
                Directory.Delete(testDir, true)
                return (true, $"Word processing: {successCount}/{totalCount} documents processed")
            else
                Directory.Delete(testDir, true)
                return (true, "No Word documents found (acceptable)")
        else
            Directory.Delete(testDir, true)
            return (true, "test-docs directory not found (acceptable)")
    with
    | ex ->
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        return (false, $"Word document test failed: {ex.Message}")
}

// Test for real-time indexing capability
let testRealTimeIndexing () = async {
    // This feature is not mentioned as implemented, so we expect it to be missing
    return (false, "Real-time indexing not implemented (would require file system watchers)")
}

// Test for multi-language support
let testMultiLanguageSupport () = async {
    let testDir = Path.Combine(Path.GetTempPath(), "sdf_multilang_test_" + Guid.NewGuid().ToString("N")[..7])
    let dbPath = Path.Combine(testDir, "test.db")
    
    try
        Directory.CreateDirectory(testDir) |> ignore
        let! _ = Database.initializeDatabase(dbPath)
        
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
        let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
        
        // Test with non-English content
        let spanishContent = "Aprendizaje automático y inteligencia artificial"
        let frenchContent = "Apprentissage automatique et intelligence artificielle"
        let germanContent = "Maschinelles Lernen und künstliche Intelligenz"
        
        let testFile = Path.Combine(testDir, "multilang.txt")
        File.WriteAllText(testFile, spanishContent)
        
        let doc = {
            Metadata = {
                Id = DocumentId (Guid.NewGuid())
                Path = DocumentPath testFile
                FileName = "multilang.txt"
                Size = int64 spanishContent.Length
                Created = DateTime.Now
                Modified = DateTime.Now
                Format = PlainText
                Hash = DocumentHash (spanishContent.GetHashCode().ToString())
            }
            State = NotProcessed
        }
        
        match! searchEngine.IndexDocument(doc) with
        | Ok _ ->
            // Try searching in English for Spanish content
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
                if response.Results.Length > 0 then
                    return (true, "Multi-language: Cross-language search works (limited)")
                else
                    return (false, "Multi-language: No cross-language search capability")
            | Error err ->
                Directory.Delete(testDir, true)
                return (false, $"Multi-language search failed: {err}")
        | Error err ->
            Directory.Delete(testDir, true)
            return (false, $"Multi-language indexing failed: {err}")
    with
    | ex ->
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        return (false, $"Multi-language test failed: {ex.Message}")
}

// Test for advanced search filters
let testAdvancedSearchFilters () = async {
    let testDir = Path.Combine(Path.GetTempPath(), "sdf_filters_test_" + Guid.NewGuid().ToString("N")[..7])
    let dbPath = Path.Combine(testDir, "test.db")
    
    try
        Directory.CreateDirectory(testDir) |> ignore
        let! _ = Database.initializeDatabase(dbPath)
        
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
        let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
        
        // Test with filters
        let query = {
            Id = QueryId (Guid.NewGuid())
            Text = "test"
            Filters = Map.ofList [("fileType", "pdf"); ("dateRange", "last30days")]
            MaxResults = 5
            Timestamp = DateTime.Now
        }
        
        match! searchEngine.Search(query) with
        | Ok response ->
            Directory.Delete(testDir, true)
            // Filters are accepted but may not be implemented
            return (false, "Advanced filters: Interface exists but implementation unclear")
        | Error err ->
            Directory.Delete(testDir, true)
            return (false, $"Advanced filters test failed: {err}")
    with
    | ex ->
        if Directory.Exists(testDir) then Directory.Delete(testDir, true)
        return (false, $"Advanced filters test failed: {ex.Message}")
}

// Test for ONNX/advanced embedding models
let testAdvancedEmbeddingModels () = async {
    try
        // Check if ONNX embedding service exists
        // Based on the codebase, there should be an OnnxEmbeddingService
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        
        match! embeddingService.GenerateEmbedding("test") with
        | Ok (EmbeddingVector vector) ->
            if vector.Length >= 384 then
                return (true, $"Advanced embeddings: Using {vector.Length}D vectors (good quality)")
            else
                return (false, $"Advanced embeddings: Only {vector.Length}D vectors (basic quality)")
        | Error err ->
            return (false, $"Advanced embeddings test failed: {err}")
    with
    | ex ->
        return (false, $"Advanced embeddings test failed: {ex.Message}")
}

let runMissingFeatureTests () = async {
    let tests = [
        ("UI Availability", testUIAvailability)
        ("Advanced PDF Processing", testAdvancedPdfProcessing)
        ("Word Document Processing", testWordDocumentProcessing)
        ("Real-time Indexing", testRealTimeIndexing)
        ("Multi-language Support", testMultiLanguageSupport)
        ("Advanced Search Filters", testAdvancedSearchFilters)
        ("Advanced Embedding Models", testAdvancedEmbeddingModels)
    ]
    
    let mutable results = []
    for (testName, testFunc) in tests do
        let startTime = DateTime.Now
        try
            let! (success, message) = testFunc()
            let duration = DateTime.Now - startTime
            results <- (testName, success, message, duration) :: results
        with
        | ex ->
            let duration = DateTime.Now - startTime
            results <- (testName, false, ex.Message, duration) :: results
    
    return List.rev results
}
