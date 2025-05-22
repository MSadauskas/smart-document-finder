namespace SmartDocumentFinder.VectorStore
open SmartDocumentFinder.Core

type SimpleEmbeddingService() =
    let rng = System.Random()
    
    interface IEmbeddingService with
        member _.GenerateEmbedding(text: string) = 
            async {
                // Create pseudo-random vector based on text hash
                let hash = text.GetHashCode()
                let rng = System.Random(hash)
                let vector = Array.init 384 (fun _ -> rng.NextSingle() * 2.0f - 1.0f)
                return Ok (EmbeddingVector vector)
            }
        
        member _.GenerateBatchEmbeddings(texts: string list) = 
            async {
                let vectors = texts |> List.map (fun text -> 
                    let hash = text.GetHashCode()
                    let rng = System.Random(hash)
                    let vector = Array.init 384 (fun _ -> rng.NextSingle() * 2.0f - 1.0f)
                    EmbeddingVector vector)
                return Ok vectors
            }
