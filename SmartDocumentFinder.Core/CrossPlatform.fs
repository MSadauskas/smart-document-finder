namespace SmartDocumentFinder.Core
open System
open System.IO

module CrossPlatform =
    
    /// Get the user's data directory for the application
    let getAppDataDirectory () =
        let userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        Path.Combine(userProfile, ".smartdoc")
    
    /// Get the default database path for the current platform
    let getDefaultDatabasePath () =
        let appDir = getAppDataDirectory()
        Path.Combine(appDir, "documents.db")
    
    /// Get the test database path
    let getTestDatabasePath () =
        let appDir = getAppDataDirectory()
        Path.Combine(appDir, "test.db")
    
    /// Ensure a directory exists, create if necessary
    let ensureDirectoryExists (path: string) =
        let dir = Path.GetDirectoryName(path)
        if not (Directory.Exists(dir)) then
            Directory.CreateDirectory(dir) |> ignore
    
    /// Get the test documents folder relative to current directory
    let getTestDocsPath () =
        let currentDir = Directory.GetCurrentDirectory()
        Path.Combine(currentDir, "test-docs")
    
    /// Get platform info for debugging
    let getPlatformInfo () =
        let os = Environment.OSVersion.Platform.ToString()
        let arch = Environment.Is64BitOperatingSystem
        let user = Environment.UserName
        sprintf "Platform: %s (%s-bit), User: %s" os (if arch then "64" else "32") user
