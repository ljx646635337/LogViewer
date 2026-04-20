@echo off
chcp 65001 >nul
echo ========================================
echo   LogViewer Linux 发布脚本
echo ========================================
echo.

cd /d "%~dp0backend"

echo [1/2] 正在编译并发布 Linux 版本...
dotnet publish LogViewer.Api.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ..\publish-linux

if %errorlevel% neq 0 (
    echo.
    echo [错误] 发布失败，请检查上面的错误信息
    pause
    exit /b 1
)

echo.
echo [2/2] 发布完成！
echo.

dir /b ..\publish-linux\LogViewer* 2>nul
if exist "..\publish-linux\LogViewer.Api" (
    echo.
    echo ✅ 发布成功！文件在: %~dp0publish-linux\
    echo ✅ 可执行文件: LogViewer.Api
) else (
    echo.
    echo ⚠️ 未找到 LogViewer.Api，可能发布配置有问题
)

echo.
echo 发布完成！
echo.
pause
