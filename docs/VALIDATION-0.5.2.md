# Validation: 0.5.2

17 October 2026. Windows desktop changes; game components remain 0.5.0.

## Completed locally

- Built the self-contained launcher and native installer/bootstrap from the
  canonical repository. The base portable archive, components ZIP and game
  component files matched their pinned SHA256 values.
- Installed two full copies in isolated temporary folders. Removed only the
  second; retained settings, saves, application/save backups and unrelated files.
  The first launcher's hash did not change and its root EXE verified the package
  without an external .NET installation.
- Tested both maintenance modes. The uninstaller's list is visible and expanded,
  excludes the current installation and cannot change the main target. Confirm,
  cancel, locked-file failure, retry, close protection and removal of the completed
  row were exercised through WPF events with the native worker.
- Started the installed `DivineLabs.Uninstall.exe` and inspected its actual child
  window using UI Automation. Options exposed the expanded copy list and the
  original read-only target. Closed that test window without removing its installation.
- Verified the final portable's layout, root EXE, relocation with spaces/Cyrillic
  paths, data location and error reporting.
- Checked shared product facts and local navigation/image/license links in all
  nine READMEs. Discord and Telegram returned HTTP 200. Added English/Russian
  offline instructions independently of repository-relative links.

The README image is a render of the current WPF feature screen with default
settings, English text and the dark theme. It contains no personal paths and does
not represent a connected game session. No game was started to create it.

## Publication gates

Before retiring older releases:

1. Match every uploaded asset's size and GitHub SHA256 digest to the local file;
   use GitHub's actual filename in the public checksum manifest.
2. Run `scripts/Check-PublicComponents.ps1` against the published 0.5.2 files.
   This fixture has neither bundled components nor a cache and uses the launcher's
   anonymous downloader. Verify the downloaded ZIP and resulting package cache.
3. Promote 0.5.2 to an ordinary Latest release, remove only the nine snapshotted
   older release objects/assets, and verify that their Git tags remain unchanged.

Evidence and publication results are retained under
`W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\0.5.2\Checks`.
Old release metadata is archived separately under `release-archive/pre-0.5.2`.

## Limits

WPF routed events and UI Automation are not physical mouse/keyboard acceptance.
No new gameplay or Android run was performed; game-component bytes were unchanged.
Installation fixtures used no Windows shortcut integrations, preserving the user's
working shortcuts and uninstall entry. Actual user installations were not removed.

Build warnings NU1900 reported unavailable vulnerability metadata from the NuGet
sources. A dependency vulnerability audit is not claimed.

## Publication results

- All five uploaded assets matched local sizes and GitHub SHA256 digests.
- The root EXE anonymously downloaded components from the 0.5.2 release into a
  fresh fixture with no bundled components or cache, verified the ZIP SHA256 and
  extracted/verified the resulting package.
- Installer and portable links through `releases/latest/download` returned HTTP 200.
- 0.5.2 is an ordinary Latest release. All nine older release objects and their
  assets were removed; every previously existing Git tag retained its original SHA.
- The existing [Windows CI run](https://github.com/TheDivineLabs/DivineLabs_IdleSlayer_Mods/actions/runs/39038600539)
  passed for release commit `e1b067c502cc88cc45957213a6b1219eebb0bbb7`.

The binaries were built from `d7faebd286c6479e7dfbc3cff620b0f28e9d75c9`;
the release commit adds documentation and test-harness refinements only.
Both identities are recorded in the published `build.json`. This final report
update changes documentation only and does not rebuild the package.
