#!/bin/bash
echo "========================================"
echo "  LogViewer Linux 发布脚本"
echo "========================================"
echo ""

cd "$(dirname "$0")/backend"

echo "[1/2] 正在编译并发布 Linux 版本..."
dotnet publish LogViewer.Api.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ../publish-linux

if [ $? -ne 0 ]; then
    echo ""
    echo "[错误] 发布失败，请检查上面的错误信息"
    exit 1
fi

echo ""
echo "[2/2] 发布完成！"
echo ""
ls -la ../publish-linux/LogViewer* 2>/dev/null

if [ -f "../publish-linux/LogViewer.Api" ]; then
    echo ""
    echo "✅ 发布成功！文件在: $(dirname "$0")/publish-linux/"
    echo "✅ 可执行文件: LogViewer.Api"
else
    echo ""
    echo "⚠️ 未找到 LogViewer.Api，可能发布配置有问题"
fi

echo ""
echo "发布完成！下一步："
echo "  1. 把 publish-linux 里的文件上传到服务器 /opt/logviewer/"
echo "  2. 问大哥要 MySQL 的用户名密码"
echo "  3. 配置服务器上的 appsettings.json"
