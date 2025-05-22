namespace SmartDocumentFinder.DocumentProcessor

open System
open System.IO
open System.Text
open UglyToad.PdfPig
open SmartDocumentFinder.Core

module PdfExtractor =
    
    let extractPdfText (filePath: string) : Async<Result<string, DocumentError>> =
        async {
            try
                use document = PdfDocument.Open(filePath)
                let text = StringBuilder()
                
                let pages = document.GetPages() |> List.ofSeq
                if pages.IsEmpty then
                    return Ok ""
                else
                    for page in pages do
                        let pageText = page.Text
                        text.AppendLine(pageText) |> ignore
                    
                    return Ok (text.ToString())
            with
            | :? FileNotFoundException -> return Error (FileNotFound filePath)
            | :? UnauthorizedAccessException -> return Error (FileAccessDenied filePath)
            | ex -> return Error (ProcessingError ex.Message)
        }
