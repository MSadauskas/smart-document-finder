namespace SmartDocumentFinder.VectorStore

open System
open Microsoft.Data.Sqlite
open SmartDocumentFinder.Core

type ContentLookup(dbPath: string) =
    let connectionString = $"Data Source={dbPath};Cache=Shared;Pooling=true"
    
    member _.GetChunkContent(chunkId: ChunkId) : Async<string option> =
        async {
            try
                use connection = new SqliteConnection(connectionString)
                connection.Open()
                
                let (ChunkId id) = chunkId
                let sql = "SELECT content FROM chunks WHERE id = @id"
                use command = new SqliteCommand(sql, connection)
                command.Parameters.AddWithValue("@id", id.ToString()) |> ignore
                
                use reader = command.ExecuteReader()
                if reader.Read() then
                    return Some (reader.GetString(0))
                else
                    return None
            with
            | _ -> return None
        }
    
    member _.GetDocumentFromChunk(chunkId: ChunkId) : Async<(DocumentId * DocumentPath) option> =
        async {
            try
                use connection = new SqliteConnection(connectionString)
                connection.Open()
                
                let (ChunkId id) = chunkId
                let sql = """
                    SELECT d.id, d.path 
                    FROM documents d 
                    JOIN chunks c ON d.id = c.document_id 
                    WHERE c.id = @chunkId
                """
                use command = new SqliteCommand(sql, connection)
                command.Parameters.AddWithValue("@chunkId", id.ToString()) |> ignore
                
                use reader = command.ExecuteReader()
                if reader.Read() then
                    let docId = DocumentId (Guid.Parse(reader.GetString(0)))
                    let docPath = DocumentPath (reader.GetString(1))
                    return Some (docId, docPath)
                else
                    return None
            with
            | _ -> return None
        }
