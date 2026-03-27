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

Go to **Files** → **Define Distribution Tree** in the install4j sidebar. Click the **+** button to add files and directories. In the wizard:

1. Select the `Windows_resources directory` which contains `OrkThumbnailHandler.dll` and `SharpShell.dll`.
2. Choose **"Add to subdirectory"** and enter `shell-extension`.
3. Click **Finish**.

This places the DLLs at `${installer:sys.installationDir}\shell-extension\` on the user's machine.

### 2. Add a user opt-out checkbox

The thumbnail handler is enabled by default, but users can opt out during installation. To add this:

1. In **Screens & Actions**, find the **"Additional confirmations"** screen under the **Installer** node (or whichever screen you want the checkbox on).
2. Select the screen and add a **form component** of type **"Check box"** to it.
3. Configure the checkbox:

| Property                       | Value |
|--------------------------------|-------|
| Check box > Text               | `Register Windows Explorer thumbnail previews for .ork files` |
| User input > Variable name     | `installThumbnailHandler` |
| Check box > Initially selected | Yes |

This creates an install4j variable `${installer:installThumbnailHandler}` that is `"true"` or `"false"` at runtime.

### 3. Add actions to the Installer

Go to **Installer** → **Screens & Actions** in the sidebar. Expand the **Installer** node. You need to add two **"Run executable or batch file"** actions at the **end** of the action list (after the existing screens/actions that install files). Click the **+** button on the right to add each one, and search for **"Run executable or batch file"**.

> **Important:** install4j's "Run script" action executes Java, not PowerShell. Use **"Run executable or batch file"** actions that call `powershell.exe` instead.

**Action 1 — Register the shell extension:**

Add a **"Run executable or batch file"** action with these properties:

| Property | Value |
|----------|-------|
| Executable | `powershell.exe` |
| Arguments | `-NoProfile -ExecutionPolicy Bypass -Command "& '$env:windir\Microsoft.NET\Framework64\v4.0.30319\regasm.exe' /codebase '${installer:sys.installationDir}\shell-extension\OrkThumbnailHandler.dll'"` |
| Wait for termination | Yes |
| Failure strategy | Continue on failure |
| Condition expression | `${installer:installThumbnailHandler}` |

**Action 2 — Notify the shell:**

Add another **"Run executable or batch file"** action immediately after:

| Property | Value |
|----------|-------|
| Executable | `powershell.exe` |
| Arguments | `-NoProfile -ExecutionPolicy Bypass -Command "Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public class Shell { [DllImport(\"shell32.dll\")] public static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2); }'; [Shell]::SHChangeNotify(0x08000000, 0x0000, [IntPtr]::Zero, [IntPtr]::Zero)"` |
| Wait for termination | Yes |
| Failure strategy | Continue on failure |
| Condition expression | `${installer:installThumbnailHandler}` |

This tells Explorer to reload its shell extensions without disrupting the user's open windows.

### 4. Add actions to the Uninstaller

Still in **Screens & Actions**, expand the **Uninstaller** node. Add two actions at the **beginning** of the action list — they must run **before** the action that deletes files (otherwise the DLL is gone before it can be unregistered).

The uninstaller actions do **not** need a condition expression — if the DLL is present, it should always be unregistered on uninstall regardless of the original checkbox choice.

**Action 1 — Unregister the shell extension:**

Add a **"Run executable or batch file"** action:

| Property | Value |
|----------|-------|
| Executable | `powershell.exe` |
| Arguments | `-NoProfile -ExecutionPolicy Bypass -Command "& '$env:windir\Microsoft.NET\Framework64\v4.0.30319\regasm.exe' /unregister '${installer:sys.installationDir}\shell-extension\OrkThumbnailHandler.dll'"` |
| Wait for termination | Yes |
| Failure strategy | Continue on failure |

**Action 2 — Notify the shell:**

Add the same shell notification action from step 3 immediately after (without the condition expression).

### 5. Updates

If your installer handles updates by running the installer again (which is the default install4j behavior), the registration actions from step 3 will re-run automatically. The checkbox will be shown again, so the user can change their preference on update.

### Action order summary

| Node | Action order | Conditioned on checkbox? |
|------|-------------|--------------------------|
| **Installer** | *(existing screens/actions)* → Register → Notify shell | Yes |
| **Uninstaller** | Unregister → Notify shell → *(existing screens/actions)* | No (always runs) |

### install4j notes

- **64-bit only:** The handler DLL must be registered with the 64-bit regasm (`Framework64`). Explorer on 64-bit Windows is a 64-bit process and will not load 32-bit shell extensions.
- **Don't move the DLL after registration:** `regasm /codebase` embeds the DLL's absolute path in the registry. This is fine for installers since the install directory is stable.
- **`${installer:sys.installationDir}` in scripts:** This is an install4j variable that resolves to the actual install path at runtime. Make sure the `shell-extension\` subfolder name in the scripts matches what you created in step 1.
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
