namespace SmartDocumentFinder.ComprehensiveTests

open System
open System.IO
open SmartDocumentFinder.Core

module CrossPlatformTests =
    
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
    
    let testPlatformDetection () = async {
        let platformInfo = CrossPlatform.getPlatformInfo()
        
        let isValidPlatform = 
            platformInfo.Contains("Windows") || 
            platformInfo.Contains("Unix") || 
            platformInfo.Contains("Linux") ||
            platformInfo.Contains("macOS")
        
        if isValidPlatform then
            return (true, $"✅ Platform detected: {platformInfo}")
        else
            return (false, $"❌ Unknown platform: {platformInfo}")
    }
    
    let testDatabasePathResolution () = async {
        let dbPath = CrossPlatform.getDefaultDatabasePath()
        let testDbPath = CrossPlatform.getTestDatabasePath()
        
        let isValidDbPath = 
            dbPath.Contains(".smartdoc") && 
            (dbPath.Contains("Users") || dbPath.Contains("home") || dbPath.Contains("Documents"))
        
        let isValidTestPath = 
            testDbPath.Contains(".smartdoc") && 
            testDbPath.Contains("test.db")
        
        if isValidDbPath && isValidTestPath then
            return (true, $"✅ DB paths: {Path.GetDirectoryName(dbPath)} | {Path.GetDirectoryName(testDbPath)}")
        else
            return (false, $"❌ Invalid paths: db={dbPath}, test={testDbPath}")
    }
    
    let testDirectoryCreation () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_crossplatform_test_" + Guid.NewGuid().ToString("N")[..7])
        let testDbPath = Path.Combine(testDir, "test.db")
        
        try
            // Test directory creation
            CrossPlatform.ensureDirectoryExists(testDbPath)
            
            let directoryExists = Directory.Exists(Path.GetDirectoryName(testDbPath))
            
            // Cleanup
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            
            if directoryExists then
                return (true, $"✅ Directory creation successful: {testDir}")
            else
                return (false, $"❌ Directory creation failed: {testDir}")
        with
        | ex ->
            // Cleanup on error
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Directory creation exception: {ex.Message}")
    }
    
    let testPathNormalization () = async {
        let testPaths = [
            "/unix/style/path"
            "\\windows\\style\\path"
            "mixed/style\\path"
            "relative/path"
            "./current/directory"
            "../parent/directory"
        ]
        
        let mutable allSuccess = true
        let mutable messages = []
        
        for testPath in testPaths do
            try
                let normalizedPath = Path.GetFullPath(testPath)
                let isValid = not (String.IsNullOrEmpty(normalizedPath))
                
                if isValid then
                    messages <- $"✅ '{testPath}' → valid path" :: messages
                else
                    allSuccess <- false
                    messages <- $"❌ '{testPath}' → invalid" :: messages
            with
            | ex ->
                // Some paths might be invalid on certain platforms, which is expected
                messages <- $"⚠️  '{testPath}' → {ex.GetType().Name}" :: messages
        
        return (allSuccess, String.Join("\n", List.rev messages))
    }
    
    let testFileOperations () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_fileops_test_" + Guid.NewGuid().ToString("N")[..7])
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            
            // Test file creation
            let testFile = Path.Combine(testDir, "test.txt")
            let testContent = "Cross-platform test content"
            File.WriteAllText(testFile, testContent)
            
            // Test file reading
            let readContent = File.ReadAllText(testFile)
            let contentMatches = readContent = testContent
            
            // Test file existence
            let fileExists = File.Exists(testFile)
            
            // Test file deletion
            File.Delete(testFile)
            let fileDeleted = not (File.Exists(testFile))
            
            // Cleanup
            Directory.Delete(testDir, true)
            
            if contentMatches && fileExists && fileDeleted then
                return (true, $"✅ File operations: create, read, delete successful")
            else
                return (false, $"❌ File operations failed: content={contentMatches}, exists={fileExists}, deleted={fileDeleted}")
        with
        | ex ->
            // Cleanup on error
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ File operations exception: {ex.Message}")
    }
    
    let testDatabaseCompatibility () = async {
        let testDir = Path.Combine(Path.GetTempPath(), "sdf_db_test_" + Guid.NewGuid().ToString("N")[..7])
        let dbPath = Path.Combine(testDir, "test.db")
        
        try
            Directory.CreateDirectory(testDir) |> ignore
            
            // Test database initialization
            match! Database.initializeDatabase(dbPath) with
            | Ok _ ->
                let dbExists = File.Exists(dbPath)
                
                // Cleanup
                Directory.Delete(testDir, true)
                
                if dbExists then
                    return (true, $"✅ Database compatibility: SQLite works on this platform")
                else
                    return (false, $"❌ Database file not created")
            | Error err ->
                // Cleanup
                if Directory.Exists(testDir) then
                    Directory.Delete(testDir, true)
                return (false, $"❌ Database initialization failed: {err}")
        with
        | ex ->
            // Cleanup on error
            if Directory.Exists(testDir) then
                Directory.Delete(testDir, true)
            return (false, $"❌ Database compatibility exception: {ex.Message}")
    }
    
    let testEnvironmentVariables () = async {
        let homeVar = Environment.GetEnvironmentVariable("HOME")
        let userProfileVar = Environment.GetEnvironmentVariable("USERPROFILE")
        let userVar = Environment.GetEnvironmentVariable("USER")
        let usernameVar = Environment.GetEnvironmentVariable("USERNAME")
        
        let hasHomeInfo = 
            not (String.IsNullOrEmpty(homeVar)) || 
            not (String.IsNullOrEmpty(userProfileVar))
        
        let hasUserInfo = 
            not (String.IsNullOrEmpty(userVar)) || 
            not (String.IsNullOrEmpty(usernameVar))
        
        if hasHomeInfo && hasUserInfo then
            return (true, $"✅ Environment variables available")
        else
            return (false, $"❌ Missing environment variables: home={hasHomeInfo}, user={hasUserInfo}")
    }
    
    let runAllTests () = async {
        let tests = [
            ("Platform Detection", testPlatformDetection)
            ("Database Path Resolution", testDatabasePathResolution)
            ("Directory Creation", testDirectoryCreation)
            ("Path Normalization", testPathNormalization)
            ("File Operations", testFileOperations)
            ("Database Compatibility", testDatabaseCompatibility)
            ("Environment Variables", testEnvironmentVariables)
        ]
        
        let mutable results = []
        for (testName, testFunc) in tests do
            let! result = runTest testName testFunc
            results <- result :: results
            
        return List.rev results
    }
