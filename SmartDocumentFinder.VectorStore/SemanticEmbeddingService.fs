namespace SmartDocumentFinder.VectorStore

open System
open System.Text
open SmartDocumentFinder.Core

type SemanticEmbeddingService() =
    
    // Enhanced semantic vocabulary with expanded coverage
    let semanticWords = Map.ofList [
        // Technology & AI terms (dimension 0 - high tech score)
        ("machine", [|0.9f; 0.2f; 0.1f|])
        ("learning", [|0.8f; 0.3f; 0.2f|])
        ("algorithm", [|0.8f; 0.4f; 0.1f|])
        ("data", [|0.7f; 0.5f; 0.3f|])
        ("neural", [|0.9f; 0.1f; 0.0f|])
        ("network", [|0.8f; 0.3f; 0.1f|])
        ("ai", [|0.9f; 0.1f; 0.0f|])
        ("artificial", [|0.9f; 0.2f; 0.1f|])
        ("intelligence", [|0.9f; 0.2f; 0.1f|])
        ("deep", [|0.8f; 0.2f; 0.1f|])
        ("model", [|0.7f; 0.3f; 0.2f|])
        ("training", [|0.8f; 0.3f; 0.1f|])
        
        // Programming terms (dimension 1 - high programming score)
        ("python", [|0.4f; 0.9f; 0.1f|])
        ("programming", [|0.3f; 0.9f; 0.1f|])
        ("code", [|0.2f; 0.9f; 0.1f|])
        ("software", [|0.3f; 0.8f; 0.2f|])
        ("development", [|0.4f; 0.8f; 0.2f|])
        ("computer", [|0.5f; 0.7f; 0.2f|])
        ("coding", [|0.2f; 0.9f; 0.1f|])
        ("developer", [|0.3f; 0.8f; 0.2f|])
        ("engineering", [|0.4f; 0.8f; 0.2f|])
        ("tutorial", [|0.2f; 0.8f; 0.1f|])
        ("syntax", [|0.2f; 0.8f; 0.1f|])
        ("function", [|0.3f; 0.8f; 0.1f|])
        ("variable", [|0.2f; 0.8f; 0.1f|])
        ("class", [|0.3f; 0.8f; 0.1f|])
        ("object", [|0.3f; 0.7f; 0.1f|])
        
        // Business & Finance terms (dimension 2 - high business score)
        ("finance", [|0.1f; 0.2f; 0.9f|])
        ("financial", [|0.1f; 0.2f; 0.9f|])
        ("business", [|0.2f; 0.3f; 0.8f|])
        ("revenue", [|0.1f; 0.1f; 0.9f|])
        ("profit", [|0.1f; 0.1f; 0.9f|])
        ("market", [|0.2f; 0.2f; 0.8f|])
        ("investment", [|0.1f; 0.2f; 0.9f|])
        ("report", [|0.2f; 0.3f; 0.8f|])
        ("quarterly", [|0.1f; 0.2f; 0.8f|])
        ("annual", [|0.1f; 0.2f; 0.8f|])
        ("growth", [|0.2f; 0.3f; 0.8f|])
        ("sales", [|0.1f; 0.2f; 0.9f|])
        ("income", [|0.1f; 0.1f; 0.9f|])
        ("expenses", [|0.1f; 0.1f; 0.8f|])
        ("budget", [|0.1f; 0.2f; 0.8f|])
        ("accounting", [|0.1f; 0.2f; 0.9f|])
        ("economics", [|0.2f; 0.2f; 0.8f|])
        ("money", [|0.1f; 0.1f; 0.9f|])
        ("cost", [|0.1f; 0.2f; 0.8f|])
        ("price", [|0.1f; 0.2f; 0.8f|])
        ("company", [|0.2f; 0.3f; 0.8f|])
        ("corporation", [|0.2f; 0.3f; 0.8f|])
        ("enterprise", [|0.2f; 0.3f; 0.8f|])
        
        // Document types and academic terms
        ("document", [|0.2f; 0.4f; 0.6f|])
        ("analysis", [|0.4f; 0.3f; 0.6f|])
        ("research", [|0.5f; 0.4f; 0.4f|])
        ("study", [|0.4f; 0.4f; 0.4f|])
        ("paper", [|0.3f; 0.4f; 0.5f|])
        ("guide", [|0.3f; 0.6f; 0.3f|])
        ("manual", [|0.2f; 0.6f; 0.4f|])
        ("specification", [|0.3f; 0.7f; 0.3f|])
    ]
    
    let tokenize (text: string) =
        text.ToLowerInvariant()
            .Split([|' '; '\t'; '\n'; '\r'; '.'; ','; '!'; '?'; ';'; ':'|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList    
    let generateTextVector (text: string) =
        let tokens = tokenize text
        let baseVector = Array.create 384 0.0f
        
        // Enhanced semantic scoring with domain amplification
        let mutable techScore = 0.0f
        let mutable programmingScore = 0.0f  
        let mutable businessScore = 0.0f
        let mutable matchedTokens = 0
        
        for token in tokens do
            match semanticWords.TryFind token with
            | Some vector ->
                techScore <- techScore + vector.[0]
                programmingScore <- programmingScore + vector.[1] 
                businessScore <- businessScore + vector.[2]
                matchedTokens <- matchedTokens + 1
            | None -> 
                // Generic word contribution (smaller impact)
                programmingScore <- programmingScore + 0.05f
        
        // Domain amplification: boost scores when multiple related terms found
        let totalTokens = float32 tokens.Length
        if totalTokens > 0.0f then
            techScore <- techScore / totalTokens
            programmingScore <- programmingScore / totalTokens
            businessScore <- businessScore / totalTokens
            
            // Amplify dominant domains
            let maxScore = max (max techScore programmingScore) businessScore
            if maxScore > 0.3f then
                if techScore = maxScore then techScore <- techScore * 1.5f
                if programmingScore = maxScore then programmingScore <- programmingScore * 1.5f  
                if businessScore = maxScore then businessScore <- businessScore * 1.5f
        
        // Create enhanced semantic vector
        baseVector.[0] <- min 1.0f techScore           // Technology dimension
        baseVector.[1] <- min 1.0f programmingScore    // Programming dimension  
        baseVector.[2] <- min 1.0f businessScore       // Business dimension
        
        // Add semantic density bonus (more matched terms = higher confidence)
        let semanticDensity = float32 matchedTokens / max 1.0f totalTokens
        baseVector.[3] <- semanticDensity
        
        // Fill remaining dimensions with text-specific but consistent values
        let textHash = text.GetHashCode()
        let rng = Random(textHash)
        
        for i in 4 .. 383 do
            let hashComponent = (float32 (textHash >>> (i % 32)) / float32 Int32.MaxValue) * 0.15f
            let randomComponent = (rng.NextSingle() - 0.5f) * 0.08f
            baseVector.[i] <- hashComponent + randomComponent
        
        // Normalize the vector for consistent similarity calculation
        let magnitude = baseVector |> Array.map (fun x -> x * x) |> Array.sum |> sqrt
        if magnitude > 0.0f then
            for i in 0 .. 383 do
                baseVector.[i] <- baseVector.[i] / magnitude
        
        baseVector    
    interface IEmbeddingService with
        member _.GenerateEmbedding(text: string) = 
            async {
                try
                    let vector = generateTextVector text
                    return Ok (EmbeddingVector vector)
                with ex ->
                    return Error (StorageError ex.Message)
            }
        
        member _.GenerateBatchEmbeddings(texts: string list) = 
            async {
                try
                    let vectors = texts |> List.map (fun text -> 
                        let vector = generateTextVector text
                        EmbeddingVector vector)
                    return Ok vectors
                with ex ->
                    return Error (StorageError ex.Message)
            }
