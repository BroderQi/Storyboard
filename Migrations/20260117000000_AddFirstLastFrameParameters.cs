using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storyboard.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstLastFrameParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 添加首帧专业参数
            migrationBuilder.Sql(@"
                ALTER TABLE Shots ADD COLUMN FirstFrameComposition TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN FirstFrameLightingType TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN FirstFrameTimeOfDay TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN FirstFrameColorStyle TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN FirstFrameLensType TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN FirstFrameNegativePrompt TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN FirstFrameImageSize TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN FirstFrameAspectRatio TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN FirstFrameSelectedModel TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN FirstFrameSeed INTEGER;
            ");

            // 添加尾帧专业参数
            migrationBuilder.Sql(@"
                ALTER TABLE Shots ADD COLUMN LastFrameComposition TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN LastFrameLightingType TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN LastFrameTimeOfDay TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN LastFrameColorStyle TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN LastFrameLensType TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN LastFrameNegativePrompt TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN LastFrameImageSize TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN LastFrameAspectRatio TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN LastFrameSelectedModel TEXT NOT NULL DEFAULT '';
                ALTER TABLE Shots ADD COLUMN LastFrameSeed INTEGER;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite 不支持 DROP COLUMN，需要重建表
            // 这里我们简单地忽略，因为 Down 通常不会被使用
        }
    }
}
