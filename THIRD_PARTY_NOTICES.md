# Third-party resources and dependencies

Brisa's MIT license covers its original code and does not change rights in trademarks, assets or dependencies.

- Ical.Net 5.2.4: MIT; license text in `src/Brisa.WinUI/Assets/Licenses/Ical.Net.txt`.
- Noda Time: Apache-2.0 license and NOTICE in the same directory; a transitive calendar dependency.
- .NET, Windows App SDK and Windows SDK: Microsoft components distributed under their package terms. Keep the notices included in official .NET publish output and consult the projects' NuGet metadata.
- OpenAI symbol: identifies the service and is not covered by Brisa's MIT license. See `docs/ASSETS.md` for provenance; no sponsorship or affiliation is implied.
- Other application icons: read from installed applications rather than bundled as independent assets. Sample screenshots may show those icons.
- Segoe symbols and desktop effects come from Windows and are not redistributed as original fonts.

Inspect transitive packages with `dotnet list src/Brisa.WinUI/Brisa.WinUI.csproj package --include-transitive`. Preserve license texts in release packages and review the provenance of every new asset before including it.
