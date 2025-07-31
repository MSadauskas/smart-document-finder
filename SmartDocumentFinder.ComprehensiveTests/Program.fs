open System
open SmartDocumentFinder.ComprehensiveTests

[<EntryPoint>]
let main argv =
    printfn "Smart Document Finder - Comprehensive Test Suite"
    printfn "================================================"
    
    let runTests () = async {
        match argv with
        | [||] ->
            // Run all test suites
            let! overallResult = TestRunner.runAllTestSuites()
            return if overallResult.TotalFailed = 0 then 0 else 1
            
        | [| suiteName |] ->
            // Run specific test suite
            let! suiteResult = TestRunner.runSpecificSuite suiteName
            match suiteResult with
            | Some result -> return if result.FailedTests = 0 then 0 else 1
            | None -> return 1
            
        | [| "--help" |] | [| "-h" |] ->
            printfn ""
            printfn "Usage:"
            printfn "  dotnet run                    # Run all test suites"
            printfn "  dotnet run <suite>           # Run specific test suite"
            printfn "  dotnet run --help            # Show this help"
            printfn ""
            printfn "Available test suites:"
            printfn "  document        Document processor tests"
            printfn "  vector          Vector operations tests"
            printfn "  search          Search engine tests"
            printfn "  semantic        Semantic embedding tests"
            printfn "  crossplatform   Cross-platform compatibility tests"
            printfn "  performance     Performance and scalability tests"
            printfn "  error           Error handling tests"
            printfn "  e2e             End-to-end integration tests"
            printfn ""
            return 0
            
        | _ ->
            printfn "❌ Invalid arguments. Use --help for usage information."
            return 1
    }
    
    try
        runTests () |> Async.RunSynchronously
    with
    | ex ->
        printfn ""
        printfn "❌ Test execution failed with exception:"
        printfn "%s" ex.Message
        printfn ""
        printfn "Stack trace:"
        printfn "%s" ex.StackTrace
        1
