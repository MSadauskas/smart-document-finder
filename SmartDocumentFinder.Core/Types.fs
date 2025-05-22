namespace SmartDocumentFinder.Core
open System

type QueryId = QueryId of Guid
type SearchResultScore = SearchResultScore of float

type SearchQuery = {
    Id: QueryId
    Text: string
    Filters: Map<string, string>
    MaxResults: int
    Timestamp: DateTime
}

type SearchResult = {
    ChunkId: ChunkId
    DocumentId: DocumentId
    DocumentPath: DocumentPath
    ChunkContent: string
    Score: SearchResultScore
    Highlights: string list
}

type SearchResponse = {
    Query: SearchQuery
    Results: SearchResult list
    TotalFound: int
    ProcessingTime: TimeSpan
}

type DocumentError =
    | FileNotFound of path: string
    | FileAccessDenied of path: string
    | UnsupportedFormat of format: string
    | ProcessingError of message: string

type SearchError =
    | InvalidQuery of message: string
    | EmbeddingGenerationFailed of message: string
    | VectorStorageError of message: string

type SystemError =
    | ConfigurationError of message: string
    | InitializationError of message: string
    | StorageError of message: string

type AppError =
    | DocumentError of DocumentError
    | SearchError of SearchError  
    | SystemError of SystemError
