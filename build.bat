@echo off
echo Building SPUHUD Mod...

cd DecompiledCode
dotnet build SPUHUDMod.csproj -c Release

if %ERRORLEVEL% == 0 (
    echo Build successful!
    echo Copying DLL to mod folder...
    copy bin\Release\SPUHUDMod.dll ..\SPUHUDMod.dll
    echo Done! Mod is ready for installation.
) else (
    echo Build failed!
)

pause
