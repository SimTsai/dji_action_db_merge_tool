# DJI Action DB Merge Tool

A C# / WPF tool for merging DJI Action camera SQLite databases (`ACxxx.db`), typically found at `/MISC/ACxxx.db` on DJI storage cards.

## Features

- **GUI mode** – drag-and-drop-friendly file pickers, a progress bar, and a live log.
- **CLI mode** – run headless from a terminal or batch script.
- Correct merge order with automatic **foreign-key remapping**:
  - `video_info_table` and `image_info_table` are migrated first, generating old→new ID maps.
  - `gis_info_table.video_index` / `image_index` are rewritten to the new IDs.
- Duplicate-safe insertion for all primary-keyed tables (`INSERT OR IGNORE`).
- Full transaction – the target database is never left in a partial state.

## Requirements

- Windows (WPF)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build

```bash
dotnet build DjiActionDbMergeTool.slnx
```

## Usage

### GUI

Double-click `DjiActionDbMergeTool.exe` (or `dotnet run` from the project folder), select the **Source** and **Target** databases, then click **Merge**.

### CLI

```
DjiActionDbMergeTool.exe --source <path-to-source.db> --target <path-to-target.db>
```

| Argument | Description |
|----------|-------------|
| `--source` | Path to the source database (read-only). |
| `--target` | Path to the target database (will be created if it does not exist). |

Exit code `0` = success, `1` = error.

## Database Tables

| Table | Primary Key | Notes |
|-------|-------------|-------|
| `version_table` | – | Deduplicated by value. |
| `mtime_table` | `dcf_index` | `INSERT OR IGNORE` on conflict. |
| `gis_info_table` | `ID` (auto) | `video_index` / `image_index` remapped. |
| `image_info_table` | `ID` (auto) | Migrated before `gis_info_table`. |
| `video_info_table` | `ID` (auto) | Migrated before `gis_info_table`. |
| `file_additional_info_table` | `file_index` | `INSERT OR IGNORE` on conflict. |
| `dir_additional_info_table` | `dir_no` | `INSERT OR IGNORE` on conflict. |