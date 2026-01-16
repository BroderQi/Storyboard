@echo off
chcp 65001 >nul
echo ================================================================================
echo                    分镜板 Storyboard - 发布文件夹准备工具
echo ================================================================================
echo.

REM 设置变量
set "PROJECT_DIR=%~dp0"
set "BUILD_TYPE=%1"

REM 如果没有指定构建类型，默认使用 Release
if "%BUILD_TYPE%"=="" set "BUILD_TYPE=Release"

set "SOURCE_DIR=%PROJECT_DIR%bin\%BUILD_TYPE%\net8.0"
set "RELEASE_DIR=%PROJECT_DIR%Release"
set "RUNTIMES_DIR=%RELEASE_DIR%\Runtimes"

echo 当前配置：
echo - 项目目录: %PROJECT_DIR%
echo - 构建类型: %BUILD_TYPE%
echo - 源目录: %SOURCE_DIR%
echo - 发布目录: %RELEASE_DIR%
echo.

REM 检查源目录是否存在
if not exist "%SOURCE_DIR%" (
    echo [错误] 源目录不存在: %SOURCE_DIR%
    echo 请先编译项目！
    echo.
    pause
    exit /b 1
)

REM 询问是否继续
echo 准备将 %BUILD_TYPE% 构建复制到发布文件夹...
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

echo [2/5] 复制应用程序文件...
xcopy "%SOURCE_DIR%\*" "%RELEASE_DIR%\" /E /I /Y /Q
if errorlevel 1 (
    echo [错误] 复制文件失败！
    pause
    exit /b 1
)

echo [3/5] 复制使用说明文件...
copy "%PROJECT_DIR%Windows使用说明.txt" "%RELEASE_DIR%\" >nul
copy "%PROJECT_DIR%macOS使用说明.txt" "%RELEASE_DIR%\" >nul

echo [4/5] 创建运行时下载说明...
(
echo ================================================================================
echo                    .NET 8 运行时下载说明
echo ================================================================================
echo.
echo 本软件需要 .NET 8 运行时才能运行。请根据您的操作系统下载对应的运行时：
echo.
echo --------------------------------------------------------------------------------
echo Windows 用户：
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
echo macOS 用户：
echo --------------------------------------------------------------------------------
echo.
echo 下载地址：
echo https://dotnet.microsoft.com/download/dotnet/8.0
echo.
echo 需要下载（根据您的 Mac 芯片类型选择）：
echo.
echo Intel 芯片 Mac：
echo - .NET Runtime 8.0.x - macOS x64
echo   文件名示例：dotnet-runtime-8.0.x-osx-x64.pkg
echo.
echo Apple Silicon (M1/M2/M3) Mac：
echo - .NET Runtime 8.0.x - macOS Arm64
echo   文件名示例：dotnet-runtime-8.0.x-osx-arm64.pkg
echo.
echo 下载后请将安装包放入 Runtimes 文件夹中。
echo.
echo --------------------------------------------------------------------------------
echo 快速下载链接（2026年1月）：
echo --------------------------------------------------------------------------------
echo.
echo Windows x64:
echo https://download.visualstudio.microsoft.com/download/pr/...
echo.
echo macOS x64:
echo https://download.visualstudio.microsoft.com/download/pr/...
echo.
echo macOS Arm64:
echo https://download.visualstudio.microsoft.com/download/pr/...
echo.
echo 注意：具体下载链接请访问官网获取最新版本。
echo.
echo ================================================================================
) > "%RUNTIMES_DIR%\运行时下载说明.txt"

echo [5/5] 创建发布说明...
(
echo ================================================================================
echo                    分镜板 Storyboard - 发布说明
echo ================================================================================
echo.
echo 发布日期: %date% %time%
echo 构建类型: %BUILD_TYPE%
echo.
echo --------------------------------------------------------------------------------
echo 文件夹结构：
echo --------------------------------------------------------------------------------
echo.
echo Release/
echo ├── Windows使用说明.txt          - Windows 用户必读
echo ├── macOS使用说明.txt            - macOS 用户必读
echo ├── Storyboard.exe               - 应用程序主文件
echo ├── Runtimes/                    - .NET 运行时文件夹
echo │   └── 运行时下载说明.txt       - 下载 .NET 8 运行时的说明
echo └── [其他依赖文件]
echo.
echo --------------------------------------------------------------------------------
echo 发布前准备：
echo --------------------------------------------------------------------------------
echo.
echo 1. 下载 .NET 8 运行时安装包
echo    - 访问: https://dotnet.microsoft.com/download/dotnet/8.0
echo    - 下载 Windows Desktop Runtime (x64)
echo    - 下载 macOS Runtime (x64 和 Arm64)
echo.
echo 2. 将下载的运行时安装包放入 Runtimes 文件夹
echo    - windowsdesktop-runtime-8.0.23-win-x64.exe
echo    - dotnet-runtime-8.0.x-osx-x64.pkg
echo    - dotnet-runtime-8.0.x-osx-arm64.pkg
echo.
echo 3. 测试应用程序
echo    - 在干净的测试环境中验证应用程序能否正常运行
echo    - 确认所有依赖文件都已包含
echo.
echo 4. 打包发布
echo    - 将整个 Release 文件夹压缩为 ZIP 或 7z 格式
echo    - 建议文件名: Storyboard-v1.0.0.zip
echo.
echo --------------------------------------------------------------------------------
echo 用户使用流程：
echo --------------------------------------------------------------------------------
echo.
echo Windows 用户:
echo 1. 解压 Release 文件夹
echo 2. 阅读 Windows使用说明.txt
echo 3. 安装 Runtimes 文件夹中的 windowsdesktop-runtime-8.0.23-win-x64.exe
echo 4. 运行 Storyboard.exe
echo.
echo macOS 用户:
echo 1. 解压 Release 文件夹
echo 2. 阅读 macOS使用说明.txt
echo 3. 安装 Runtimes 文件夹中对应芯片的 .NET 运行时
echo 4. 运行 Storyboard
echo.
echo ================================================================================
) > "%RELEASE_DIR%\发布说明.txt"

echo.
echo ================================================================================
echo                              准备完成！
echo ================================================================================
echo.
echo 发布文件夹位置: %RELEASE_DIR%
echo.
echo 下一步操作：
echo 1. 下载 .NET 8 运行时安装包（参考 Runtimes\运行时下载说明.txt）
echo 2. 将运行时安装包放入 Release\Runtimes 文件夹
echo 3. 测试应用程序
echo 4. 打包发布
echo.
echo 详细说明请查看: %RELEASE_DIR%\发布说明.txt
echo.
pause
