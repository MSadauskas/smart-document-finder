namespace SmartDocumentFinder.VectorStore
open System
open SmartDocumentFinder.Core

module VectorOperations =
    let cosineSimilarity (EmbeddingVector v1) (EmbeddingVector v2) : float =
        let mutable dot = 0.0f
        let mutable n1 = 0.0f  
        let mutable n2 = 0.0f
        for i = 0 to v1.Length - 1 do
            dot <- dot + v1.[i] * v2.[i]
            n1 <- n1 + v1.[i] * v1.[i]
            n2 <- n2 + v2.[i] * v2.[i]
        let norm1 = sqrt n1
        let norm2 = sqrt n2
        if norm1 = 0.0f || norm2 = 0.0f then 0.0
        else float (dot / (norm1 * norm2))
    
    let serializeVector (EmbeddingVector vector) : byte[] =
        let bytes = Array.create (vector.Length * 4) 0uy
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length)
        bytes
    
    let deserializeVector (bytes: byte[]) : EmbeddingVector =
        let vector = Array.create (bytes.Length / 4) 0.0f
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length)
        EmbeddingVector vector
    
    let findTopSimilar (queryVector: EmbeddingVector) (candidates: (ChunkId * EmbeddingVector) list) (topK: int) : (ChunkId * float) list =
        candidates
        |> List.map (fun (id, vec) -> (id, cosineSimilarity queryVector vec))
        |> List.sortByDescending snd
        |> List.take (min topK candidates.Length)
