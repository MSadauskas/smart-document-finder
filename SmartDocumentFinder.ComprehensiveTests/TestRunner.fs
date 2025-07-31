namespace SmartDocumentFinder.ComprehensiveTests

open System

module TestRunner =
    
    type TestSuiteResult = {
        SuiteName: string
        Results: DocumentProcessorTests.TestResult list
        TotalTests: int
        PassedTests: int
        FailedTests: int
        TotalDuration: TimeSpan
    }
    
    type OverallTestResult = {
        SuiteResults: TestSuiteResult list
        TotalTests: int
        TotalPassed: int
        TotalFailed: int
        TotalDuration: TimeSpan
        SuccessRate: float
    }
    
    let createSuiteResult suiteName results =
        let totalTests = results |> List.length
        let passedTests = results |> List.filter (fun r -> r.Success) |> List.length
        let failedTests = totalTests - passedTests
        let totalDuration = results |> List.fold (fun acc r -> acc + r.Duration) TimeSpan.Zero
        
        {
            SuiteName = suiteName
            Results = results
            TotalTests = totalTests
            PassedTests = passedTests
            FailedTests = failedTests
            TotalDuration = totalDuration
        }
    
    let printTestResult (result: DocumentProcessorTests.TestResult) =
        let status = if result.Success then "✅ PASS" else "❌ FAIL"
        let duration = result.Duration.TotalMilliseconds
        printfn "  %s %-40s (%6.1fms)" status result.TestName duration
        
        if not result.Success || result.Message.Contains("❌") then
            // Print detailed message for failures or warnings
            let lines = result.Message.Split('\n')
            for line in lines do
                if not (String.IsNullOrWhiteSpace(line)) then
                    printfn "    %s" line
        elif result.Message.Length > 100 then
            // Print summary for long success messages
            let summary = result.Message.Substring(0, min 100 result.Message.Length) + "..."
            printfn "    %s" summary
        else
            printfn "    %s" result.Message
    
    let printSuiteResult (suiteResult: TestSuiteResult) =
        let successRate = float suiteResult.PassedTests / float suiteResult.TotalTests * 100.0
        let status = if suiteResult.FailedTests = 0 then "✅" else "❌"
        
        printfn ""
        printfn "═══════════════════════════════════════════════════════════════"
        printfn "%s %s" status suiteResult.SuiteName
        printfn "═══════════════════════════════════════════════════════════════"
        printfn "Tests: %d | Passed: %d | Failed: %d | Success Rate: %.1f%%" 
                suiteResult.TotalTests suiteResult.PassedTests suiteResult.FailedTests successRate
        printfn "Duration: %.1fms" suiteResult.TotalDuration.TotalMilliseconds
        printfn ""
        
        for result in suiteResult.Results do
            printTestResult result
    
    let printOverallResult (overallResult: OverallTestResult) =
        printfn ""
        printfn "╔═══════════════════════════════════════════════════════════════╗"
        printfn "║                      OVERALL TEST RESULTS                    ║"
        printfn "╚═══════════════════════════════════════════════════════════════╝"
        printfn ""
        
        let status = if overallResult.TotalFailed = 0 then "✅ ALL TESTS PASSED" else "❌ SOME TESTS FAILED"
        printfn "%s" status
        printfn ""
        printfn "Total Tests:    %d" overallResult.TotalTests
        printfn "Passed:         %d" overallResult.TotalPassed
        printfn "Failed:         %d" overallResult.TotalFailed
        printfn "Success Rate:   %.1f%%" overallResult.SuccessRate
        printfn "Total Duration: %.1fs" overallResult.TotalDuration.TotalSeconds
        printfn ""
        
        // Print suite summary
        printfn "Suite Summary:"
        for suite in overallResult.SuiteResults do
            let suiteStatus = if suite.FailedTests = 0 then "✅" else "❌"
            let suiteRate = float suite.PassedTests / float suite.TotalTests * 100.0
            printfn "  %s %-30s %2d/%2d (%.0f%%)" suiteStatus suite.SuiteName suite.PassedTests suite.TotalTests suiteRate
        
        printfn ""
        
        // Print failed tests summary if any
        if overallResult.TotalFailed > 0 then
            printfn "Failed Tests:"
            for suite in overallResult.SuiteResults do
                let failedTests = suite.Results |> List.filter (fun r -> not r.Success)
                if not failedTests.IsEmpty then
                    printfn "  %s:" suite.SuiteName
                    for test in failedTests do
                        printfn "    ❌ %s" test.TestName
            printfn ""
    
    let runAllTestSuites () = async {
        printfn "╔═══════════════════════════════════════════════════════════════╗"
        printfn "║              SMART DOCUMENT FINDER - COMPREHENSIVE TESTS     ║"
        printfn "╚═══════════════════════════════════════════════════════════════╝"
        printfn ""
        printfn "Starting comprehensive test suite..."
        printfn "Platform: %s" (CrossPlatform.getPlatformInfo())
        printfn "Timestamp: %s" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        
        let overallStartTime = DateTime.Now
        let mutable allSuiteResults = []
        
        // Define test suites
        let testSuites = [
            ("Document Processor Tests", DocumentProcessorTests.runAllTests)
            ("Vector Operations Tests", VectorOperationsTests.runAllTests)
            ("Search Engine Tests", SearchEngineTests.runAllTests)
            ("Semantic Embedding Tests", SemanticEmbeddingTests.runAllTests)
            ("Cross-Platform Tests", CrossPlatformTests.runAllTests)
            ("Performance Tests", PerformanceTests.runAllTests)
            ("Error Handling Tests", ErrorHandlingTests.runAllTests)
            ("End-to-End Tests", EndToEndTests.runAllTests)
        ]
        
        // Run each test suite
        for (suiteName, testFunc) in testSuites do
            printfn ""
            printfn "Running %s..." suiteName
            
            try
                let! results = testFunc()
                let suiteResult = createSuiteResult suiteName results
                allSuiteResults <- suiteResult :: allSuiteResults
                printSuiteResult suiteResult
            with
            | ex ->
                printfn "❌ Test suite '%s' failed with exception: %s" suiteName ex.Message
                let failedResult = createSuiteResult suiteName [{
                    TestName = "Suite Execution"
                    Success = false
                    Message = ex.Message
                    Duration = TimeSpan.Zero
                }]
                allSuiteResults <- failedResult :: allSuiteResults
        
        let overallDuration = DateTime.Now - overallStartTime
        let allSuiteResults = List.rev allSuiteResults
        
        // Calculate overall results
        let totalTests = allSuiteResults |> List.sumBy (fun s -> s.TotalTests)
        let totalPassed = allSuiteResults |> List.sumBy (fun s -> s.PassedTests)
        let totalFailed = allSuiteResults |> List.sumBy (fun s -> s.FailedTests)
        let successRate = if totalTests > 0 then float totalPassed / float totalTests * 100.0 else 0.0
        
        let overallResult = {
            SuiteResults = allSuiteResults
            TotalTests = totalTests
            TotalPassed = totalPassed
            TotalFailed = totalFailed
            TotalDuration = overallDuration
            SuccessRate = successRate
        }
        
        printOverallResult overallResult
        
        return overallResult
    }
    
    let runSpecificSuite (suiteName: string) = async {
        printfn "Running %s..." suiteName
        
        let testFunc =
            match suiteName.ToLower() with
            | "document" | "documentprocessor" -> Some DocumentProcessorTests.runAllTests
            | "vector" | "vectoroperations" -> Some VectorOperationsTests.runAllTests
            | "search" | "searchengine" -> Some SearchEngineTests.runAllTests
            | "semantic" | "semanticembedding" -> Some SemanticEmbeddingTests.runAllTests
            | "crossplatform" | "platform" -> Some CrossPlatformTests.runAllTests
            | "performance" | "perf" -> Some PerformanceTests.runAllTests
            | "error" | "errorhandling" -> Some ErrorHandlingTests.runAllTests
            | "e2e" | "endtoend" -> Some EndToEndTests.runAllTests
            | _ -> None
        
        match testFunc with
        | Some func ->
            let! results = func()
            let suiteResult = createSuiteResult suiteName results
            printSuiteResult suiteResult
            return Some suiteResult
        | None ->
            printfn "❌ Unknown test suite: %s" suiteName
            printfn "Available suites: document, vector, search, semantic, crossplatform, performance, error, e2e"
            return None
    }
