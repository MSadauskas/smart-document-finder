namespace SmartDocumentFinder.DocumentProcessor

open System
open System.IO
open System.Text
open SmartDocumentFinder.Core

module TextExtractor =
    
    let detectFormat (filePath: string) : DocumentFormat =
        let extension = Path.GetExtension(filePath).ToLowerInvariant()
        match extension with
        | ".pdf" -> Pdf
        | ".doc" | ".docx" -> Word
        | ".xls" | ".xlsx" -> Excel
        | ".txt" | ".md" -> PlainText
        | _ -> Unknown

    let private extractPlainText (filePath: string) : Async<Result<string, DocumentError>> =
        async {
            try
                let! content = File.ReadAllTextAsync(filePath) |> Async.AwaitTask
                return Ok content
            with
            | :? FileNotFoundException -> 
                return Error (FileNotFound filePath)
            | :? UnauthorizedAccessException -> 
                return Error (FileAccessDenied filePath)
            | ex -> 
                return Error (ProcessingError ex.Message)
        }

    let private extractPdfText (filePath: string) : Async<Result<string, DocumentError>> =
        async {
            try
                // Simple PDF text extraction - basic implementation
                return Error (UnsupportedFormat "PDF extraction not yet implemented")
            with
            | ex -> 
                return Error (ProcessingError ex.Message)
        }

    let private extractWordText (filePath: string) : Async<Result<string, DocumentError>> =
        async {
            try
                match WordProcessor.extractTextFromDocx filePath with
                | Ok text -> return Ok text
                | Error err -> return Error (ProcessingError err)
            with
            | ex -> 
                return Error (ProcessingError ex.Message)
        }

    let extractText (DocumentPath filePath) : Async<Result<string, DocumentError>> =
        async {
            let format = detectFormat filePath
            match format with
            | PlainText -> 
                return! extractPlainText filePath
            | Pdf ->
                return! PdfExtractor.extractPdfText filePath
            | Word ->
                return! extractWordText filePath
            | Excel ->
                return Error (UnsupportedFormat $"Format {format} not yet implemented")
            | Unknown ->
                return Error (UnsupportedFormat $"Unknown file format: {Path.GetExtension(filePath)}")
        }

module TextChunker =
    
    let private splitIntoSentences (text: string) : string[] =
        text.Split([|'.'; '!'; '?'|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace(s)))

    let private countWords (text: string) : int =
        text.Split([|' '; '\t'; '\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries).Length

    let chunkText (documentId: DocumentId) (text: string) (chunkSize: int) (overlap: int) : TextChunk list =
        let sentences = splitIntoSentences text
        let mutable chunks = []
        let mutable currentChunk = StringBuilder()
        let mutable startPos = 0
        let mutable chunkIndex = 0

        for sentence in sentences do
            let proposedChunk = currentChunk.ToString() + sentence + ". "
            
            if (countWords proposedChunk) > chunkSize && currentChunk.Length > 0 then
                let chunkContent = currentChunk.ToString().Trim()
                let chunk = {
                    Id = ChunkId (Guid.NewGuid())
                    DocumentId = documentId
                    Index = ChunkIndex chunkIndex
                    Content = chunkContent
                    StartPosition = startPos
                    EndPosition = startPos + chunkContent.Length
                    WordCount = countWords chunkContent
                }
                chunks <- chunk :: chunks
                chunkIndex <- chunkIndex + 1
                startPos <- startPos + chunkContent.Length - overlap
                currentChunk.Clear() |> ignore
            
            currentChunk.Append(sentence).Append(". ") |> ignore

        // Add final chunk if remaining content
        if currentChunk.Length > 0 then
            let chunkContent = currentChunk.ToString().Trim()
            let chunk = {
                Id = ChunkId (Guid.NewGuid())
                DocumentId = documentId
                Index = ChunkIndex chunkIndex
                Content = chunkContent
                StartPosition = startPos
                EndPosition = startPos + chunkContent.Length
                WordCount = countWords chunkContent
            }
            chunks <- chunk :: chunks

        List.rev chunks
