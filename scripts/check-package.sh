#!/usr/bin/env bash
# Packs the SDK, checks what the packages contain, and runs consumers that
# install them from a local feed the way an application would.
set -euo pipefail
cd "$(dirname "$0")/.."

version=$(dotnet msbuild src/Synthient.Client -getProperty:Version)
rm -rf artifacts/packages consumer-test/packages consumer-test/*/bin consumer-test/*/obj
dotnet pack Synthient.slnx -c Release -o artifacts/packages

for package in Synthient.Client Synthient.Client.GrpcSchema; do
  nupkg="artifacts/packages/$package.$version.nupkg"
  files=$(unzip -Z1 "$nupkg")
  for expected in "lib/net8.0/$package.dll" "lib/net8.0/$package.xml" "lib/net10.0/$package.dll" "lib/net10.0/$package.xml" README.md icon.png; do
    if ! grep -qx "$expected" <<<"$files"; then
      echo "$nupkg is missing $expected" >&2
      exit 1
    fi
  done
  test -f "artifacts/packages/$package.$version.snupkg"
done

# System.Text.Json is a dependency only on net8.0, where it brings the strict
# nullable handling that .NET 10 has built in.
unzip -p "artifacts/packages/Synthient.Client.$version.nupkg" Synthient.Client.nuspec | python3 -c '
import sys, xml.etree.ElementTree as ET
ns = {"n": "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"}
groups = {g.get("targetFramework"): [d.get("id") for d in g.findall("n:dependency", ns)]
          for g in ET.parse(sys.stdin).getroot().findall(".//n:dependencies/n:group", ns)}
expected = {"net8.0": ["System.Text.Json"], "net10.0": []}
if groups != expected:
    sys.exit(f"unexpected dependency groups: {groups}")
'

for framework in net8.0 net10.0; do
  dotnet run --project consumer-test/Consumer -f "$framework" -p:SynthientVersion="$version"
done

dotnet publish consumer-test/AotConsumer -c Release -p:SynthientVersion="$version" -o consumer-test/AotConsumer/bin/publish
consumer-test/AotConsumer/bin/publish/AotConsumer
