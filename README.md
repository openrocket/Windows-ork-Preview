# windows-ork-preview

A Windows shell extension that provides thumbnail previews for `.ork` (OpenRocket Design Document) files in Windows Explorer. Designed to be bundled with the OpenRocket installer via install4j.

`.ork` files are ZIP archives containing a `preview.png` image. This extension extracts that image and displays it as the file's thumbnail in Explorer — in icon view, the Details pane, and file-open dialogs.

![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-blue)
![Windows](https://img.shields.io/badge/platform-Windows%2010%2F11-0078d4)

## How it works

1. Windows Explorer encounters a `.ork` file and asks the registered thumbnail handler for an image.
2. The handler opens the `.ork` file as a ZIP archive.
3. It extracts `preview.png` from the root of the archive.
4. It returns the image as a bitmap, scaled to the requested thumbnail size.

## Project structure

```
windows-ork-preview/
├── README.md
├── src/
│   └── OrkThumbnailHandler/
│       ├── OrkThumbnailHandler.csproj
│       ├── OrkThumbnailHandler.cs      # The thumbnail provider implementation
│       └── Properties/
│           └── AssemblyInfo.cs
└── scripts/
    ├── register.ps1                    # Register the shell extension (for local testing)
    ├── unregister.ps1                  # Unregister the shell extension (for local testing)
    └── restart-explorer.ps1            # Restart Explorer to pick up changes
```

## Prerequisites

- Windows 10 or 11 (64-bit)
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) (pre-installed on Windows 10 1903+ and Windows 11)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (Community edition is fine) with the **.NET desktop development** workload, and the [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48) — for building from source

## Building

Restore NuGet packages first, then build:

```powershell
cd src\OrkThumbnailHandler
msbuild OrkThumbnailHandler.csproj /t:Restore
msbuild OrkThumbnailHandler.csproj /p:Configuration=Release /p:Platform=x64
```

The output DLL will be in `src\OrkThumbnailHandler\bin\x64\Release\`.

## Local testing

For development and testing, you can register the handler manually without going through the installer. These scripts are **not** used in the install4j build — they're just for convenience during development.

1. Build the project (see above).
2. Open an **elevated PowerShell** prompt and run:

```powershell
.\scripts\register.ps1 -DllPath "C:\full\path\to\bin\x64\Release\OrkThumbnailHandler.dll"
```

3. Restart Explorer:

```powershell
.\scripts\restart-explorer.ps1
```

4. To unregister:

```powershell
.\scripts\unregister.ps1 -DllPath "C:\full\path\to\bin\x64\Release\OrkThumbnailHandler.dll"
.\scripts\restart-explorer.ps1
```

> **Note:** Don't move the DLL after registering — `regasm /codebase` embeds the absolute path in the registry.

## install4j integration

This is how the shell extension is deployed to end users as part of the OpenRocket installer.

### 1. Add the shell extension files to the installer

In your install4j project, add a **file set** or include the following files from the build output:

```
OrkThumbnailHandler.dll
SharpShell.dll
```

Set the destination to a subdirectory of the installation directory, e.g.:

```
${installer:sys.installationDir}/shell-extension/
```

### 2. Register on install

Add a **"Run PowerShell script"** action in your **Install** action sequence, **after** file extraction:

```powershell
$regasm = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
$dll = "${installer:sys.installationDir}\shell-extension\OrkThumbnailHandler.dll"

if (Test-Path $regasm) {
    & $regasm /codebase $dll 2>&1
}
```

Action settings:
- **Run as:** Administrator (the installer should already be elevated)
- **Failure strategy:** Report warning and continue — the installer should succeed even if registration fails (e.g. on a non-standard Windows installation)
- **Execute on:** Install only (not on update — see below)

### 3. Unregister on uninstall

Add a **"Run PowerShell script"** action **early** in the **Uninstall** action sequence, **before** file deletion:

```powershell
$regasm = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
$dll = "${installer:sys.installationDir}\shell-extension\OrkThumbnailHandler.dll"

if ((Test-Path $regasm) -and (Test-Path $dll)) {
    & $regasm /unregister $dll 2>&1
}
```

### 4. Re-register on update

When the user updates OpenRocket, the DLL may have changed. Add a **"Run PowerShell script"** action in the **Update** sequence that re-registers:

```powershell
$regasm = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\regasm.exe"
$dll = "${installer:sys.installationDir}\shell-extension\OrkThumbnailHandler.dll"

if (Test-Path $regasm) {
    & $regasm /codebase $dll 2>&1
}
```

### 5. Notify the shell

After registration (or unregistration), add a **"Run PowerShell script"** action to notify Explorer that file associations have changed. This is a gentler alternative to killing and restarting Explorer — it tells the shell to reload its extension cache without disrupting the user's open windows.

```powershell
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class Shell {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
"@

# SHCNE_ASSOCCHANGED = 0x08000000, SHCNF_IDLIST = 0x0000
[Shell]::SHChangeNotify(0x08000000, 0x0000, [IntPtr]::Zero, [IntPtr]::Zero)
```

### install4j notes

- **64-bit only:** The handler DLL must be registered with the 64-bit regasm (`Framework64`). Explorer on 64-bit Windows is a 64-bit process and will not load 32-bit shell extensions.
- **Don't move the DLL after registration:** `regasm /codebase` embeds the DLL's absolute path in the registry. This is fine for installers since the install directory is stable.
- **Silent installs:** The PowerShell scripts work fine in silent/unattended mode. No user interaction is needed.
- **Rollback:** If registration fails during install, OpenRocket itself still works — the user just won't get thumbnails. Log the failure but don't fail the install.

## Troubleshooting

**Thumbnails not showing after installation:**
- Clear the thumbnail cache: run `cleanmgr` → select "Thumbnails" and clean.
- Log off and back on, or restart Explorer.
- Verify the DLL is in the location it was registered from — it must not have been moved.

**Thumbnails show for some .ork files but not others:**
- The file may not contain a `preview.png` at the ZIP root. The handler gracefully returns no thumbnail in that case.

**Build fails with "reference assemblies for .NETFramework,Version=v4.8 were not found":**
- Install the [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48).

**Build fails with "SharpShell could not be found":**
- Run `msbuild OrkThumbnailHandler.csproj /t:Restore` before building to restore the NuGet package.

## License

MIT
