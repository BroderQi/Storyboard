using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storyboard.Infrastructure.Persistence;

namespace Storyboard.Infrastructure.Migrations;

/// <summary>
/// 数据库迁移助手 - 安全地添加新列而不丢失数据
/// </summary>
public static class DatabaseMigrationHelper
{
    /// <summary>
    /// 应用增量迁移 - 添加 AI 解析字段
    /// </summary>
    public static async Task ApplyIncrementalMigrationsAsync(StoryboardDbContext context, ILogger logger)
    {
        try
        {
            // 确保数据库已创建
            await context.Database.EnsureCreatedAsync();

            // 获取数据库连接
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();

            // 检查并添加新列
            var columnsToAdd = new Dictionary<string, string>
            {
                // Material info fields
                { "MaterialResolution", "TEXT NOT NULL DEFAULT ''" },
                { "MaterialFileSize", "TEXT NOT NULL DEFAULT ''" },
                { "MaterialFormat", "TEXT NOT NULL DEFAULT ''" },
                { "MaterialColorTone", "TEXT NOT NULL DEFAULT ''" },
                { "MaterialBrightness", "TEXT NOT NULL DEFAULT ''" },
                // 图片生成参数
                { "ImageSize", "TEXT NOT NULL DEFAULT ''" },
                { "NegativePrompt", "TEXT NOT NULL DEFAULT ''" },
                // 图片专业参数 (legacy)
                { "AspectRatio", "TEXT NOT NULL DEFAULT ''" },
                { "LightingType", "TEXT NOT NULL DEFAULT ''" },
                { "TimeOfDay", "TEXT NOT NULL DEFAULT ''" },
                { "Composition", "TEXT NOT NULL DEFAULT ''" },
                { "ColorStyle", "TEXT NOT NULL DEFAULT ''" },
                { "LensType", "TEXT NOT NULL DEFAULT ''" },
                // 首帧专业参数
                { "FirstFrameComposition", "TEXT NOT NULL DEFAULT ''" },
                { "FirstFrameLightingType", "TEXT NOT NULL DEFAULT ''" },
                { "FirstFrameTimeOfDay", "TEXT NOT NULL DEFAULT ''" },
                { "FirstFrameColorStyle", "TEXT NOT NULL DEFAULT ''" },
                { "FirstFrameLensType", "TEXT NOT NULL DEFAULT ''" },
                { "FirstFrameNegativePrompt", "TEXT NOT NULL DEFAULT ''" },
                { "FirstFrameImageSize", "TEXT NOT NULL DEFAULT ''" },
                { "FirstFrameAspectRatio", "TEXT NOT NULL DEFAULT ''" },
                { "FirstFrameSelectedModel", "TEXT NOT NULL DEFAULT ''" },
                { "FirstFrameSeed", "INTEGER" },
                // 尾帧专业参数
                { "LastFrameComposition", "TEXT NOT NULL DEFAULT ''" },
                { "LastFrameLightingType", "TEXT NOT NULL DEFAULT ''" },
                { "LastFrameTimeOfDay", "TEXT NOT NULL DEFAULT ''" },
                { "LastFrameColorStyle", "TEXT NOT NULL DEFAULT ''" },
                { "LastFrameLensType", "TEXT NOT NULL DEFAULT ''" },
                { "LastFrameNegativePrompt", "TEXT NOT NULL DEFAULT ''" },
                { "LastFrameImageSize", "TEXT NOT NULL DEFAULT ''" },
                { "LastFrameAspectRatio", "TEXT NOT NULL DEFAULT ''" },
                { "LastFrameSelectedModel", "TEXT NOT NULL DEFAULT ''" },
                { "LastFrameSeed", "INTEGER" },
                // 视频生成参数
                { "VideoPrompt", "TEXT NOT NULL DEFAULT ''" },
                { "SceneDescription", "TEXT NOT NULL DEFAULT ''" },
                { "ActionDescription", "TEXT NOT NULL DEFAULT ''" },
                { "StyleDescription", "TEXT NOT NULL DEFAULT ''" },
                { "VideoNegativePrompt", "TEXT NOT NULL DEFAULT ''" },
                // 视频专业参数
                { "CameraMovement", "TEXT NOT NULL DEFAULT ''" },
                { "ShootingStyle", "TEXT NOT NULL DEFAULT ''" },
                { "VideoEffect", "TEXT NOT NULL DEFAULT ''" },
                { "VideoResolution", "TEXT NOT NULL DEFAULT ''" },
                { "VideoRatio", "TEXT NOT NULL DEFAULT ''" },
                { "VideoFrames", "INTEGER NOT NULL DEFAULT 0" },
                { "UseFirstFrameReference", "INTEGER NOT NULL DEFAULT 1" },
                { "UseLastFrameReference", "INTEGER NOT NULL DEFAULT 0" },
                { "Seed", "INTEGER" },
                { "CameraFixed", "INTEGER NOT NULL DEFAULT 0" },
                { "Watermark", "INTEGER NOT NULL DEFAULT 0" }
            };

            foreach (var (columnName, columnType) in columnsToAdd)
            {
                if (!await ColumnExistsAsync(connection, "Shots", columnName))
                {
                    logger.LogInformation("添加列 {ColumnName} 到 Shots 表", columnName);

                    using var command = connection.CreateCommand();
                    command.CommandText = $"ALTER TABLE Shots ADD COLUMN {columnName} {columnType}";
                    await command.ExecuteNonQueryAsync();

                    logger.LogInformation("成功添加列 {ColumnName}", columnName);
                }
                else
                {
                    logger.LogDebug("列 {ColumnName} 已存在，跳过", columnName);
                }
            }

            await connection.CloseAsync();
            logger.LogInformation("数据库迁移完成");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "数据库迁移失败");
            throw;
        }
    }

    private static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name = '{columnName}'";
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }
}
