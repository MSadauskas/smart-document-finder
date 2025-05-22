namespace SmartDocumentFinder.Core

// Service interfaces
type IDocumentProcessor =
    abstract member ProcessDocument: Document -> Async<Result<TextChunk list, DocumentError>>
    abstract member ExtractText: DocumentPath -> Async<Result<string, DocumentError>>

type IVectorStore =
    abstract member StoreEmbedding: DocumentEmbedding -> Async<Result<unit, SystemError>>
    abstract member SearchSimilar: EmbeddingVector * int -> Async<Result<(ChunkId * float) list, SystemError>>
    abstract member GetEmbedding: ChunkId -> Async<Result<DocumentEmbedding option, SystemError>>

type ISearchEngine =
    abstract member Search: SearchQuery -> Async<Result<SearchResponse, SearchError>>
    abstract member IndexDocument: Document -> Async<Result<unit, SearchError>>

type IEmbeddingService =
    abstract member GenerateEmbedding: string -> Async<Result<EmbeddingVector, SystemError>>
    abstract member GenerateBatchEmbeddings: string list -> Async<Result<EmbeddingVector list, SystemError>>

// Configuration types
type EmbeddingModel = {
    Name: string
    Dimensions: EmbeddingDimension
    MaxTokens: int
    ModelPath: string option
}

type LLMModel = {
    Name: string
    ModelPath: string
    MaxContextTokens: int
    Temperature: float
}

type SystemConfig = {
    WatchDirectories: string list
    EmbeddingModel: EmbeddingModel
    LLMModel: LLMModel option
    ChunkSize: int
    ChunkOverlap: int
    MaxConcurrentProcessing: int
    VectorStorePath: string
    EnableAutoSync: bool
}
