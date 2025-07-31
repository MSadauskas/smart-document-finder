namespace SmartDocumentFinder.ComprehensiveTests

open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.VectorStore

module SemanticEmbeddingTests =
    
    type TestResult = {
        TestName: string
        Success: bool
        Message: string
        Duration: TimeSpan
    }
    
    let runTest (testName: string) (testFunc: unit -> Async<bool * string>) : Async<TestResult> =
        async {
            let startTime = DateTime.Now
            try
                let! (success, message) = testFunc()
                let duration = DateTime.Now - startTime
                return { TestName = testName; Success = success; Message = message; Duration = duration }
            with
            | ex ->
                let duration = DateTime.Now - startTime
                return { TestName = testName; Success = false; Message = ex.Message; Duration = duration }
        }
    
    let testEmbeddingGeneration () = async {
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        
        let testTexts = [
            "machine learning algorithms"
            "python programming tutorial"
            "financial business report"
            "software development documentation"
            "academic research paper"
        ]
        
        let mutable allSuccess = true
        let mutable messages = []
        
        for text in testTexts do
            match! embeddingService.GenerateEmbedding(text) with
            | Ok (EmbeddingVector vector) ->
                if vector.Length > 0 && vector |> Array.exists (fun x -> x <> 0.0f) then
                    messages <- $"✅ '{text}': {vector.Length}D vector" :: messages
                else
                    allSuccess <- false
                    messages <- $"❌ '{text}': empty or zero vector" :: messages
            | Error err ->
                allSuccess <- false
                messages <- $"❌ '{text}': {err}" :: messages
        
        return (allSuccess, String.Join("\n", List.rev messages))
    }
    
    let testDomainDiscrimination () = async {
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        
        // Test domain-specific queries
        let domainQueries = [
            ("Technology", ["machine learning", "artificial intelligence", "neural networks"])
            ("Programming", ["python programming", "code development", "software engineering"])
            ("Business", ["financial report", "revenue analysis", "business strategy"])
            ("Academic", ["research methodology", "academic study", "scientific analysis"])
        ]
        
        let mutable allSuccess = true
        let mutable messages = []
        
        for (domain, queries) in domainQueries do
            let mutable domainEmbeddings = []
            
            for query in queries do
                match! embeddingService.GenerateEmbedding(query) with
                | Ok embedding -> domainEmbeddings <- embedding :: domainEmbeddings
                | Error err ->
                    allSuccess <- false
                    messages <- $"❌ {domain} - '{query}': {err}" :: messages
            
            if domainEmbeddings.Length = queries.Length then
                // Check if embeddings within domain are more similar to each other
                let similarities = [
                    for i in 0 .. domainEmbeddings.Length - 2 do
                        for j in i + 1 .. domainEmbeddings.Length - 1 do
                            yield VectorOperations.cosineSimilarity domainEmbeddings.[i] domainEmbeddings.[j]
                ]
                
                let avgSimilarity = similarities |> List.average
                if avgSimilarity > 0.3 then
                    messages <- $"✅ {domain}: avg similarity {avgSimilarity:F3}" :: messages
                else
                    messages <- $"⚠️  {domain}: low similarity {avgSimilarity:F3}" :: messages
        
        return (allSuccess, String.Join("\n", List.rev messages))
    }
    
    let testVocabularyMatching () = async {
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        
        // Test specific vocabulary terms that should be recognized
        let vocabularyTests = [
            ("machine learning", ["machine", "learning", "ML", "algorithm"])
            ("python programming", ["python", "programming", "code", "development"])
            ("financial report", ["financial", "finance", "business", "revenue"])
            ("research paper", ["research", "academic", "study", "analysis"])
        ]
        
        let mutable allSuccess = true
        let mutable messages = []
        
        for (query, expectedTerms) in vocabularyTests do
            match! embeddingService.GenerateEmbedding(query) with
            | Ok queryEmbedding ->
                let mutable termMatches = 0
                
                for term in expectedTerms do
                    match! embeddingService.GenerateEmbedding(term) with
                    | Ok termEmbedding ->
                        let similarity = VectorOperations.cosineSimilarity queryEmbedding termEmbedding
                        if similarity > 0.2 then
                            termMatches <- termMatches + 1
                    | Error _ -> ()
                
                let matchRatio = float termMatches / float expectedTerms.Length
                if matchRatio >= 0.5 then
                    messages <- $"✅ '{query}': {termMatches}/{expectedTerms.Length} terms matched" :: messages
                else
                    allSuccess <- false
                    messages <- $"❌ '{query}': only {termMatches}/{expectedTerms.Length} terms matched" :: messages
            | Error err ->
                allSuccess <- false
                messages <- $"❌ '{query}': {err}" :: messages
        
        return (allSuccess, String.Join("\n", List.rev messages))
    }
    
    let testEmbeddingConsistency () = async {
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        
        let testText = "machine learning algorithms and neural networks"
        
        // Generate the same embedding multiple times
        let mutable embeddings = []
        for i in 1..5 do
            match! embeddingService.GenerateEmbedding(testText) with
            | Ok embedding -> embeddings <- embedding :: embeddings
            | Error err -> return (false, $"Failed to generate embedding {i}: {err}")
        
        if embeddings.Length = 5 then
            // Check consistency - all embeddings should be identical
            let firstEmbedding = embeddings.[0]
            let allIdentical = embeddings |> List.forall (fun emb -> 
                VectorOperations.cosineSimilarity firstEmbedding emb = 1.0)
            
            if allIdentical then
                return (true, "✅ Embedding generation is consistent")
            else
                return (false, "❌ Embedding generation is inconsistent")
        else
            return (false, $"Failed to generate all embeddings: {embeddings.Length}/5")
    }
    
    let testEmbeddingDimensions () = async {
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        
        let testTexts = [
            "short"
            "medium length text with several words"
            "This is a much longer text that contains many more words and should still produce an embedding of the same dimensionality as shorter texts, demonstrating that the embedding service normalizes input length appropriately."
        ]
        
        let mutable allSuccess = true
        let mutable dimensions = []
        let mutable messages = []
        
        for text in testTexts do
            match! embeddingService.GenerateEmbedding(text) with
            | Ok (EmbeddingVector vector) ->
                dimensions <- vector.Length :: dimensions
                messages <- $"✅ {text.Length} chars → {vector.Length}D" :: messages
            | Error err ->
                allSuccess <- false
                messages <- $"❌ {text.Length} chars: {err}" :: messages
        
        // Check if all dimensions are the same
        let uniqueDimensions = dimensions |> List.distinct
        if uniqueDimensions.Length = 1 then
            let dim = uniqueDimensions.[0]
            messages <- $"✅ Consistent dimensions: {dim}D" :: messages
        else
            allSuccess <- false
            messages <- $"❌ Inconsistent dimensions: {uniqueDimensions}" :: messages
        
        return (allSuccess, String.Join("\n", List.rev messages))
    }
    
    let testSemanticSimilarity () = async {
        let embeddingService = SemanticEmbeddingService() :> IEmbeddingService
        
        // Test semantic similarity between related concepts
        let similarityTests = [
            ("machine learning", "artificial intelligence", 0.3)
            ("python programming", "software development", 0.3)
            ("financial report", "business analysis", 0.3)
            ("research paper", "academic study", 0.3)
            ("neural networks", "deep learning", 0.4)
        ]
        
        let mutable allSuccess = true
        let mutable messages = []
        
        for (text1, text2, expectedMinSimilarity) in similarityTests do
            match! embeddingService.GenerateEmbedding(text1) with
            | Ok emb1 ->
                match! embeddingService.GenerateEmbedding(text2) with
                | Ok emb2 ->
                    let similarity = VectorOperations.cosineSimilarity emb1 emb2
                    if similarity >= expectedMinSimilarity then
                        messages <- $"✅ '{text1}' ↔ '{text2}': {similarity:F3}" :: messages
                    else
                        allSuccess <- false
                        messages <- $"❌ '{text1}' ↔ '{text2}': {similarity:F3} (expected ≥{expectedMinSimilarity:F1})" :: messages
                | Error err ->
                    allSuccess <- false
                    messages <- $"❌ '{text2}': {err}" :: messages
            | Error err ->
                allSuccess <- false
                messages <- $"❌ '{text1}': {err}" :: messages
        
        return (allSuccess, String.Join("\n", List.rev messages))
    }
    
    let runAllTests () = async {
        let tests = [
            ("Embedding Generation", testEmbeddingGeneration)
            ("Domain Discrimination", testDomainDiscrimination)
            ("Vocabulary Matching", testVocabularyMatching)
            ("Embedding Consistency", testEmbeddingConsistency)
            ("Embedding Dimensions", testEmbeddingDimensions)
            ("Semantic Similarity", testSemanticSimilarity)
        ]
        
        let mutable results = []
        for (testName, testFunc) in tests do
            let! result = runTest testName testFunc
            results <- result :: results
            
        return List.rev results
    }
