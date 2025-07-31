namespace SmartDocumentFinder.ComprehensiveTests

open System
open SmartDocumentFinder.Core
open SmartDocumentFinder.VectorStore

module VectorOperationsTests =
    
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
    
    let testCosineSimilarity () = async {
        // Test identical vectors
        let v1 = EmbeddingVector [| 1.0f; 2.0f; 3.0f |]
        let v2 = EmbeddingVector [| 1.0f; 2.0f; 3.0f |]
        let similarity1 = VectorOperations.cosineSimilarity v1 v2
        
        // Test orthogonal vectors
        let v3 = EmbeddingVector [| 1.0f; 0.0f |]
        let v4 = EmbeddingVector [| 0.0f; 1.0f |]
        let similarity2 = VectorOperations.cosineSimilarity v3 v4
        
        // Test opposite vectors
        let v5 = EmbeddingVector [| 1.0f; 2.0f |]
        let v6 = EmbeddingVector [| -1.0f; -2.0f |]
        let similarity3 = VectorOperations.cosineSimilarity v5 v6
        
        // Test zero vectors
        let v7 = EmbeddingVector [| 0.0f; 0.0f |]
        let v8 = EmbeddingVector [| 1.0f; 2.0f |]
        let similarity4 = VectorOperations.cosineSimilarity v7 v8
        
        let results = [
            ("Identical vectors", similarity1, fun s -> abs(s - 1.0) < 0.001)
            ("Orthogonal vectors", similarity2, fun s -> abs(s) < 0.001)
            ("Opposite vectors", similarity3, fun s -> abs(s - (-1.0)) < 0.001)
            ("Zero vector", similarity4, fun s -> s = 0.0)
        ]
        
        let mutable allSuccess = true
        let mutable messages = []
        
        for (name, similarity, validator) in results do
            if validator similarity then
                messages <- $"✅ {name}: {similarity:F6}" :: messages
            else
                allSuccess <- false
                messages <- $"❌ {name}: {similarity:F6} (unexpected)" :: messages
        
        return (allSuccess, String.Join("\n", List.rev messages))
    }
    
    let testVectorSerialization () = async {
        let originalVector = EmbeddingVector [| 1.5f; -2.3f; 0.0f; 42.7f; -0.001f |]
        
        // Serialize and deserialize
        let serialized = VectorOperations.serializeVector originalVector
        let deserialized = VectorOperations.deserializeVector serialized
        
        let (EmbeddingVector original) = originalVector
        let (EmbeddingVector restored) = deserialized
        
        if original.Length <> restored.Length then
            return (false, $"Length mismatch: {original.Length} vs {restored.Length}")
        
        let mutable allMatch = true
        for i = 0 to original.Length - 1 do
            if abs(original.[i] - restored.[i]) > 0.0001f then
                allMatch <- false
        
        if allMatch then
            return (true, $"✅ Vector serialization: {original.Length} elements, {serialized.Length} bytes")
        else
            return (false, "Vector values don't match after serialization/deserialization")
    }
    
    let testSimilarityRanking () = async {
        let queryVector = EmbeddingVector [| 1.0f; 0.0f; 0.0f |]
        
        let testVectors = [
            (ChunkId (Guid.NewGuid()), EmbeddingVector [| 1.0f; 0.0f; 0.0f |])  // Identical
            (ChunkId (Guid.NewGuid()), EmbeddingVector [| 0.9f; 0.1f; 0.0f |])  // Very similar
            (ChunkId (Guid.NewGuid()), EmbeddingVector [| 0.5f; 0.5f; 0.0f |])  // Somewhat similar
            (ChunkId (Guid.NewGuid()), EmbeddingVector [| 0.0f; 1.0f; 0.0f |])  // Orthogonal
            (ChunkId (Guid.NewGuid()), EmbeddingVector [| -1.0f; 0.0f; 0.0f |]) // Opposite
        ]
        
        let similarities = testVectors |> List.map (fun (id, vec) -> 
            (id, VectorOperations.cosineSimilarity queryVector vec))
        
        let ranked = VectorOperations.findTopSimilar queryVector testVectors 5
        
        // Check if results are properly ranked (descending order)
        let scores = ranked |> List.map snd
        let isProperlyRanked = 
            scores |> List.pairwise |> List.forall (fun (a, b) -> a >= b)
        
        if isProperlyRanked && ranked.Length = 5 then
            let topScore = ranked |> List.head |> snd
            let bottomScore = ranked |> List.last |> snd
            return (true, $"✅ Similarity ranking: top={topScore:F3}, bottom={bottomScore:F3}")
        else
            return (false, $"Ranking failed: length={ranked.Length}, properly_ranked={isProperlyRanked}")
    }
    
    let testVectorDimensions () = async {
        let dimensions = [1; 10; 100; 384; 1000]
        let mutable allSuccess = true
        let mutable messages = []
        
        for dim in dimensions do
            try
                let vector = EmbeddingVector (Array.create dim 0.5f)
                let serialized = VectorOperations.serializeVector vector
                let deserialized = VectorOperations.deserializeVector serialized
                let (EmbeddingVector restored) = deserialized
                
                if restored.Length = dim then
                    messages <- $"✅ Dimension {dim}: {serialized.Length} bytes" :: messages
                else
                    allSuccess <- false
                    messages <- $"❌ Dimension {dim}: length mismatch" :: messages
            with
            | ex ->
                allSuccess <- false
                messages <- $"❌ Dimension {dim}: {ex.Message}" :: messages
        
        return (allSuccess, String.Join("\n", List.rev messages))
    }
    
    let testPerformance () = async {
        let vectorSize = 384
        let numVectors = 1000
        
        // Generate test vectors
        let random = Random(42) // Fixed seed for reproducibility
        let queryVector = EmbeddingVector (Array.init vectorSize (fun _ -> float32 (random.NextDouble() * 2.0 - 1.0)))
        let testVectors = [
            for i in 1..numVectors do
                let id = ChunkId (Guid.NewGuid())
                let vector = EmbeddingVector (Array.init vectorSize (fun _ -> float32 (random.NextDouble() * 2.0 - 1.0)))
                yield (id, vector)
        ]
        
        let startTime = DateTime.Now
        let results = VectorOperations.findTopSimilar queryVector testVectors 10
        let duration = DateTime.Now - startTime
        
        if results.Length = 10 && duration.TotalMilliseconds < 100.0 then
            return (true, $"✅ Performance: {numVectors} vectors in {duration.TotalMilliseconds:F1}ms")
        else
            return (false, $"Performance issue: {results.Length} results in {duration.TotalMilliseconds:F1}ms")
    }
    
    let runAllTests () = async {
        let tests = [
            ("Cosine Similarity", testCosineSimilarity)
            ("Vector Serialization", testVectorSerialization)
            ("Similarity Ranking", testSimilarityRanking)
            ("Vector Dimensions", testVectorDimensions)
            ("Performance", testPerformance)
        ]
        
        let mutable results = []
        for (testName, testFunc) in tests do
            let! result = runTest testName testFunc
            results <- result :: results
            
        return List.rev results
    }
