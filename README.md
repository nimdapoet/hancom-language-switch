# Hancom Language Switch

[Tiếng Việt](README.vi.md)

A portable, independent Windows utility that selects the English or Korean interface for Hangul/HWP and Hanword in Hancom Office 2022 (version 12.x). It uses the language resources already installed with Hancom Office. This is not an official Hancom product or a new translation pack.

## Requirements

- Windows with Hancom Office 2022 and the selected application's matching English and Korean resource DLLs installed.
- The .NET Framework 4 runtime; the executable runs without an installer or administrator privileges under normal user-profile permissions.

## Getting started

1. Save your documents and close all Hancom applications, including Hancom Configuration Utility (한컴 기본 설정). Close older Hancom versions too.
2. Download the portable ZIP from the latest GitHub release, extract it, and double-click `HancomLanguageSwitch.exe`.
3. Select **Hangul / HWP (한글)**. Optionally select **Hanword (한워드)** as well.
4. Click **Use English**, then **Open Hangul** or **Open Hanword**. These buttons launch the installed 2022 applications.
5. To switch back, close Hancom and click **Use Korean**. **Restore original** restores the language settings captured before the first change by this utility.

The main Hangul 2022 interface has been verified in English on version 12.0.0.535. Some dialogs, font names, or content may remain in Korean. Hanword's configuration and resources were checked, but its interface has not been verified interactively.

## Configuration and backups

The utility changes the selected applications' `CultureName` entries in `%APPDATA%\HNC\User\Common\120\ThemeInfo.xml`. It retains the shared default language and other settings. It does not modify Hancom program files, licenses, or documents, and does not upload document contents.

Before the first change, the original configuration and its SHA-256 digest are saved under `%LOCALAPPDATA%\HancomLanguageSwitch\2022\`. An additional configuration snapshot is saved before each write. Changes are written with an atomic replacement and rejected if another process changes the configuration during the operation. Restore merges the original language settings into the current configuration, preserving other settings.

To remove the utility, restore the original language first if desired, then delete the extracted utility folder. Backups remain in the directory above.

## Build and tests

Run `build.ps1` in PowerShell on Windows with the .NET Framework compiler installed. The output is written to `dist`. The source uses framework libraries and requires no third-party packages. The optional `HancomLanguageSwitch.Cli.exe` build supports `--status`, `--english`, `--korean`, and `--restore`, with an optional second argument such as `Hwp` or `Hwp,Hword`.

The initial release passed 28 automated assertions covering configuration preservation, restore, invalid input, corrupt backups, malformed XML, application-in-use checks, and concurrent writes. `tests/CoreTests.cs` contains the test harness. `tools/InspectIL.cs` is a reflection-only inspection utility used during development and is not required to run the switcher.

## Scope

The portable package contains the switcher and documentation; it does not redistribute Hancom DLLs or other Hancom binaries. Availability of language resources and translated UI coverage can vary between installations.
