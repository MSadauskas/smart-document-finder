open System
open System.IO
open SmartDocumentFinder.Core
open SmartDocumentFinder.DocumentProcessor
open SmartDocumentFinder.VectorStore
open SmartDocumentFinder.SearchEngine

[<EntryPoint>]
let main argv =
    printfn "📊 TESTING DOCUMENT INDEX STATUS"
    printfn "================================"
    
    let testIndexStatus () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_index_status_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            let! _ = Database.initializeDatabase(dbPath)
            
            let processor = DocumentService.DocumentProcessor() :> IDocumentProcessor
            let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
            let vectorStore = SqliteVectorStore(dbPath) :> IVectorStore
            let searchEngine = BinarySearchEngine(vectorStore, processor, embeddingService, dbPath) :> ISearchEngine
            let indexInfo = DocumentIndexInfo(dbPath)
            
            printfn "📊 Initial Index Status:"
            match! indexInfo.GetIndexStats() with
            | Ok stats ->
                printfn "   Total Documents: %d" stats.TotalDocuments
                printfn "   Languages: %A" stats.LanguageBreakdown
                printfn "   Source Paths: %A" stats.SourcePaths
                printfn "   Last Indexed: %A" stats.LastIndexed
                printfn "   Indexed Today: %d" stats.IndexedToday
            | Error err ->
                printfn "   ❌ Error: %A" err
            
            printfn ""
            printfn "📝 Adding test documents..."
            
            // Create test documents in different languages and folders
            let testDocs = [
                ("folder1/lithuanian-doc.txt", "Dokumentinis filmas apie gamtą ir gyvūnus. Šis filmas parodo įvairius gyvūnus jų natūralioje aplinkoje.")
                ("folder1/english-doc.txt", "This is a business and financial report covering market analysis, revenue projections, and investment opportunities.")
                ("folder2/programming-doc.txt", "Python programming tutorial about machine learning algorithms and data structures.")
                ("folder2/tech-doc.txt", "Software engineering best practices include clean code, testing, and documentation.")
                ("folder3/lithuanian-tech.txt", "Programavimo vadovas apie duomenų struktūras ir algoritmus. Šis vadovas skirtas pažengusiems programuotojams.")
            ]
            
            // Index all documents
            for (relativePath, content) in testDocs do
                let fullDir = Path.Combine(testDir, Path.GetDirectoryName(relativePath))
                Directory.CreateDirectory(fullDir) |> ignore
                
                let filePath = Path.Combine(testDir, relativePath)
                File.WriteAllText(filePath, content)
                
                let doc = {
                    Metadata = {
                        Id = DocumentId (Guid.NewGuid())
                        Path = DocumentPath filePath
                        FileName = Path.GetFileName(filePath)
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
                | Ok _ -> printfn "   ✅ Indexed: %s (%A)" relativePath doc.Metadata.Language
                | Error err -> printfn "   ❌ Failed to index %s: %A" relativePath err
                
                // Small delay to show time progression
                do! Async.Sleep(100)
            
            printfn ""
            printfn "📊 Updated Index Status:"
            match! indexInfo.GetIndexStats() with
            | Ok stats ->
                printfn "   📄 Total Documents: %d" stats.TotalDocuments
                
                printfn "   🌍 Language Breakdown:"
                for (lang, count) in Map.toList stats.LanguageBreakdown do
                    let langName =
                        match lang with
                        | "lt" -> "🇱🇹 Lithuanian"
                        | "en" -> "🇬🇧 English"
                        | _ -> lang
                    printfn "      %s: %d documents" langName count
                
                printfn "   📁 Source Paths:"
                for path in stats.SourcePaths do
                    let folderName = Path.GetFileName(path)
                    printfn "      📂 %s" folderName
                
                match stats.LastIndexed with
                | Some lastTime ->
                    printfn "   ⏰ Last Indexed: %s" (lastTime.ToString("yyyy-MM-dd HH:mm:ss"))
                | None ->
                    printfn "   ⏰ Last Indexed: Never"
                
                printfn "   📅 Indexed Today: %d documents" stats.IndexedToday
                
            | Error err ->
                printfn "   ❌ Error: %A" err
            
            printfn ""
            printfn "📋 Recent Documents:"
            match! indexInfo.GetRecentDocuments(3) with
            | Ok recentDocs ->
                for (filename, path, indexedAt) in recentDocs do
                    printfn "   📄 %s (indexed: %s)" filename (indexedAt.ToString("HH:mm:ss"))
            | Error err ->
                printfn "   ❌ Error: %A" err
            
            Directory.Delete(testDir, true)
            
        with
        | ex ->
            if Directory.Exists(testDir) then Directory.Delete(testDir, true)
            printfn "❌ Test failed: %s" ex.Message
    }
    
    try
        testIndexStatus () |> Async.RunSynchronously
        printfn ""
        printfn "🎯 Index status test completed!"
        0
    with
    | ex ->
        printfn "❌ Test execution failed: %s" ex.Message
        1
