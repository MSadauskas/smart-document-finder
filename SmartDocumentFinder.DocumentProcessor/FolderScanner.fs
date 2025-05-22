namespace SmartDocumentFinder.DocumentProcessor

open System
open System.IO
open SmartDocumentFinder.Core

module FolderScanner =
    
    let scanFolder (folderPath: string) : Async<Result<Document list, DocumentError>> =
        async {
            try
                let files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                let documents = 
                    files
                    |> Array.filter (fun f -> 
                        let ext = Path.GetExtension(f).ToLower()
                        [".txt"; ".pdf"; ".doc"; ".docx"] |> List.contains ext)
                    |> Array.map DocumentService.createDocument
                    |> Array.toList
                return Ok documents
            with
            | :? DirectoryNotFoundException -> return Error (FileNotFound folderPath)
            | :? UnauthorizedAccessException -> return Error (FileAccessDenied folderPath)
            | ex -> return Error (ProcessingError ex.Message)
        }
    
    let indexDocuments (searchEngine: ISearchEngine) (documents: Document list) : Async<Result<int, SearchError>> =
        async {
            let mutable indexed = 0
            for doc in documents do
                printfn "📄 Indexing: %A" doc.Metadata.Path
                match! searchEngine.IndexDocument(doc) with
                | Ok _ -> 
                    indexed <- indexed + 1
                    printfn "✅ Successfully indexed: %A" doc.Metadata.Path
                | Error err -> 
                    printfn "❌ Failed to index %A: %A" doc.Metadata.Path err
            return Ok indexed
        }
