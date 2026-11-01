# HSDRaw source provenance

Upstream: https://github.com/Ploaj/HSDLib
Revision: 85567e40797de3c820a55476eca54c704921a848
License: MIT, copyright Ploaj; see LICENSE.
Imported: HSDRaw C# source and project only. No upstream application shell,
compiled binaries, assets or HSDRawViewer dependencies are included.
Upstream project targets netstandard2.0 and references Microsoft.Windows.Compatibility 8.0.30.
Native HSD resource graphs remain authoritative; display projections are independent.

Fighterport builds the unchanged sources through HSDRaw.Native.csproj targeting
net472 with framework System.Drawing. This avoids the upstream application-oriented
Windows.Compatibility package and adds no native DLL/runtime dependency.
