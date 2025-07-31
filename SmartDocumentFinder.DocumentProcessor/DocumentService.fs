namespace SmartDocumentFinder.DocumentProcessor

open System
open System.IO
open System.Security.Cryptography
open SmartDocumentFinder.Core

module DocumentService =
    
    let private computeFileHash (filePath: string) : string =
        use sha256 = SHA256.Create()
        use stream = File.OpenRead(filePath)
        let hashBytes = sha256.ComputeHash(stream)
        Convert.ToHexString(hashBytes)

    let private createDocumentMetadata (filePath: string) : DocumentMetadata =
        let fileInfo = FileInfo(filePath)
        // Try to detect language from filename or content preview
        let language =
            try
                // Quick preview of file content for language detection
                let preview = File.ReadAllText(filePath) |> fun text ->
                    if text.Length > 500 then text.Substring(0, 500) else text
                LanguageDetection.detectDocumentLanguage preview
            with
            | _ -> English // Default to English if detection fails

        {
            Id = DocumentId (Guid.NewGuid())
            Path = DocumentPath filePath
            FileName = fileInfo.Name
            Size = fileInfo.Length
            Created = fileInfo.CreationTime
            Modified = fileInfo.LastWriteTime
            Format = TextExtractor.detectFormat filePath
            Hash = DocumentHash (computeFileHash filePath)
            Language = language
        }

    let createDocument (filePath: string) : Document =
        {
            Metadata = createDocumentMetadata filePath
            State = NotProcessed
        }

    type DocumentProcessor() =
        interface IDocumentProcessor with
            member _.ProcessDocument(document: Document) : Async<Result<TextChunk list, DocumentError>> =
                async {
                    let (DocumentPath filePath) = document.Metadata.Path
                    match! TextExtractor.extractText document.Metadata.Path with
                    | Ok text ->
                        let chunks = TextChunker.chunkText document.Metadata.Id text 200 50
                        return Ok chunks
                    | Error err ->
                        return Error err
                }

            member _.ExtractText(documentPath: DocumentPath) : Async<Result<string, DocumentError>> =
                TextExtractor.extractText documentPath
