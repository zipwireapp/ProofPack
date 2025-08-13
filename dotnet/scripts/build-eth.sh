#!/bin/bash

set -e

PROJECT_PATH="src/Zipwire.ProofPack.Ethereum/Zipwire.ProofPack.Ethereum.csproj"

# Check if the script is run from the dotnet directory
if [ ! -f "Zipwire.ProofPack.sln" ]; then
  echo "Error: Solution file 'Zipwire.ProofPack.sln' not found."
  echo "Please run this script from the 'dotnet' directory."
  exit 1
fi

mkdir -p ./artifacts

echo "Building Ethereum package..."
dotnet build -c Release "$PROJECT_PATH"

echo "Running tests for Ethereum package..."
dotnet test -c Release tests/Zipwire.ProofPack.Ethereum.Tests/Zipwire.ProofPack.Ethereum.Tests.csproj

echo "Packing Ethereum package..."
dotnet pack -c Release "$PROJECT_PATH" -o ./artifacts

echo "Ethereum package build and pack completed! Package is in ./artifacts" 