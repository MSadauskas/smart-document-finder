namespace SmartDocumentFinder.VectorStore

open System
open Microsoft.Data.Sqlite
open SmartDocumentFinder.Core

type DocumentMetadataStore(dbPath: string) =
    let connectionString = $"Data Source={dbPath};Cache=Shared;Pooling=true"
    
    member _.StoreDocument(document: Document) : Async<Result<unit, SystemError>> =
        async {
            try
                use connection = new SqliteConnection(connectionString)
                connection.Open()
                
                let (DocumentId docId) = document.Metadata.Id
                let (DocumentPath path) = document.Metadata.Path
                let (DocumentHash hash) = document.Metadata.Hash
                
                let sql = """
                    INSERT OR REPLACE INTO documents
                    (id, path, filename, size, created, modified, format, hash, state, language, indexed_at)
                    VALUES (@id, @path, @filename, @size, @created, @modified, @format, @hash, @state, @language, @indexed_at)
                """
                
                use command = new SqliteCommand(sql, connection)
                command.Parameters.AddWithValue("@id", docId.ToString()) |> ignore
                command.Parameters.AddWithValue("@path", path) |> ignore
                command.Parameters.AddWithValue("@filename", document.Metadata.FileName) |> ignore
                command.Parameters.AddWithValue("@size", document.Metadata.Size) |> ignore
                command.Parameters.AddWithValue("@created", document.Metadata.Created.ToString("O")) |> ignore
                command.Parameters.AddWithValue("@modified", document.Metadata.Modified.ToString("O")) |> ignore
                command.Parameters.AddWithValue("@format", document.Metadata.Format.ToString()) |> ignore
                command.Parameters.AddWithValue("@hash", hash) |> ignore
                command.Parameters.AddWithValue("@state", "Processed") |> ignore
                command.Parameters.AddWithValue("@language", LanguageDetection.languageToString document.Metadata.Language) |> ignore
                command.Parameters.AddWithValue("@indexed_at", DateTime.Now.ToString("O")) |> ignore
                
                command.ExecuteNonQuery() |> ignore
                return Ok ()
            with
            | ex -> return Error (StorageError ex.Message)
        }
    
    member _.GetDocumentCount() : Async<Result<int, SystemError>> =
        async {
            try
                use connection = new SqliteConnection(connectionString)
                connection.Open()
                
                let sql = "SELECT COUNT(*) FROM documents"
                use command = new SqliteCommand(sql, connection)
                let count = command.ExecuteScalar() :?> int64
                return Ok (int count)
            with
            | ex -> return Error (StorageError ex.Message)
        }
        
    member _.StoreChunk(chunk: TextChunk) : Async<Result<unit, SystemError>> =
        async {
            try
                use connection = new SqliteConnection(connectionString)
                connection.Open()
                
                let (ChunkId chunkId) = chunk.Id
                let (DocumentId docId) = chunk.DocumentId
                let (ChunkIndex idx) = chunk.Index
                
                let sql = """
                    INSERT OR REPLACE INTO chunks
                    (id, document_id, chunk_index, content, start_position, end_position, word_count)
                    VALUES (@id, @docId, @idx, @content, @start, @end, @words)
                """
                
                use command = new SqliteCommand(sql, connection)
                command.Parameters.AddWithValue("@id", chunkId.ToString()) |> ignore
                command.Parameters.AddWithValue("@docId", docId.ToString()) |> ignore
                command.Parameters.AddWithValue("@idx", idx) |> ignore
                command.Parameters.AddWithValue("@content", chunk.Content) |> ignore
                command.Parameters.AddWithValue("@start", chunk.StartPosition) |> ignore
                command.Parameters.AddWithValue("@end", chunk.EndPosition) |> ignore
                command.Parameters.AddWithValue("@words", chunk.WordCount) |> ignore
                
                command.ExecuteNonQuery() |> ignore
                return Ok ()
            with
            | ex -> return Error (StorageError ex.Message)
        }
