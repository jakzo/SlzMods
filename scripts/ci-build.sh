#!/bin/bash

set -eux

configuration=${1:-Debug}

find . -name "*.csproj" | while read csproj; do
    if grep -q '<TargetFrameworkVersion>v4\.' "$csproj"; then
        msbuild "$csproj" "/p:Configuration=$configuration"
    else
        dotnet build "$csproj" --configuration "$configuration"
    fi
done
