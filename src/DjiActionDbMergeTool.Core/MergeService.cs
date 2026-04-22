using Microsoft.Data.Sqlite;

namespace DjiActionDbMergeTool.Core;

public class MergeService
{
    private const int TotalSteps = 7;

    public async Task MergeAsync(
        string sourceDbPath,
        string targetDbPath,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(async () =>
        {
            var sourceCs = new SqliteConnectionStringBuilder
            {
                DataSource = sourceDbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            var targetCs = new SqliteConnectionStringBuilder
            {
                DataSource = targetDbPath,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            await using var source = new SqliteConnection(sourceCs);
            await using var target = new SqliteConnection(targetCs);

            await source.OpenAsync(cancellationToken);
            await target.OpenAsync(cancellationToken);

            // Ensure target schema exists
            await EnsureSchemaAsync(target, cancellationToken);

            await using var transaction = await target.BeginTransactionAsync(cancellationToken);

            try
            {
                int step = 0;

                Report(progress, ++step, TotalSteps, "Merging video_info_table...");
                var videoIdMap = await MergeVideoInfoTableAsync(source, target, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                Report(progress, ++step, TotalSteps, "Merging image_info_table...");
                var imageIdMap = await MergeImageInfoTableAsync(source, target, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                Report(progress, ++step, TotalSteps, "Merging gis_info_table...");
                await MergeGisInfoTableAsync(source, target, videoIdMap, imageIdMap, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                Report(progress, ++step, TotalSteps, "Merging mtime_table...");
                await MergeMtimeTableAsync(source, target, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                Report(progress, ++step, TotalSteps, "Merging file_additional_info_table...");
                await MergeFileAdditionalInfoTableAsync(source, target, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                Report(progress, ++step, TotalSteps, "Merging dir_additional_info_table...");
                await MergeDirAdditionalInfoTableAsync(source, target, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                Report(progress, ++step, TotalSteps, "Merging version_table...");
                await MergeVersionTableAsync(source, target, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                progress?.Report(new MergeProgress
                {
                    Current = TotalSteps,
                    Total = TotalSteps,
                    Message = "Merge completed successfully.",
                    IsCompleted = true
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);

                progress?.Report(new MergeProgress
                {
                    Current = 0,
                    Total = TotalSteps,
                    Message = "Merge failed.",
                    HasError = true,
                    ErrorMessage = ex.Message
                });

                throw;
            }
        }, cancellationToken);
    }

    private static void Report(IProgress<MergeProgress>? progress, int current, int total, string message)
    {
        progress?.Report(new MergeProgress { Current = current, Total = total, Message = message });
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection conn, string tableName, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
        cmd.Parameters.AddWithValue("@name", tableName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task EnsureSchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        var ddl = """
            CREATE TABLE IF NOT EXISTS version_table(version INT);
            CREATE TABLE IF NOT EXISTS mtime_table (dcf_index INT PRIMARY KEY, mtime INT);
            CREATE TABLE IF NOT EXISTS gis_info_table (ID INTEGER PRIMARY KEY, dcf_index INT, camera_type INT, file_name TEXT, uuid INT, file_type INT, sub_type INT, result INT, star INT, origin INT, cloud_download INT, highlight INT, video_index INT, image_index INT);
            CREATE TABLE IF NOT EXISTS image_info_table (ID INTEGER PRIMARY KEY, thm_offset INT, thm_size INT, scr_offset INT, scr_size INT, xmp_offset INT, xmp_size INT, exif BLOB, origin_state INT, has_ai_mot INT, watermark_info INT, custom_file_tag INT);
            CREATE TABLE IF NOT EXISTS video_info_table (ID INTEGER PRIMARY KEY, duration INT, frame_num INT, frame_den INT, rotation INT, resolution_width INT, resolution_height INT, slowmotion_rate INT, encode_format INT, nail_offset INT, nail_length INT, shutter_reciprocal INT, shutter_integer INT, shutter_decimal INT, ei_value INT, wb_count INT, wb_tint INT, shutter_angle INT, nd_value INT, aperture INT, ev_bias INT, shutter_type INT, project_frame INT, tc_start INT, tc_end INT, venc_type INT, model_name TEXT, thm_offset64 INT, thm_size64 INT, scr_offset64 INT, scr_size64 INT, project_frame_num INT, project_frame_den INT, digital_effect INT, steady_mode INT, fov_type INT, beauty_enable INT, smoother INT, whitening INT, face_slimming INT, eye_enlarge INT, nose_slimming INT, mouth_beautify INT, teeth_whitening INT, leg_longer INT, head_shrinking INT, lipstick INT, blush INT, dark_circle INT, acne_spot_removal INT, eyebrows INT, gps_status INT, highlight INT, app_audio_status INT, bit_depth INT, color_mode_main_version INT, color_mode_sub_version INT, ebike_status INT, custom_file_tag INT, has_ai_mot INT, any_aspect_ratio_export_support INT, any_aspect_ratio_export_has_black_corner INT, style_filter_mode INT);
            CREATE TABLE IF NOT EXISTS file_additional_info_table (file_index INTEGER PRIMARY KEY, property INTEGER, info BLOB);
            CREATE TABLE IF NOT EXISTS dir_additional_info_table (dir_no INTEGER PRIMARY KEY, property INTEGER, info BLOB);
            """;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = ddl;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Dictionary<long, long>> MergeVideoInfoTableAsync(
        SqliteConnection source, SqliteConnection target, CancellationToken ct)
    {
        var idMap = new Dictionary<long, long>();

        if (!await TableExistsAsync(source, "video_info_table", ct))
            return idMap;

        await using var selectCmd = source.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM video_info_table";

        await using var reader = await selectCmd.ExecuteReaderAsync(ct);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var dataColumns = columns.Where(c => c != "ID").ToList();

        var insertSql = $"INSERT INTO video_info_table ({string.Join(",", dataColumns)}) VALUES ({string.Join(",", dataColumns.Select(c => "@" + c))})";

        while (await reader.ReadAsync(ct))
        {
            var oldId = reader.GetInt64(reader.GetOrdinal("ID"));

            await using var insertCmd = target.CreateCommand();
            insertCmd.CommandText = insertSql;

            foreach (var col in dataColumns)
            {
                var val = reader[col];
                insertCmd.Parameters.AddWithValue("@" + col, val is DBNull ? (object)DBNull.Value : val);
            }

            await insertCmd.ExecuteNonQueryAsync(ct);

            await using var lastIdCmd = target.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid()";
            var newId = (long)(await lastIdCmd.ExecuteScalarAsync(ct))!;

            idMap[oldId] = newId;
        }

        return idMap;
    }

    private static async Task<Dictionary<long, long>> MergeImageInfoTableAsync(
        SqliteConnection source, SqliteConnection target, CancellationToken ct)
    {
        var idMap = new Dictionary<long, long>();

        if (!await TableExistsAsync(source, "image_info_table", ct))
            return idMap;

        await using var selectCmd = source.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM image_info_table";

        await using var reader = await selectCmd.ExecuteReaderAsync(ct);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var dataColumns = columns.Where(c => c != "ID").ToList();

        var insertSql = $"INSERT INTO image_info_table ({string.Join(",", dataColumns)}) VALUES ({string.Join(",", dataColumns.Select(c => "@" + c))})";

        while (await reader.ReadAsync(ct))
        {
            var oldId = reader.GetInt64(reader.GetOrdinal("ID"));

            await using var insertCmd = target.CreateCommand();
            insertCmd.CommandText = insertSql;

            foreach (var col in dataColumns)
            {
                var val = reader[col];
                insertCmd.Parameters.AddWithValue("@" + col, val is DBNull ? (object)DBNull.Value : val);
            }

            await insertCmd.ExecuteNonQueryAsync(ct);

            await using var lastIdCmd = target.CreateCommand();
            lastIdCmd.CommandText = "SELECT last_insert_rowid()";
            var newId = (long)(await lastIdCmd.ExecuteScalarAsync(ct))!;

            idMap[oldId] = newId;
        }

        return idMap;
    }

    private static async Task MergeGisInfoTableAsync(
        SqliteConnection source, SqliteConnection target,
        Dictionary<long, long> videoIdMap, Dictionary<long, long> imageIdMap,
        CancellationToken ct)
    {
        if (!await TableExistsAsync(source, "gis_info_table", ct))
            return;

        await using var selectCmd = source.CreateCommand();
        selectCmd.CommandText = "SELECT * FROM gis_info_table";

        await using var reader = await selectCmd.ExecuteReaderAsync(ct);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
        var dataColumns = columns.Where(c => c != "ID").ToList();

        var insertSql = $"INSERT INTO gis_info_table ({string.Join(",", dataColumns)}) VALUES ({string.Join(",", dataColumns.Select(c => "@" + c))})";

        while (await reader.ReadAsync(ct))
        {
            await using var insertCmd = target.CreateCommand();
            insertCmd.CommandText = insertSql;

            foreach (var col in dataColumns)
            {
                var val = reader[col];

                if (col == "video_index" && val is not DBNull)
                {
                    var oldIdx = Convert.ToInt64(val);
                    val = (oldIdx > 0 && videoIdMap.TryGetValue(oldIdx, out var newIdx)) ? newIdx : oldIdx;
                }
                else if (col == "image_index" && val is not DBNull)
                {
                    var oldIdx = Convert.ToInt64(val);
                    val = (oldIdx > 0 && imageIdMap.TryGetValue(oldIdx, out var newIdx)) ? newIdx : oldIdx;
                }

                insertCmd.Parameters.AddWithValue("@" + col, val is DBNull ? (object)DBNull.Value : val);
            }

            await insertCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task MergeMtimeTableAsync(
        SqliteConnection source, SqliteConnection target, CancellationToken ct)
    {
        if (!await TableExistsAsync(source, "mtime_table", ct))
            return;

        await using var selectCmd = source.CreateCommand();
        selectCmd.CommandText = "SELECT dcf_index, mtime FROM mtime_table";

        await using var reader = await selectCmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            await using var insertCmd = target.CreateCommand();
            insertCmd.CommandText = "INSERT OR IGNORE INTO mtime_table (dcf_index, mtime) VALUES (@dcf_index, @mtime)";
            insertCmd.Parameters.AddWithValue("@dcf_index", reader["dcf_index"]);
            insertCmd.Parameters.AddWithValue("@mtime", reader["mtime"]);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task MergeFileAdditionalInfoTableAsync(
        SqliteConnection source, SqliteConnection target, CancellationToken ct)
    {
        if (!await TableExistsAsync(source, "file_additional_info_table", ct))
            return;

        await using var selectCmd = source.CreateCommand();
        selectCmd.CommandText = "SELECT file_index, property, info FROM file_additional_info_table";

        await using var reader = await selectCmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            await using var insertCmd = target.CreateCommand();
            insertCmd.CommandText = "INSERT OR IGNORE INTO file_additional_info_table (file_index, property, info) VALUES (@file_index, @property, @info)";
            insertCmd.Parameters.AddWithValue("@file_index", reader["file_index"]);
            insertCmd.Parameters.AddWithValue("@property", reader["property"]);
            var info = reader["info"];
            insertCmd.Parameters.AddWithValue("@info", info is DBNull ? (object)DBNull.Value : info);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task MergeDirAdditionalInfoTableAsync(
        SqliteConnection source, SqliteConnection target, CancellationToken ct)
    {
        if (!await TableExistsAsync(source, "dir_additional_info_table", ct))
            return;

        await using var selectCmd = source.CreateCommand();
        selectCmd.CommandText = "SELECT dir_no, property, info FROM dir_additional_info_table";

        await using var reader = await selectCmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            await using var insertCmd = target.CreateCommand();
            insertCmd.CommandText = "INSERT OR IGNORE INTO dir_additional_info_table (dir_no, property, info) VALUES (@dir_no, @property, @info)";
            insertCmd.Parameters.AddWithValue("@dir_no", reader["dir_no"]);
            insertCmd.Parameters.AddWithValue("@property", reader["property"]);
            var info = reader["info"];
            insertCmd.Parameters.AddWithValue("@info", info is DBNull ? (object)DBNull.Value : info);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task MergeVersionTableAsync(
        SqliteConnection source, SqliteConnection target, CancellationToken ct)
    {
        if (!await TableExistsAsync(source, "version_table", ct))
            return;

        await using var selectCmd = source.CreateCommand();
        selectCmd.CommandText = "SELECT version FROM version_table";

        await using var reader = await selectCmd.ExecuteReaderAsync(ct);
        var versions = new List<long>();

        while (await reader.ReadAsync(ct))
        {
            if (reader["version"] is not DBNull)
                versions.Add(Convert.ToInt64(reader["version"]));
        }

        foreach (var ver in versions)
        {
            await using var checkCmd = target.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM version_table WHERE version=@v";
            checkCmd.Parameters.AddWithValue("@v", ver);
            var count = (long)(await checkCmd.ExecuteScalarAsync(ct))!;

            if (count == 0)
            {
                await using var insertCmd = target.CreateCommand();
                insertCmd.CommandText = "INSERT INTO version_table (version) VALUES (@v)";
                insertCmd.Parameters.AddWithValue("@v", ver);
                await insertCmd.ExecuteNonQueryAsync(ct);
            }
        }
    }
}
