namespace SmartDocumentFinder.VectorStore

open System
open Microsoft.Data.Sqlite
open SmartDocumentFinder.Core

type DocumentIndexStats = {
    TotalDocuments: int
    LanguageBreakdown: Map<string, int>
    SourcePaths: string list
    LastIndexed: DateTime option
    IndexedToday: int
}

type DocumentIndexInfo(dbPath: string) =
    let connectionString = $"Data Source={dbPath};Cache=Shared;Pooling=true"
    
    member _.GetIndexStats() : Async<Result<DocumentIndexStats, SystemError>> =
        async {
            try
                use connection = new SqliteConnection(connectionString)
                connection.Open()
                
                // Get total document count
                let totalSql = "SELECT COUNT(*) FROM documents"
                use totalCommand = new SqliteCommand(totalSql, connection)
                let totalCount = totalCommand.ExecuteScalar() :?> int64 |> int
                
                // Get language breakdown
                let langSql = "SELECT language, COUNT(*) FROM documents GROUP BY language"
                use langCommand = new SqliteCommand(langSql, connection)
                use langReader = langCommand.ExecuteReader()
                
                let mutable languageBreakdown = Map.empty
                while langReader.Read() do
                    let lang = langReader.GetString(0)
                    let count = langReader.GetInt32(1)
                    languageBreakdown <- languageBreakdown.Add(lang, count)
                
                langReader.Close()
                
                // Get unique source paths (directories)
                let pathSql = "SELECT DISTINCT path FROM documents ORDER BY path"
                use pathCommand = new SqliteCommand(pathSql, connection)
                use pathReader = pathCommand.ExecuteReader()
                
                let mutable sourcePaths = []
                while pathReader.Read() do
                    let fullPath = pathReader.GetString(0)
                    let directory = System.IO.Path.GetDirectoryName(fullPath)
                    if not (List.contains directory sourcePaths) then
                        sourcePaths <- directory :: sourcePaths
                
                pathReader.Close()
                
                // Get last indexed timestamp
                let lastIndexedSql = "SELECT MAX(indexed_at) FROM documents WHERE indexed_at IS NOT NULL"
                use lastCommand = new SqliteCommand(lastIndexedSql, connection)
                let lastIndexedObj = lastCommand.ExecuteScalar()
                
                let lastIndexed = 
                    if lastIndexedObj = null || lastIndexedObj = box DBNull.Value then
                        None
                    else
                        try
                            Some (DateTime.Parse(lastIndexedObj.ToString()))
                        with
                        | _ -> None
                
                // Get documents indexed today
                let today = DateTime.Today
                let todaySql = "SELECT COUNT(*) FROM documents WHERE DATE(indexed_at) = DATE(@today)"
                use todayCommand = new SqliteCommand(todaySql, connection)
                todayCommand.Parameters.AddWithValue("@today", today.ToString("yyyy-MM-dd")) |> ignore
                let todayCount = todayCommand.ExecuteScalar() :?> int64 |> int
                
                let stats = {
                    TotalDocuments = totalCount
                    LanguageBreakdown = languageBreakdown
                    SourcePaths = List.rev sourcePaths
                    LastIndexed = lastIndexed
                    IndexedToday = todayCount
                }
                
                return Ok stats
                
            with
            | ex -> return Error (StorageError ex.Message)
        }
    
    member _.GetRecentDocuments(limit: int) : Async<Result<(string * string * DateTime) list, SystemError>> =
        async {
            try
                use connection = new SqliteConnection(connectionString)
                connection.Open()
                
                let sql = """
                    SELECT filename, path, indexed_at
                    FROM documents
                    WHERE indexed_at IS NOT NULL
                    ORDER BY indexed_at DESC
                    LIMIT @limit
                """

                use command = new SqliteCommand(sql, connection)
                command.Parameters.AddWithValue("@limit", limit) |> ignore
                use reader = command.ExecuteReader()
                
                let mutable recentDocs = []
                while reader.Read() do
                    let filename = reader.GetString(0)
                    let path = reader.GetString(1)
                    let indexedAt = DateTime.Parse(reader.GetString(2))
                    recentDocs <- (filename, path, indexedAt) :: recentDocs
                
                return Ok (List.rev recentDocs)
                
            with
            | ex -> return Error (StorageError ex.Message)
        }
