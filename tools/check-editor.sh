#!/usr/bin/env bash
# Compile-checks the Editor-only scripts against Unity assemblies + an Editor API stub.
set -e
export DOTNET_ROOT=/home/user/.dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
cd "$(dirname "$0")/compile-check"
cp Elementa.CompileCheck.csproj Elementa.EditorCheck.csproj
trap "rm -f Elementa.EditorCheck.csproj" EXIT
python3 - <<'PY'
p="Elementa.EditorCheck.csproj"
s=open(p).read()
s=s.replace("<AssemblyName>Assembly-CSharp</AssemblyName>","<AssemblyName>Assembly-CSharp-Editor</AssemblyName>\n    <OutputPath>bin/editor/</OutputPath>")
s=s.replace('<Compile Include="../../Assets/Scripts/**/*.cs" Exclude="../../Assets/Scripts/Editor/**/*.cs" />',
            '<Compile Include="../../Assets/Scripts/**/*.cs" />\n    <Compile Include="/home/user/tools/editorstub/UnityEditorStub.cs" />')
s=s.replace("<DefineConstants>UNITY_STANDALONE;UNITY_2021_3_OR_NEWER</DefineConstants>","<DefineConstants>UNITY_STANDALONE;UNITY_EDITOR;UNITY_2021_3_OR_NEWER</DefineConstants>")
open(p,"w").write(s)
PY
/home/user/.dotnet/dotnet build Elementa.EditorCheck.csproj -nologo -v q -p:UnityRefs="${UnityRefs:-/home/user/tools/unityref}" 2>&1 | grep -E "error|Build succeeded" | head -40
