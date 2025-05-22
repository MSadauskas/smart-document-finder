namespace SmartDocumentFinder.SearchEngine
open System
open SmartDocumentFinder.Core

type BasicSearchEngine(vectorStore: IVectorStore, documentProcessor: IDocumentProcessor) =
    interface ISearchEngine with
        member _.Search(query: SearchQuery) : Async<Result<SearchResponse, SearchError>> =
            async {
                let startTime = DateTime.Now
                try
                    let queryVector = EmbeddingVector (Array.create 384 0.1f)
                    match! vectorStore.SearchSimilar(queryVector, query.MaxResults) with
                    | Ok similarities ->
                        let results = 
                            similarities
                            |> List.map (fun (chunkId, score) -> {
                                ChunkId = chunkId
                                DocumentId = DocumentId (Guid.NewGuid())
                                DocumentPath = DocumentPath "mock/path"
                                ChunkContent = "Mock chunk content"
                                Score = SearchResultScore score
                                Highlights = ["mock highlight"]
                            })
                        let response = {
                            Query = query
                            Results = results
                            TotalFound = results.Length
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
                                Model = "mock-model"
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
