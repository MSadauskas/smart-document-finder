# Cross-Platform Compatibility Guide

## ✅ Verified Platforms

### macOS (Current Test)
- **Status**: ✅ Fully Working
- **Database**: `/Users/username/.smartdoc/`
- **Platform**: Unix (64-bit)

### Linux (Original Development) 
- **Status**: ✅ Fully Working  
- **Database**: `/home/username/.smartdoc/`
- **Platform**: Unix (64-bit)

### Windows (Expected)
- **Status**: 🔄 Should Work (untested)
- **Database**: `C:\Users\username\.smartdoc\`
- **Platform**: Win32NT (64-bit)

## 🔧 Cross-Platform Features

### Automatic Path Resolution
```fsharp
// Automatically resolves to correct platform path:
let dbPath = CrossPlatform.getDefaultDatabasePath()
// Linux:   /home/user/.smartdoc/documents.db
// macOS:    /Users/user/.smartdoc/documents.db  
// Windows:  C:\Users\user\.smartdoc\documents.db
```

### Directory Creation
```fsharp
// Works on all platforms:
CrossPlatform.ensureDirectoryExists(dbPath)
```

### Platform Detection
```fsharp
// Runtime platform info:
CrossPlatform.getPlatformInfo()
// "Platform: Unix (64-bit), User: mikas"
```

## 🧪 Testing Instructions

### Windows Testing
```powershell
# Clone and test on Windows:
git clone https://github.com/MSadauskas/smart-document-finder.git
cd smart-document-finder
dotnet run --project SmartDocumentFinder.Test
```

**Expected Windows Output**:
```
🖥️ Platform: Win32NT (64-bit), User: YourUsername
📁 Database: C:\Users\YourUsername\.smartdoc\test.db
📂 Test docs: C:\path\to\smart-document-finder\test-docs
```

### Linux Testing
```bash
# Already tested - known working
git clone https://github.com/MSadauskas/smart-document-finder.git
cd smart-document-finder  
dotnet run --project SmartDocumentFinder.Test
```

## 🔧 Windows-Specific Considerations

### File Paths
- ✅ Uses `Path.Combine()` for correct separators
- ✅ Handles `C:\` drive letters
- ✅ Works with Windows user directories

### Permissions
- ✅ `.smartdoc` folder created in user directory (no admin needed)
- ✅ SQLite database works on all filesystems
- ✅ Document scanning respects Windows file permissions

### Dependencies
- ✅ .NET 9 runs natively on Windows
- ✅ SQLite embedded - no external dependencies
- ✅ Avalonia UI fully Windows compatible

## 🚨 Known Limitations

### PDF Processing
- ❌ One PDF fails with "stack error" (platform-independent issue)
- ✅ Most PDFs process correctly
- 🔧 **Fix**: Improve PDF error handling

### Document Formats
- ✅ Text files: Universal support
- ✅ PDF files: Cross-platform (minor bugs)
- ✅ Word files: Cross-platform via OpenXML

## 🎯 Deployment Targets

### Single-File Executables
```bash
# Create platform-specific executables:

# Windows
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true

# macOS  
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true

# Linux
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

## ✅ Verification Checklist

**Required for Full Cross-Platform Support**:
- ✅ Database paths resolve correctly
- ✅ Document processing works
- ✅ Directory creation succeeds  
- ✅ File scanning operates
- ✅ Search functionality active
- ✅ No hardcoded paths remain
- 🔄 Windows testing needed

**Next Steps**:
1. Test on Windows machine
2. Create release binaries for all platforms
3. Document any Windows-specific issues
