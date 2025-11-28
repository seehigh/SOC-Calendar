#!/bin/bash
# Run EF Core migrations for Railway deployment

echo "Installing EF Core CLI..."
dotnet tool install --global dotnet-ef

echo "Running database migrations..."
dotnet ef database update

echo "✅ Migrations completed successfully!"
