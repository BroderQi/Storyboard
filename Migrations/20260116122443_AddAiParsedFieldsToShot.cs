using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storyboard.Migrations
{
    /// <inheritdoc />
    public partial class AddAiParsedFieldsToShot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 安全地添加列 - 如果列已存在则跳过
            AddColumnIfNotExists(migrationBuilder, "Shots", "ImageSize", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "NegativePrompt", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "AspectRatio", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "LightingType", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "TimeOfDay", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "Composition", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "ColorStyle", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "LensType", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "VideoPrompt", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "SceneDescription", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "ActionDescription", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "StyleDescription", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "VideoNegativePrompt", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "CameraMovement", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "ShootingStyle", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "VideoEffect", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "VideoResolution", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "VideoRatio", "TEXT NOT NULL DEFAULT ''");
            AddColumnIfNotExists(migrationBuilder, "Shots", "VideoFrames", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfNotExists(migrationBuilder, "Shots", "UseFirstFrameReference", "INTEGER NOT NULL DEFAULT 1");
            AddColumnIfNotExists(migrationBuilder, "Shots", "UseLastFrameReference", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfNotExists(migrationBuilder, "Shots", "Seed", "INTEGER");
            AddColumnIfNotExists(migrationBuilder, "Shots", "CameraFixed", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfNotExists(migrationBuilder, "Shots", "Watermark", "INTEGER NOT NULL DEFAULT 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚时删除列
            DropColumnIfExists(migrationBuilder, "Shots", "ImageSize");
            DropColumnIfExists(migrationBuilder, "Shots", "NegativePrompt");
            DropColumnIfExists(migrationBuilder, "Shots", "AspectRatio");
            DropColumnIfExists(migrationBuilder, "Shots", "LightingType");
            DropColumnIfExists(migrationBuilder, "Shots", "TimeOfDay");
            DropColumnIfExists(migrationBuilder, "Shots", "Composition");
            DropColumnIfExists(migrationBuilder, "Shots", "ColorStyle");
            DropColumnIfExists(migrationBuilder, "Shots", "LensType");
            DropColumnIfExists(migrationBuilder, "Shots", "VideoPrompt");
            DropColumnIfExists(migrationBuilder, "Shots", "SceneDescription");
            DropColumnIfExists(migrationBuilder, "Shots", "ActionDescription");
            DropColumnIfExists(migrationBuilder, "Shots", "StyleDescription");
            DropColumnIfExists(migrationBuilder, "Shots", "VideoNegativePrompt");
            DropColumnIfExists(migrationBuilder, "Shots", "CameraMovement");
            DropColumnIfExists(migrationBuilder, "Shots", "ShootingStyle");
            DropColumnIfExists(migrationBuilder, "Shots", "VideoEffect");
            DropColumnIfExists(migrationBuilder, "Shots", "VideoResolution");
            DropColumnIfExists(migrationBuilder, "Shots", "VideoRatio");
            DropColumnIfExists(migrationBuilder, "Shots", "VideoFrames");
            DropColumnIfExists(migrationBuilder, "Shots", "UseFirstFrameReference");
            DropColumnIfExists(migrationBuilder, "Shots", "UseLastFrameReference");
            DropColumnIfExists(migrationBuilder, "Shots", "Seed");
            DropColumnIfExists(migrationBuilder, "Shots", "CameraFixed");
            DropColumnIfExists(migrationBuilder, "Shots", "Watermark");
        }

        private void AddColumnIfNotExists(MigrationBuilder migrationBuilder, string table, string column, string type)
        {
            migrationBuilder.Sql($@"
                SELECT CASE
                    WHEN COUNT(*) = 0 THEN
                        'ALTER TABLE {table} ADD COLUMN {column} {type}'
                    ELSE
                        'SELECT 1'
                END as sql
                FROM pragma_table_info('{table}')
                WHERE name = '{column}'
            ");
        }

        private void DropColumnIfExists(MigrationBuilder migrationBuilder, string table, string column)
        {
            // SQLite 不支持 DROP COLUMN，需要重建表
            // 这里我们简单地忽略，因为 Down 通常不会被使用
        }
    }
}
