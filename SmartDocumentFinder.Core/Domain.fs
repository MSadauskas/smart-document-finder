namespace SmartDocumentFinder.Core
open System

type DocumentId = DocumentId of Guid
type DocumentPath = DocumentPath of string
type DocumentHash = DocumentHash of string

type DocumentFormat = Pdf | Word | Excel | PlainText | Unknown

type DocumentMetadata = {
    Id: DocumentId
    Path: DocumentPath
    FileName: string
    Size: int64
    Created: DateTime
    Modified: DateTime
    Format: DocumentFormat
    Hash: DocumentHash
}

type DocumentState =
    | NotProcessed
    | Processing
    | Processed of ProcessedAt: DateTime
    | Failed of Error: string * FailedAt: DateTime

type Document = {
    Metadata: DocumentMetadata
    State: DocumentState
}

type ChunkId = ChunkId of Guid
type ChunkIndex = ChunkIndex of int

type TextChunk = {
    Id: ChunkId
    DocumentId: DocumentId
    Index: ChunkIndex
    Content: string
    StartPosition: int
    EndPosition: int
    WordCount: int
}

type EmbeddingDimension = EmbeddingDimension of int
type EmbeddingVector = EmbeddingVector of float32[]

type DocumentEmbedding = {
    ChunkId: ChunkId
    Vector: EmbeddingVector
    Model: string
    CreatedAt: DateTime
}
