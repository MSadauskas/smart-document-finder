# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Smart Document Finder is a production-ready semantic document search application built with F# and .NET 9. It provides AI-powered semantic search with binary relevance filtering (documents are either relevant with score 1.0 or filtered out entirely) instead of confusing similarity scores.

## Architecture

Multi-project F# solution with clean separation of concerns:

- **SmartDocumentFinder.Core**: Domain types, interfaces, cross-platform utilities
- **SmartDocumentFinder.DocumentProcessor**: Multi-format text extraction (PDF, Word, Excel, text)
- **SmartDocumentFinder.VectorStore**: SQLite-based vector storage with semantic embeddings
- **SmartDocumentFinder.SearchEngine**: Multiple search algorithms (basic, binary relevance, enhanced)
- **SmartDocumentFinder.UI**: Avalonia cross-platform desktop GUI (currently has syntax error)
- **SmartDocumentFinder.Interactive**: CLI interface for document indexing and search
- **SmartDocumentFinder.Test**: Integration tests (working perfectly)

## Build Commands

```bash
# Build backend (excludes broken UI)
dotnet build SmartDocumentFinder.sln --exclude SmartDocumentFinder.UI

# Build specific project
dotnet build SmartDocumentFinder.Core/
```

## Run Commands

```bash
# Run integration tests (working - primary way to verify functionality)
dotnet run --project SmartDocumentFinder.Test

# Interactive CLI mode
dotnet run --project SmartDocumentFinder.Interactive

# Index documents from folder
dotnet run --project SmartDocumentFinder.Interactive index ./test-docs

# Desktop GUI (currently broken due to syntax error)
dotnet run --project SmartDocumentFinder.UI
```

## Current Status

**✅ Production-Ready Backend:**
- ~5ms search response time
- Semantic search with 60+ vocabulary intelligence
- Cross-platform support (macOS/Linux/Windows)
- Type-safe F# with comprehensive error handling
- SQLite vector storage in `~/.smartdoc/`

**❌ Known Issues:**
1. **UI Syntax Error**: F# compilation error in `SmartDocumentFinder.UI/ViewModels/MainWindowViewModel.fs` lines 112-113
2. **PDF Processing**: One PDF fails with "empty stack" error during embedding

## Key Technologies

- F# with .NET 9
- Avalonia UI (cross-platform desktop)
- SQLite vector storage with cosine similarity
- UglyToad.PdfPig (PDF processing)
- DocumentFormat.OpenXml (Word/Excel processing)
- Microsoft.ML.OnnxRuntime support

## Test Data

Pre-configured test documents in `./test-docs/` including business reports, ML documentation, Python tutorials, and sample PDF/Word files.

## Architecture Notes

- **Domain-Driven Design**: Strong typing with `Result<T, Error>` throughout
- **Binary Relevance**: 0.45 similarity threshold filters irrelevant documents completely
- **Semantic Intelligence**: Custom 60+ vocabulary embedding service with domain-specific knowledge
- **Cross-Platform**: Handles file paths and user directories across OS platforms
- **Interface-Driven**: Clean abstractions for document processing, vector storage, and search engines

## Development Priority

The backend is production-ready. Primary development focus should be fixing the UI syntax error in MainWindowViewModel.fs to complete the desktop application.