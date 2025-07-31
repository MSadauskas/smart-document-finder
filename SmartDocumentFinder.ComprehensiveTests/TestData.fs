namespace SmartDocumentFinder.ComprehensiveTests

open System
open System.IO
open SmartDocumentFinder.Core

module TestData =
    
    // Test document content for different domains
    let machineLearningContent = """
    Machine Learning and Artificial Intelligence
    
    This document covers fundamental concepts in machine learning including:
    - Neural networks and deep learning algorithms
    - Supervised and unsupervised learning techniques
    - Data preprocessing and feature engineering
    - Model training, validation, and testing procedures
    - Performance metrics and evaluation methods
    
    Key topics include regression, classification, clustering, and reinforcement learning.
    Popular frameworks like TensorFlow, PyTorch, and scikit-learn are discussed.
    """
    
    let pythonProgrammingContent = """
    Python Programming Tutorial
    
    This comprehensive guide covers Python programming fundamentals:
    - Variables, data types, and control structures
    - Functions, classes, and object-oriented programming
    - File handling and exception management
    - Libraries and modules for development
    - Code organization and best practices
    
    Advanced topics include decorators, generators, context managers, and async programming.
    Popular libraries like NumPy, Pandas, and Matplotlib are covered.
    """
    
    let businessReportContent = """
    Quarterly Business Report
    
    Financial Performance Summary:
    - Revenue increased by 15% compared to last quarter
    - Profit margins improved due to cost optimization
    - Investment in new market segments showing positive returns
    - Cash flow remains strong with healthy reserves
    
    Market Analysis:
    - Customer acquisition costs decreased by 8%
    - Market share expanded in key demographics
    - Competitive positioning strengthened through innovation
    - Strategic partnerships driving growth initiatives
    """
    
    let softwareDocContent = """
    Software Development Documentation
    
    System Architecture Overview:
    - Microservices architecture with containerized deployment
    - RESTful APIs for service communication
    - Database design with normalized schemas
    - Caching strategies for performance optimization
    
    Development Process:
    - Agile methodology with sprint planning
    - Code review and quality assurance procedures
    - Continuous integration and deployment pipelines
    - Testing strategies including unit, integration, and end-to-end tests
    """
    
    let academicResearchContent = """
    Academic Research Paper
    
    Abstract:
    This study investigates the correlation between environmental factors and productivity.
    The research methodology includes statistical analysis of collected data samples.
    
    Introduction:
    Previous studies have shown various factors affecting workplace productivity.
    This research aims to provide comprehensive analysis and recommendations.
    
    Methodology:
    - Data collection from multiple sources
    - Statistical analysis using regression models
    - Peer review and validation processes
    - Ethical considerations and compliance
    """
    
    // Create test documents
    let createTestDocument (content: string) (filename: string) : Document =
        let docId = DocumentId (Guid.NewGuid())
        let docPath = DocumentPath filename
        let metadata = {
            Id = docId
            Path = docPath
            FileName = Path.GetFileName(filename)
            Size = int64 content.Length
            Created = DateTime.Now.AddDays(-1.0)
            Modified = DateTime.Now
            Format = PlainText
            Hash = DocumentHash (content.GetHashCode().ToString())
        }
        { Metadata = metadata; State = NotProcessed }
    
    // Test document collection
    let getTestDocuments () =
        [
            (machineLearningContent, "ml-document.txt")
            (pythonProgrammingContent, "python-tutorial.txt")
            (businessReportContent, "business-report.txt")
            (softwareDocContent, "software-documentation.txt")
            (academicResearchContent, "academic-research.txt")
        ]
        |> List.map (fun (content, filename) -> (content, createTestDocument content filename))
    
    // Test queries for different domains
    let getTestQueries () = [
        ("machine learning", ["ml-document.txt"])
        ("python programming", ["python-tutorial.txt"])
        ("financial report", ["business-report.txt"])
        ("software development", ["software-documentation.txt"])
        ("academic research", ["academic-research.txt"])
        ("artificial intelligence", ["ml-document.txt"])
        ("business revenue", ["business-report.txt"])
        ("code review", ["software-documentation.txt"])
        ("data analysis", ["ml-document.txt"; "academic-research.txt"])
        ("programming tutorial", ["python-tutorial.txt"])
    ]
    
    // Create test files in a temporary directory
    let createTestFiles (testDir: string) =
        if not (Directory.Exists(testDir)) then
            Directory.CreateDirectory(testDir) |> ignore
        
        let testDocs = getTestDocuments()
        for (content, doc) in testDocs do
            let (DocumentPath filePath) = doc.Metadata.Path
            let fullPath = Path.Combine(testDir, Path.GetFileName(filePath))
            File.WriteAllText(fullPath, content)
        
        testDocs |> List.map snd
    
    // Clean up test files
    let cleanupTestFiles (testDir: string) =
        if Directory.Exists(testDir) then
            Directory.Delete(testDir, true)
