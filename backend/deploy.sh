#!/bin/bash
# LogViewer 发布并推送到 Linux 服务器

set -e

# ===== 配置 =====
SERVER="10.236.100.20"
PORT="22"
USER="root"
REMOTE_PATH="/data/nginx/logViewer"

# ===== 1. 发布 Linux 版本 =====
echo "[1/2] 正在发布 Linux 版本..."
cd "$(dirname "$0")"
dotnet publish LogViewer.Api.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./publish-linux
echo "[OK] 发布完成"

# ===== 2. 推送到服务器 =====
echo "[2/2] 正在推送到服务器 ${SERVER}:${REMOTE_PATH} ..."
scp -r -P ${PORT} ./publish-linux/* ${USER}@${SERVER}:${REMOTE_PATH}/
echo "[OK] 推送完成"
echo ""
echo "下一步：在服务器上重启服务"
echo "  systemctl restart logviewer"
