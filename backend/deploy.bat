@echo off
chcp 65001 >nul
echo [1/2] 正在发布 Linux 版本...
cd /d "%~dp0"
dotnet publish LogViewer.Api.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish-linux
echo [OK] 发布完成

echo [2/2] 正在推送到服务器...
scp -r -P 22 ./publish-linux/* root@10.236.100.20:/data/nginx/logViewer/
echo [OK] 推送完成

echo.
echo 下一步：在服务器上重启服务
echo   systemctl restart logviewer
pause
