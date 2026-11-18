# Repository layout

## Maintained files

`Brisa.slnx` groups the application, Core library, bridge, setup tool and checks. The root contains solution/build configuration, licensing and community documents, with no operational PowerShell scripts.

```text
src/
  Brisa.Core/
    Applications/ Configuration/ Deployment/ Dock/
    Infrastructure/ Productivity/ System/ Usage/
  Brisa.WinUI/
    App.xaml, MainWindow.xaml
    Assets/ Properties/
    Diagnostics/ Dock/ Features/ Platform/ UI/
  Brisa.Bridge/
tools/Brisa.Setup/
scripts/
  Build-Brisa.ps1, Start-Brisa.ps1
  Prepare-Package.ps1, New-ReleasePackage.ps1
  Get-ReleaseNotes.ps1, Test-Repository.ps1
  deployment/
    Common.ps1, Invoke-Maintenance.ps1
    Install-Brisa.ps1, Update-Brisa.ps1
    Restore-Brisa.ps1, Uninstall-Brisa.ps1
tests/
docs/
```

WinUI features are grouped into Applications, Appearance, Maintenance, Productivity, Settings, System and Usage. Shared UI belongs in `UI`, native adapters in `Platform`, and the dock's interaction code in `Dock`. WinUI feature files contribute to the same partial `MainWindow`; moving a file does not create a new runtime component or settings identity.

Core folders group related pure models and services. They retain the `Brisa.Core` namespace. The application uses `Brisa.WinUI`; the executable name remains `BrisaDock.exe`.

## Generated and private directories

| Path | Purpose | May contain user data? |
| --- | --- | --- |
| `artifacts/build/` | Default isolated build and prepared package | Only when deliberately running sample review fixtures |
| `artifacts/release-build-*/` | Fresh build used to create a candidate | No personal data allowed |
| `artifacts/releases/<version>/` | Installer, ZIP, release notes and checksums | No |
| `local/app/` | Optional personal installation | Yes: `data/` and `recovery/` |
| `qa/` | Local test results and isolated install fixtures | Possibly; never publish wholesale |
| `**/bin/`, `**/obj/` | SDK build intermediates | Do not publish |

All these paths are ignored by Git and rejected by the publication audit. Never compile over `local/app` or package it. A personal installation may retain legacy maintenance file names until it is upgraded; those are installed-version files, not maintained source.

Legacy prototypes and historical evidence were archived outside the repository during local cleanup. The current checkout does not depend on that archive. Future cleanup may remove generated files after review; user data and recovery records are not disposable build output.

## Package layout

A release ZIP contains `BrisaDock.exe`, its runtimes/assets, `Install-Brisa.cmd`, `brisa-package.json`, notices and `maintenance/`. The latter contains the six English-named deployment scripts copied from `scripts/deployment/`.

Run the root CMD launcher or pass the package root explicitly to `maintenance/Install-Brisa.ps1`. Updates, rollback and uninstall resolve their installation root as the parent of `maintenance/`. Keep packaging, setup, the WinUI maintenance launcher and their checks aligned whenever this layout changes.
