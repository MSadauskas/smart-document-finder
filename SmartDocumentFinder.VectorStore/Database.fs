namespace SmartDocumentFinder.VectorStore

open System
open System.Data
open Microsoft.Data.Sqlite
open SmartDocumentFinder.Core

module Database =
    
    let private connectionString dbPath =
        $"Data Source={dbPath};Cache=Shared;Pooling=true"
    
    let initializeDatabase (dbPath: string) : Async<Result<unit, SystemError>> =
        async {
            try
                use connection = new SqliteConnection(connectionString dbPath)
                connection.Open()
                
                let createTablesCommand = """
                    CREATE TABLE IF NOT EXISTS documents (
                        id TEXT PRIMARY KEY,
                        path TEXT NOT NULL UNIQUE,
                        filename TEXT NOT NULL,
                        size INTEGER NOT NULL,
                        created TEXT NOT NULL,
                        modified TEXT NOT NULL,
                        format TEXT NOT NULL,
                        hash TEXT NOT NULL,
                        state TEXT NOT NULL,
                        indexed_at TEXT
                    );
                    
                    CREATE TABLE IF NOT EXISTS chunks (
                        id TEXT PRIMARY KEY,
                        document_id TEXT NOT NULL,
                        chunk_index INTEGER NOT NULL,
                        content TEXT NOT NULL,
                        start_position INTEGER NOT NULL,
                        end_position INTEGER NOT NULL,
                        word_count INTEGER NOT NULL,
                        FOREIGN KEY (document_id) REFERENCES documents (id)
                    );
                    
                    CREATE TABLE IF NOT EXISTS embeddings (
                        chunk_id TEXT PRIMARY KEY,
                        vector BLOB NOT NULL,
                        model TEXT NOT NULL,
                        created_at TEXT NOT NULL,
                        FOREIGN KEY (chunk_id) REFERENCES chunks (id)
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_documents_path ON documents(path);
                    CREATE INDEX IF NOT EXISTS idx_chunks_document ON chunks(document_id);
                    CREATE INDEX IF NOT EXISTS idx_embeddings_model ON embeddings(model);
                """
                
                use command = new SqliteCommand(createTablesCommand, connection)
                command.ExecuteNonQuery() |> ignore
                return Ok ()
            with
            | ex -> return Error (StorageError ex.Message)
        }

    let createChunksTable (connection: SqliteConnection) =
        let sql = """
            CREATE TABLE IF NOT EXISTS chunks (
                id TEXT PRIMARY KEY,
                document_id TEXT NOT NULL,
                chunk_index INTEGER NOT NULL,
                content TEXT NOT NULL,
                start_position INTEGER NOT NULL,
                end_position INTEGER NOT NULL,
                word_count INTEGER NOT NULL,
                FOREIGN KEY (document_id) REFERENCES documents (id)
            );
            
            CREATE TABLE IF NOT EXISTS embeddings (
                chunk_id TEXT PRIMARY KEY,
                vector BLOB NOT NULL,
                model TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (chunk_id) REFERENCES chunks (id)
            );
            
            CREATE INDEX IF NOT EXISTS idx_documents_path ON documents(path);
            CREATE INDEX IF NOT EXISTS idx_chunks_document ON chunks(document_id);
            CREATE INDEX IF NOT EXISTS idx_embeddings_model ON embeddings(model);
        """
        use command = new SqliteCommand(sql, connection)
        command.ExecuteNonQuery() |> ignore
