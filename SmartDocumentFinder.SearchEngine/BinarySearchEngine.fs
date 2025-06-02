namespace SmartDocumentFinder.SearchEngine
open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.VectorStore

type BinarySearchEngine(vectorStore: IVectorStore, documentProcessor: IDocumentProcessor, embeddingService: IEmbeddingService, ?dbPath: string) =
    let relevanceThreshold = 0.45 // More selective binary threshold
    let path = defaultArg dbPath (CrossPlatform.getDefaultDatabasePath())
    let metadataStore = DocumentMetadataStore(path)
    let contentLookup = ContentLookup(path)
    
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