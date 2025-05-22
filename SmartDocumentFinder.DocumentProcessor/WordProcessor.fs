namespace SmartDocumentFinder.DocumentProcessor

open System
open System.IO
open System.Text
open DocumentFormat.OpenXml.Packaging
open DocumentFormat.OpenXml.Wordprocessing
open SmartDocumentFinder.Core

// Suppress deprecated API warnings for OpenXml PackageProperties
#nowarn "0044"

module WordProcessor =
    
    let extractTextFromDocx (filePath: string) : Result<string, string> =
        try
            use doc = WordprocessingDocument.Open(filePath, false)
            match doc.MainDocumentPart with
            | null -> Error "Document has no main part"
            | mainPart ->
                let body = mainPart.Document.Body
                let sb = StringBuilder()
                
                // Simple text extraction - iterate through paragraphs
                for paragraph in body.Elements<Paragraph>() do
                    for run in paragraph.Elements<Run>() do
                        for text in run.Elements<Text>() do
                            sb.Append(text.Text) |> ignore
                    sb.AppendLine() |> ignore
                
                Ok (sb.ToString())
        with
        | ex -> Error $"Failed to extract from Word document: {ex.Message}"
    
    let extractMetadataFromDocx (filePath: string) : Result<Map<string, string>, string> =
        try
            use doc = WordprocessingDocument.Open(filePath, false)
            let props = doc.PackageProperties
            let metadata = 
                [
                    ("title", if isNull props.Title then "" else props.Title)
                    ("creator", if isNull props.Creator then "" else props.Creator)
                    ("subject", if isNull props.Subject then "" else props.Subject)
                    ("description", if isNull props.Description then "" else props.Description)
                    ("keywords", if isNull props.Keywords then "" else props.Keywords)
                    ("category", if isNull props.Category then "" else props.Category)
                    ("created", props.Created.ToString())
                    ("modified", props.Modified.ToString())
                ]
                |> List.choose (fun (key, value) -> 
                    if String.IsNullOrWhiteSpace(value) then None 
                    else Some (key, value))
                |> Map.ofList
            Ok metadata
        with
        | ex -> Error $"Failed to extract Word metadata: {ex.Message}"
