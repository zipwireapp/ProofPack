#!/bin/bash

set -e

PROJECT_PATH="src/Zipwire.ProofPack/Zipwire.ProofPack.csproj"

# Check if the script is run from the dotnet directory
if [ ! -f "Zipwire.ProofPack.sln" ]; then
  echo "Error: Solution file 'Zipwire.ProofPack.sln' not found."
  echo "Please run this script from the 'dotnet' directory."
  exit 1
fi

mkdir -p ./artifacts

echo "Building base package..."
dotnet build -c Release "$PROJECT_PATH"

echo "Running tests for base package..."
dotnet test -c Release tests/Zipwire.ProofPack.Tests/Zipwire.ProofPack.Tests.csproj

echo "Packing base package..."
dotnet pack -c Release "$PROJECT_PATH" -o ./artifacts

echo "Base package build and pack completed! Package is in ./artifacts" 