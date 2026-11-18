#!/usr/bin/env bash
# Compiles all gameplay scripts against real Unity assemblies to catch errors early.
set -e
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export UnityRefs="${UnityRefs:-/home/user/tools/unityref}"
export DOTNET_ROOT=/home/user/.dotnet
cd "$(dirname "$0")/compile-check"
/home/user/.dotnet/dotnet build -nologo -v q -p:UnityRefs="$UnityRefs" 2>&1 | grep -E "error|Error|Build succeeded" | head -80
