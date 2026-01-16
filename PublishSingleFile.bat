@echo off
chcp 65001 >nul
echo ================================================================================
echo                    分镜板 Storyboard - 单文件发布工具
echo ================================================================================
echo.

REM 设置变量
set "PROJECT_DIR=%~dp0"
set "RELEASE_DIR=%PROJECT_DIR%Release"
set "RUNTIMES_DIR=%RELEASE_DIR%\Runtimes"

echo 当前配置：
echo - 项目目录: %PROJECT_DIR%
echo - 发布目录: %RELEASE_DIR%
echo - 发布模式: 单文件发布 (需要 .NET 8 运行时)
echo.

REM 询问是否继续
echo 准备执行单文件发布...
echo 这将：
echo   1. 清理旧的发布文件夹
echo   2. 使用 dotnet publish 生成单文件 exe
echo   3. 复制用户文档和运行时说明
echo.
set /p "CONFIRM=是否继续? (Y/N): "
if /i not "%CONFIRM%"=="Y" (
    echo 操作已取消。
    pause
    exit /b 0
)

echo.
echo [1/5] 清理旧的发布文件夹...
if exist "%RELEASE_DIR%" (
    rmdir /s /q "%RELEASE_DIR%"
)
mkdir "%RELEASE_DIR%"
mkdir "%RUNTIMES_DIR%"

echo [2/5] 执行单文件发布 (这可能需要几分钟)...
dotnet publish "%PROJECT_DIR%Storyboard.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:PublishReadyToRun=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=false ^
    -o "%RELEASE_DIR%"

if errorlevel 1 (
    echo.
    echo [错误] 发布失败！请检查：
    echo   1. 是否已安装 .NET 8 SDK
    echo   2. 项目是否可以正常编译
    echo.
    pause
    exit /b 1
)

echo [3/5] 清理不需要的文件...
REM 删除 .pdb 调试文件
if exist "%RELEASE_DIR%\*.pdb" del /q "%RELEASE_DIR%\*.pdb"

echo [4/5] 复制用户文档...

echo [5/5] 创建说明文档...

REM 创建 README
(
echo ================================================================================
echo                         分镜板 Storyboard Studio
echo ================================================================================
echo.
echo 本软件需要 .NET 8 运行时才能运行。请根据您的操作系统下载对应的运行时。
echo
echo --------------------------------------------------------------------------------
echo 快速开始
echo --------------------------------------------------------------------------------
echo.
echo 1. 首次使用请先安装 .NET 8 运行时
echo.
echo 2. 双击 Storyboard.exe 启动应用
echo.
echo --------------------------------------------------------------------------------
echo Windows 用户安装运行时
echo --------------------------------------------------------------------------------
echo.
echo 下载地址：
echo https://dotnet.microsoft.com/download/dotnet/8.0
echo.
echo 需要下载：
echo - Windows Desktop Runtime 8.0.x - x64
echo   文件名示例：windowsdesktop-runtime-8.0.23-win-x64.exe
echo.
echo 下载后请将安装包放入 Runtimes 文件夹中。
echo.
echo --------------------------------------------------------------------------------
echo macOS 用户安装运行时
echo --------------------------------------------------------------------------------
echo.
echo 下载地址：
echo https://dotnet.microsoft.com/download/dotnet/8.0
echo.
echo 需要下载（根据您的 Mac 芯片类型选择）
echo.
echo Intel 芯片 Mac
echo - .NET Runtime 8.0.x - macOS x64
echo   文件名示例：dotnet-runtime-8.0.x-osx-x64.pkg
echo.
echo Apple Silicon M1/M2/M3 Mac
echo - .NET Runtime 8.0.x - macOS Arm64
echo   文件名示例：dotnet-runtime-8.0.x-osx-arm64.pkg
echo.
echo.
echo --------------------------------------------------------------------------------
echo 技术支持
echo --------------------------------------------------------------------------------
echo.
echo 如有问题或建议，请微信联系 zxxl2025
echo.
echo 祝您使用愉快！
echo
echo ================================================================================
) > "%RUNTIMES_DIR%\README.txt"


echo.
echo ================================================================================
echo                              发布完成！
echo ================================================================================
echo.
echo 发布文件夹位置: %RELEASE_DIR%
echo.
echo 文件夹结构：
echo   Release/
echo   ├── Storyboard.exe           （单文件，约 100-150MB）
echo   ├── appsettings.json         （配置文件）
echo   ├── README.txt               （快速开始）
echo   ├── Windows使用说明.txt
echo   ├── macOS使用说明.txt
echo   ├── 用户指南.md
echo   ├── App/                     （应用资源）
echo   ├── Tools/                   （ffmpeg 等工具）
echo   └── Runtimes/                （运行时安装包）
echo.
echo 下一步操作：
echo 1. 下载 .NET 8 运行时安装包（参考 Runtimes\运行时下载说明.txt）
echo 2. 将运行时安装包放入 Release\Runtimes 文件夹
echo 3. 测试应用程序（双击 Storyboard.exe）
echo 4. 打包发布（压缩整个 Release 文件夹）
echo.
echo 详细说明请查看: %RELEASE_DIR%\发布说明.txt
echo.
pause
