open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

[<EntryPoint>]
let main argv =
    let mainWorkflow () = async {
        printfn "🔍 Smart Document Finder - Interactive Mode"
        printfn "==========================================="
        
        // Setup (same as test)
        let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        let dbPath = CrossPlatform.getDefaultDatabasePath()
        let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
        let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
        
        printfn "📁 Database: %s" dbPath
        
        // Ensure directory exists (cross-platform)
        CrossPlatform.ensureDirectoryExists(dbPath)
        
        // Initialize database
        let! _ = Database.initializeDatabase(dbPath)
        
        if argv.Length > 0 && argv.[0] = "index" then
            // Index documents from a folder
            let folder = if argv.Length > 1 then argv.[1] else "."
            printfn $"📁 Indexing documents from: {folder}"
            
            match! FolderScanner.scanFolder(folder) with
            | Ok documents ->
                printfn $"✅ Found {documents.Length} documents"
                
                // Index documents
                match! FolderScanner.indexDocuments searchEngine documents with
                | Ok indexed ->
                    printfn $"✅ Indexed {indexed} documents"
                | Error err ->
                    printfn $"❌ Failed to index documents: {err}"
            | Error err ->
                printfn $"❌ Failed to scan folder: {err}"
        else
            // Interactive search mode
            printfn "💡 Usage:"
            printfn "  - Type your search query and press Enter"
            printfn "  - Type 'quit' to exit"
            printfn "  - Example queries: 'machine learning', 'python tutorial', 'financial data'"
            printfn ""
            
            let rec searchLoop() = async {
                printf "🔍 Search: "
                let input = Console.ReadLine()
                
                if input = "quit" || input = "exit" then
                    ()  // Exit the loop
                elif not (String.IsNullOrWhiteSpace(input)) then
                    let query = {
                        Id = QueryId (Guid.NewGuid())
                        Text = input.Trim()
                        Filters = Map.empty
                        MaxResults = 10
                        Timestamp = DateTime.Now
                    }
                    
                    match! searchEngine.Search(query) with
                    | Ok response ->
                        if response.Results.Length = 0 then
                            printfn "❌ No relevant documents found for: '%s'" input
                        else
                            printfn $"✅ Found {response.Results.Length} relevant documents:"
                            response.Results 
                            |> List.iteri (fun i result ->
                                let (DocumentPath path) = result.DocumentPath
                                let fileName = Path.GetFileName(path)
                                let preview = 
                                    if result.ChunkContent.Length > 100 
                                    then result.ChunkContent.Substring(0, 100) + "..."
                                    else result.ChunkContent
                                printfn $"  {i+1}. {fileName}"
                                printfn $"     {preview}")
                    | Error err ->
                        printfn $"❌ Search error: {err}"
                    
                    printfn ""
                    do! searchLoop()
                else
                    // Empty input, just continue
                    do! searchLoop()
            }
            
            do! searchLoop()
            
        printfn "👋 Goodbye!"
    }
    
    mainWorkflow() |> Async.RunSynchronously
    0
