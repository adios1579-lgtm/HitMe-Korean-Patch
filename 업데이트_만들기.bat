@echo off
cd /d "%~dp0"
set /p HITME_VERSION=Version (x.y.z):
set /p HITME_NOTES=Release notes:
py -3.11 -X utf8 build_release.py update --version "%HITME_VERSION%" --notes "%HITME_NOTES%"
pause
