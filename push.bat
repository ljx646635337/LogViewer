@echo off
echo Start push...
scp -r publish-linux/* laijianxin@10.236.100.20:/data/nginx/logViewer/
echo Push Done!
pause
