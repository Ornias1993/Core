@echo off
setlocal
cd /d %~dp0
set "script_dir=%cd%"
echo "Starting Unitystation buildscript from: %script_dir%"
cd ..
cd UnityProject
set "project_dir=%cd%"
echo "Starting to build from Unityproject directory: %project_dir%"
    echo "Attempting build of UnityStation for Windows"

    "F:\Dev\Unity\2018.3.14f1\Editor\Unity.exe" -batchmode -nographics -silent-crashes -logFile %script_dir%/Logs/WindowsBuild.log -projectPath  %project_dir% -executeMethod BuildScript.PerformWindowsBuild -quit

    echo "Attempting build of UnityStation for OSX"
    "F:\Dev\Unity\2018.3.14f1\Editor\Unity.exe" -batchmode -nographics -silent-crashes -logFile %script_dir%/Logs/WindowsBuild.log -projectPath  %project_dir% -executeMethod BuildScript.PerformOSXBuild -quit

    echo "Attempting build of UnityStation for Linux"
    "F:\Dev\Unity\2018.3.14f1\Editor\Unity.exe" -batchmode -nographics -silent-crashes -logFile %script_dir%/Logs/WindowsBuild.log -projectPath  %project_dir% -executeMethod BuildScript.PerformLinuxBuild -quit

    echo "Attempting build of UnityStation Server"
    "F:\Dev\Unity\2018.3.14f1\Editor\Unity.exe" -batchmode -nographics -silent-crashes -logFile %script_dir%/Logs/WindowsBuild.log -projectPath  %project_dir% -executeMethod BuildScript.PerformServerBuild -quit

    echo "Building finished successfully"

echo "Post processing builds"

echo "Post-Processing done"


echo "Starting upload to steam"
echo "Please enter your steam developer-upload credentials"
set /p Username="Enter Steam Username: "
set /p Password="Enter Steam Password: "

%script_dir%\ContentBuilder\builder\steamcmd.exe +login %Username% %Password% +run_app_build %script_dir%\ContentBuilder\scripts\app_build_787180.vdf +run_app_build %script_dir%\ContentBuilder\scripts\app_build_792890.vdf +quit