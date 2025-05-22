namespace SmartDocumentFinder.SearchEngine
open System
open SmartDocumentFinder.Core

type BinarySearchEngine(vectorStore: IVectorStore, documentProcessor: IDocumentProcessor) =
    let relevanceThreshold = 0.3 // Binary threshold: relevant or not
    
    interface ISearchEngine with
        member _.Search(query: SearchQuery) : Async<Result<SearchResponse, SearchError>> =
            async {
                let startTime = DateTime.Now
                try
                    printfn "🔍 Binary search: '%s'" query.Text
                    let queryVector = EmbeddingVector (Array.create 384 0.1f)
                    match! vectorStore.SearchSimilar(queryVector, query.MaxResults) with
                    | Ok similarities ->
                        // Binary relevance: filter and normalize scores
                        let relevantResults = 
                            similarities
                            |> List.choose (fun (chunkId, score) ->
                                if score >= relevanceThreshold then
                                    printfn "✅ RELEVANT: chunk %A (score: %.3f)" chunkId score
                                    Some {
                                        ChunkId = chunkId
                                        DocumentId = DocumentId (Guid.NewGuid())
                                        DocumentPath = DocumentPath (sprintf "document_%A.txt" chunkId)
                                        ChunkContent = sprintf "Mock content for chunk %A matching: %s" chunkId query.Text
                                        Score = SearchResultScore 1.0 // Binary: relevant = 1.0
                                        Highlights = [query.Text]
                                    }
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
                with
                | ex -> return Error (InvalidQuery ex.Message)
            }        
        member _.IndexDocument(document: Document) : Async<Result<unit, SearchError>> =
            async {
                try
                    match! documentProcessor.ProcessDocument(document) with
                    | Ok chunks ->
                        for chunk in chunks do
                            let embedding = {
                                ChunkId = chunk.Id
                                Vector = EmbeddingVector (Array.create 384 0.1f)
                                Model = "binary-search-model"
                                CreatedAt = DateTime.Now
                            }
                            let! _ = vectorStore.StoreEmbedding(embedding)
                            ()
                        return Ok ()
                    | Error err ->
                        return Error (EmbeddingGenerationFailed (sprintf "%A" err))
                with
                | ex -> return Error (EmbeddingGenerationFailed ex.Message)
            }