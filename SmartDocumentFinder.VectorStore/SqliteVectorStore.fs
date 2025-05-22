namespace SmartDocumentFinder.VectorStore

open System
open Microsoft.Data.Sqlite
open SmartDocumentFinder.Core

type SqliteVectorStore(dbPath: string) =
    
    let connectionString = $"Data Source={dbPath};Cache=Shared;Pooling=true"
    
    interface IVectorStore with
        
        member _.StoreEmbedding(embedding: DocumentEmbedding) : Async<Result<unit, SystemError>> =
            async {
                try
                    use connection = new SqliteConnection(connectionString)
                    connection.Open()
                    
                    let (ChunkId chunkId) = embedding.ChunkId
                    let vectorBytes = VectorOperations.serializeVector embedding.Vector
                    
                    let sql = """
                        INSERT OR IGNORE INTO embeddings (chunk_id, vector, model, created_at)
                        VALUES (@chunkId, @vector, @model, @createdAt)
                    """
                    
                    use command = new SqliteCommand(sql, connection)
                    command.Parameters.AddWithValue("@chunkId", chunkId.ToString()) |> ignore
                    command.Parameters.AddWithValue("@vector", vectorBytes) |> ignore
                    command.Parameters.AddWithValue("@model", embedding.Model) |> ignore
                    command.Parameters.AddWithValue("@createdAt", embedding.CreatedAt.ToString("O")) |> ignore
                    
                    command.ExecuteNonQuery() |> ignore
                    return Ok ()
                    
                with
                | ex -> return Error (StorageError ex.Message)
            }
        
        member _.SearchSimilar(queryVector: EmbeddingVector, limit: int) : Async<Result<(ChunkId * float) list, SystemError>> =
            async {
                try
                    use connection = new SqliteConnection(connectionString)
                    connection.Open()
                    
                    let sql = "SELECT chunk_id, vector FROM embeddings"
                    use command = new SqliteCommand(sql, connection)
                    use reader = command.ExecuteReader()
                    
                    let candidates = ResizeArray<ChunkId * EmbeddingVector>()
                    
                    while reader.Read() do
                        let chunkId = ChunkId (Guid.Parse(reader.GetString(0)))
                        let vectorBytes = reader.GetValue(1) :?> byte[]
                        let vector = VectorOperations.deserializeVector vectorBytes
                        candidates.Add((chunkId, vector))
                    
                    printfn "🗄️  DB: Found %d embeddings" candidates.Count
                    let results = VectorOperations.findTopSimilar queryVector (List.ofSeq candidates) limit
                    printfn "🔗 Similarity matches: %d" results.Length
                    return Ok results
                    
                with
                | ex -> return Error (StorageError ex.Message)
            }
        
        member _.GetEmbedding(chunkId: ChunkId) : Async<Result<DocumentEmbedding option, SystemError>> =
            async {
                try
                    use connection = new SqliteConnection(connectionString)
                    connection.Open()
                    
                    let sql = "SELECT vector, model, created_at FROM embeddings WHERE chunk_id = @chunkId"
                    use command = new SqliteCommand(sql, connection)
                    let (ChunkId id) = chunkId
                    command.Parameters.AddWithValue("@chunkId", id.ToString()) |> ignore
                    
                    use reader = command.ExecuteReader()
                    
                    if reader.Read() then
                        let vectorBytes = reader.GetValue(0) :?> byte[]
                        let vector = VectorOperations.deserializeVector vectorBytes
                        let model = reader.GetString(1)
                        let createdAt = DateTime.Parse(reader.GetString(2))
                        
                        let embedding = {
                            ChunkId = chunkId
                            Vector = vector
                            Model = model
                            CreatedAt = createdAt
                        }
                        return Ok (Some embedding)
                    else
                        return Ok None
                        
                with
                | ex -> return Error (StorageError ex.Message)
            }
