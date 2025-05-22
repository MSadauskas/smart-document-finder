namespace SmartDocumentFinder.VectorStore

open System
open SmartDocumentFinder.Core

// Fallback to simple service until ONNX models ready
type OnnxEmbeddingService(modelPath: string option) =
    
    interface IEmbeddingService with
        member this.GenerateEmbedding(text: string) : Async<Result<EmbeddingVector, SystemError>> =
            async {
                // Mock embedding until real ONNX model available
                let mockEmbedding = Array.create 384 0.1f
                return Ok (EmbeddingVector mockEmbedding)
            }
        
        member this.GenerateBatchEmbeddings(texts: string list) : Async<Result<EmbeddingVector list, SystemError>> =
            async {
                let embeddings = texts |> List.map (fun _ -> EmbeddingVector (Array.create 384 0.1f))
                return Ok embeddings
            }
    
    interface IDisposable with
        member this.Dispose() = ()
