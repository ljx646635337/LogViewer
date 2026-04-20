@echo off
echo Start push...
scp -r publish-linux/* linux@xxxxx:/data/nginx/logViewer/
echo Push Done!
pause
