namespace SmartDocumentFinder.SearchEngine
open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.VectorStore

type EnhancedSearchEngine(vectorStore: IVectorStore, documentProcessor: IDocumentProcessor, embeddingService: IEmbeddingService, ?dbPath: string) =
    let path = defaultArg dbPath "/home/mikas/.smartdoc/data.db"
    let metadataStore = DocumentMetadataStore(path)
    let contentLookup = ContentLookup(path)
    
    interface ISearchEngine with
        member _.Search(query: SearchQuery) : Async<Result<SearchResponse, SearchError>> =
            async {
                let startTime = DateTime.Now
                try
                    printfn "🔍 Search query: '%s'" query.Text
                    match! embeddingService.GenerateEmbedding(query.Text) with
                    | Ok queryVector ->
                        printfn "✅ Query embedding generated: %d dimensions" (match queryVector with EmbeddingVector v -> v.Length)
                        match! vectorStore.SearchSimilar(queryVector, query.MaxResults) with
                        | Ok similarities ->
                            printfn "📊 Found %d similar vectors" similarities.Length
                            let finalResults = similarities
                                |> List.choose (fun (chunkId, score) ->
                                    if score > 0.2 then // Filter irrelevant
                                        let content = contentLookup.GetChunkContent(chunkId) |> Async.RunSynchronously
                                        let docInfo = contentLookup.GetDocumentFromChunk(chunkId) |> Async.RunSynchronously
                                        match content, docInfo with
                                        | Some chunkContent, Some (docId, docPath) ->
                                            Some {
                                                ChunkId = chunkId
                                                DocumentId = docId
                                                DocumentPath = docPath
                                                ChunkContent = chunkContent
                                                Score = SearchResultScore score
                                                Highlights = [query.Text]
                                            }
                                        | _ -> None
                                    else None)
                                |> List.sortByDescending (fun r -> let (SearchResultScore s) = r.Score in s)
                                |> List.take (min 8 similarities.Length) // Top 8 only
                            printfn "🎯 Final results: %d" finalResults.Length
                            return Ok {
                                Query = query
                                Results = finalResults
                                TotalFound = finalResults.Length
                                ProcessingTime = DateTime.Now - startTime
                            }
                        | Error err -> return Error (VectorStorageError (sprintf "%A" err))
                    | Error err -> return Error (EmbeddingGenerationFailed (sprintf "%A" err))
                with ex -> return Error (InvalidQuery ex.Message)
            }
        
        member _.IndexDocument(document: Document) : Async<Result<unit, SearchError>> =
            async {
                try
                    printfn "📝 Indexing: %A" document.Metadata.Path
                    let! _ = metadataStore.StoreDocument(document)
                    match! documentProcessor.ProcessDocument(document) with
                    | Ok chunks ->
                        printfn "✂️  Generated %d chunks" chunks.Length
                        for chunk in chunks do
                            let! _ = metadataStore.StoreChunk(chunk)
                            ()
                        let texts = chunks |> List.map (fun c -> c.Content)
                        match! embeddingService.GenerateBatchEmbeddings(texts) with
                        | Ok embeddings ->
                            printfn "🧠 Generated %d embeddings" embeddings.Length
                            for (chunk, embedding) in List.zip chunks embeddings do
                                let docEmbedding = {
                                    ChunkId = chunk.Id
                                    Vector = embedding
                                    Model = "enhanced-model"
                                    CreatedAt = DateTime.Now
                                }
                                let! _ = vectorStore.StoreEmbedding(docEmbedding)
                                ()
                            printfn "💾 Stored all embeddings"
                            return Ok ()
                        | Error err -> return Error (EmbeddingGenerationFailed (sprintf "%A" err))
                    | Error err -> return Error (EmbeddingGenerationFailed (sprintf "%A" err))
                with ex -> return Error (EmbeddingGenerationFailed ex.Message)
            }
