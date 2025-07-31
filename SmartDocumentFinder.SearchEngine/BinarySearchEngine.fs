namespace SmartDocumentFinder.SearchEngine
open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.VectorStore

type BinarySearchEngine(vectorStore: IVectorStore, documentProcessor: IDocumentProcessor, embeddingService: IEmbeddingService, ?dbPath: string) =
    let relevanceThreshold = 0.75 // Very high threshold to eliminate false positives
    let path = defaultArg dbPath (CrossPlatform.getDefaultDatabasePath())
    let metadataStore = DocumentMetadataStore(path)
    let contentLookup = ContentLookup(path)

    // Lithuanian terms that should trigger exact matching
    let lithuanianTerms = Set.ofList [
        "dokumentinis"; "filmas"; "kinas"; "video"
        "programavimas"; "programa"; "kompiuteris"; "duomenys"
        "technologija"; "mokymasis"; "algoritmas"
        "verslas"; "ataskaita"; "tyrimai"; "projektas"
    ]

    let containsLithuanianTerms (text: string) =
        let words = text.ToLowerInvariant().Split([|' '; '\t'; '\n'; '\r'|], StringSplitOptions.RemoveEmptyEntries)
        words |> Array.exists (fun word -> lithuanianTerms.Contains(word))

    let getDocumentLanguage (docId: DocumentId) : Async<Language option> =
        async {
            try
                use connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Cache=Shared;Pooling=true")
                connection.Open()

                let (DocumentId id) = docId
                let sql = "SELECT language FROM documents WHERE id = @id"
                use command = new Microsoft.Data.Sqlite.SqliteCommand(sql, connection)
                command.Parameters.AddWithValue("@id", id.ToString()) |> ignore

                use reader = command.ExecuteReader()
                if reader.Read() then
                    let langStr = reader.GetString(0)
                    return Some (LanguageDetection.stringToLanguage langStr)
                else
                    return None
            with
            | _ -> return None
        }
    
    interface ISearchEngine with
        member _.Search(query: SearchQuery) : Async<Result<SearchResponse, SearchError>> =
            async {
                let startTime = DateTime.Now
                try
                    printfn "🔍 Binary search: '%s'" query.Text
                    match! embeddingService.GenerateEmbedding(query.Text) with
                    | Ok queryVector ->
                        match! vectorStore.SearchSimilar(queryVector, query.MaxResults) with
                        | Ok similarities ->
                            // Binary relevance: filter and normalize scores
                            let relevantResults = 
                                similarities
                                |> List.choose (fun (chunkId, score) ->
                                    if score >= relevanceThreshold then
                                        printfn "✅ RELEVANT: chunk %A (score: %.3f)" chunkId score
                                        // Look up actual document path and content
                                        let docInfo = contentLookup.GetDocumentFromChunk(chunkId) |> Async.RunSynchronously
                                        let chunkContent = contentLookup.GetChunkContent(chunkId) |> Async.RunSynchronously
                                        
                                        match docInfo, chunkContent with
                                        | Some (docId, docPath), Some content ->
                                            // Check language filter from UI selection (Filters map) or auto-detection
                                            let languageFilter =
                                                match query.Filters.TryFind "language" with
                                                | Some langCode -> Some (LanguageDetection.stringToLanguage langCode)
                                                | None -> query.Language  // Fall back to auto-detected language

                                            match languageFilter with
                                            | Some targetLang ->
                                                let docLang = getDocumentLanguage docId |> Async.RunSynchronously
                                                match docLang with
                                                | Some lang when lang = targetLang ->
                                                    printfn "✅ LANGUAGE MATCH: doc=%A, filter=%A" lang targetLang
                                                    Some {
                                                        ChunkId = chunkId
                                                        DocumentId = docId
                                                        DocumentPath = docPath
                                                        ChunkContent = content
                                                        Score = SearchResultScore 1.0 // Binary: relevant = 1.0
                                                        Highlights = [query.Text]
                                                    }
                                                | Some lang ->
                                                    printfn "❌ LANGUAGE MISMATCH: doc=%A, filter=%A" lang targetLang
                                                    None
                                                | None ->
                                                    printfn "⚠️  Could not determine document language"
                                                    None
                                            | None ->
                                                // No language filter - include all results
                                                printfn "✅ NO LANGUAGE FILTER - including result"
                                                Some {
                                                    ChunkId = chunkId
                                                    DocumentId = docId
                                                    DocumentPath = docPath
                                                    ChunkContent = content
                                                    Score = SearchResultScore 1.0 // Binary: relevant = 1.0
                                                    Highlights = [query.Text]
                                                }
                                        | _ ->
                                            printfn "⚠️  Could not lookup document info for chunk %A" chunkId
                                            None
                                    else
                                        printfn "❌ IRRELEVANT: chunk %A (score: %.3f) - filtered out" chunkId score
                                        None)
                            
                            printfn "📊 Query results: %d relevant docs found" relevantResults.Length
                            let response = {
                                Query = query
                                Results = relevantResults
                                TotalFound = relevantResults.Length
                                ProcessingTime = DateTime.Now - startTime
                            }
                            return Ok response
                        | Error err ->
                            return Error (VectorStorageError (sprintf "%A" err))
                    | Error err ->
                        return Error (EmbeddingGenerationFailed (sprintf "%A" err))
                with
                | ex -> return Error (InvalidQuery ex.Message)
            }
        
        member _.IndexDocument(document: Document) : Async<Result<unit, SearchError>> =
            async {
                try
                    printfn "📝 Indexing document: %A" document.Metadata.Path
                    // Store document metadata first
                    let! _ = metadataStore.StoreDocument(document)
                    
                    match! documentProcessor.ProcessDocument(document) with
                    | Ok chunks ->
                        printfn "✂️  Generated %d chunks" chunks.Length
                        // Store each chunk
                        for chunk in chunks do
                            let! _ = metadataStore.StoreChunk(chunk)
                            ()
                        
                        // Generate and store embeddings
                        let texts = chunks |> List.map (fun c -> c.Content)
                        match! embeddingService.GenerateBatchEmbeddings(texts) with
                        | Ok embeddings ->
                            printfn "🧠 Generated %d embeddings" embeddings.Length
                            for (chunk, embedding) in List.zip chunks embeddings do
                                let docEmbedding = {
                                    ChunkId = chunk.Id
                                    Vector = embedding
                                    Model = "binary-search-model"
                                    CreatedAt = DateTime.Now
                                }
                                let! _ = vectorStore.StoreEmbedding(docEmbedding)
                                ()
                            printfn "💾 Stored all embeddings"
                            return Ok ()
                        | Error err ->
                            return Error (EmbeddingGenerationFailed (sprintf "%A" err))
                    | Error err ->
                        return Error (EmbeddingGenerationFailed (sprintf "%A" err))
                with
                | ex -> return Error (EmbeddingGenerationFailed ex.Message)
            }