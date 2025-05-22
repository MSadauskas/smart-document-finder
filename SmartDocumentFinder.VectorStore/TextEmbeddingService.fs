namespace SmartDocumentFinder.VectorStore
open SmartDocumentFinder.Core
open System
open System.Text

type TextEmbeddingService() =
    let vocab = [|
        "the"; "of"; "and"; "to"; "a"; "in"; "for"; "is"; "on"; "that"; "by"; "this"; "with"; "i"; "you"; "it"; "not"; "or";
        "be"; "are"; "from"; "at"; "as"; "your"; "all"; "any"; "can"; "had"; "her"; "was"; "one"; "our"; "out"; "day";
        "get"; "has"; "him"; "his"; "how"; "man"; "new"; "now"; "old"; "see"; "two"; "way"; "who"; "boy"; "did"; "its";
        "let"; "put"; "say"; "she"; "too"; "use"; "document"; "file"; "text"; "search"; "find"; "index"; "word"; "data";
        "system"; "software"; "computer"; "technology"; "algorithm"; "machine"; "learning"; "intelligence"; "artificial";
        "code"; "programming"; "development"; "application"; "database"; "network"; "server"; "client"; "web"; "internet"
    |]
    
    let getWordVector (word: string) =
        let lowerWord = word.ToLowerInvariant()
        match Array.tryFindIndex ((=) lowerWord) vocab with
        | Some index -> 
            let vector = Array.create 128 0.0f
            vector.[index % 128] <- 1.0f
            vector
        | None ->
            let hash = lowerWord.GetHashCode()
            let rng = Random(abs hash)
            Array.init 128 (fun _ -> rng.NextSingle() * 0.2f - 0.1f)
    
    let createTextEmbedding (text: string) =
        let words = text.Split([|' '; '\n'; '\t'; '\r'; '.'; ','; ';'|], StringSplitOptions.RemoveEmptyEntries)
        let vectors = words |> Array.map getWordVector
        
        if vectors.Length = 0 then
            Array.create 128 0.0f
        else
            let sumVector = Array.create 128 0.0f
            for vector in vectors do
                for i = 0 to 127 do
                    sumVector.[i] <- sumVector.[i] + vector.[i]
            
            // Normalize
            let magnitude = sqrt (sumVector |> Array.sumBy (fun x -> x * x))
            if magnitude > 0.0f then
                for i = 0 to 127 do
                    sumVector.[i] <- sumVector.[i] / magnitude
            
            sumVector
    
    interface IEmbeddingService with
        member _.GenerateEmbedding(text: string) = 
            async {
                let vector = createTextEmbedding text
                return Ok (EmbeddingVector vector)
            }
        
        member _.GenerateBatchEmbeddings(texts: string list) = 
            async {
                let vectors = texts |> List.map (createTextEmbedding >> EmbeddingVector)
                return Ok vectors
            }
