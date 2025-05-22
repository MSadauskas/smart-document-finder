#!/bin/bash

echo "🧪 Testing Semantic Search Intelligence"
echo "======================================="

cd /Users/mikas/Development/smart-document-finder

# Test 1: Machine Learning (should find ml-doc.txt)
echo -e "\n🔍 Test 1: 'machine learning'"
echo "Expected: Should find ML document"

# Test 2: Python Programming (should find python-tutorial.txt)  
echo -e "\n🔍 Test 2: 'python programming'"
echo "Expected: Should find Python tutorial"

# Test 3: Financial Report (should find business-report.txt)
echo -e "\n🔍 Test 3: 'financial report'"
echo "Expected: Should find business document"

# Test 4: Software Development (should find software-doc.txt)
echo -e "\n🔍 Test 4: 'software development'"
echo "Expected: Should find software engineering document"

echo -e "\n✅ All tests defined. Run individual queries to test semantic matching."
