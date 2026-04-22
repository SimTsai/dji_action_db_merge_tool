# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-04-22

### Added
- Initial release.
- GUI mode with file pickers, progress bar, and live log (Avalonia).
- CLI mode: `--source` / `--target` arguments for headless operation.
- `--version` flag to print the current version and exit.
- Version number displayed in the window title (GUI) and via `--version` (CLI).
- Correct merge order with automatic foreign-key remapping for `video_info_table`,
  `image_info_table`, and `gis_info_table`.
- Duplicate-safe insertion (`INSERT OR IGNORE`) for all primary-keyed tables.
- Full transaction – the target database is never left in a partial state.
- NativeAOT support for self-contained, fast-startup binaries on Windows and Linux.
- GitHub Actions workflow to build and publish release artifacts for
  `win-x64`, `linux-x64`, and `linux-arm64`.
