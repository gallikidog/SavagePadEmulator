# Release guide

## Local portable build

```powershell
.\scripts\Publish-Portable.ps1 -Version 1.0.3
```

Output:

```text
artifacts\SavagePadEmulator-1.0.3-portable-win-x64.zip
```

## Local installer build

1. Install **Inno Setup 6**.
2. Run:

```powershell
.\Installer\Build-Installer.ps1
```

Output:

```text
artifacts\SavagePadEmulator-Setup-1.0.3-win-x64.exe
```

The installer intentionally does not bundle ViGEmBus. The emulator needs ViGEmBus installed separately to create the virtual Xbox controller.

## GitHub Release

After testing and committing everything:

```powershell
git add .
git commit -m "v1.0.3 - Add portable package, installer and release workflow"
git push origin main
git tag -a v1.0.3 -m "SavagePadEmulator v1.0.3"
git push origin v1.0.3
```

Pushing the tag starts the GitHub Actions workflow. It builds the portable ZIP and Inno Setup installer, then attaches both to the GitHub Release page.
