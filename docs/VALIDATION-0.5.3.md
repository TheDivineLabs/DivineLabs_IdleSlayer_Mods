# Validation: 0.5.3

17 October 2026. Desktop uninstaller change; game components remain 0.5.0.

## Completed

- Built the self-contained launcher, native entry point and installer. The base
  package, component archive and game files matched the pinned SHA256 values.
- Backend fixtures cover default data retention, optional full removal through
  main and copy paths, locked data preflight and retry, refusal to traverse links,
  unrelated files outside Data, neighboring installations and removal of an empty
  installation directory.
- WPF events with the native worker cover opt-in defaults, warning/result copy,
  cancellation and consent reset, locked-file failure, retry, close protection,
  removal of another copy with Data, and an unchanged main target. All nine
  maintenance languages include the new strings. Light/dark renders were inspected.
- The packaged installer installed full copies in isolated folders. Native checks
  removed Data through the copy command, main worker and the installed root EXE's
  detached helper. Empty roots disappeared; unrelated files outside Data survived.
  A separate default-removal run retained settings and save/application backups,
  left the neighboring launcher unchanged and verified its self-contained package.
- Opened the actual installed root uninstaller. UI Automation verified its
  expanded copy list, read-only target, unchecked full-removal default and toggle
  behavior, then closed it without deleting that installation.
- Verified the final portable ZIP layout, lack of PDBs, root EXE startup,
  relocation with spaces/Cyrillic names, Data location and error handling.

## Release checks

Public asset hashes and the anonymous component download are checked against the
published candidate before it is marked Latest.

## Scope

Full removal deletes the selected installation's entire Data directory, including
settings, caches, logs, local save copies and application/game/save backups.
It cannot be undone. Files outside Data are removed only when listed in the app
receipt; unrelated files are retained. Original saves and configuration in the
separate game directory and shared interface preferences are not deleted.

Only isolated temporary installations were removed. Existing user installations
and their retained backups were not cleaned. No game was started or modified by
these checks; UI automation used WPF events and accessibility patterns rather than
physical clicks. Gameplay and animation/performance checks were not repeated.

NuGet vulnerability metadata feeds were unavailable during the build (NU1900).
Compilation succeeded; this is not a dependency vulnerability audit.
