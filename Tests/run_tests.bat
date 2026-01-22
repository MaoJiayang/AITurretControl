@echo off

:: Build the project
cd /d "%~dp0"
echo Building the project...
dotnet build
if %errorlevel% neq 0 (
    echo Build failed. Exiting.
    exit /b %errorlevel%
)

echo.

:: Run the project
echo Running the project...
dotnet run
if %errorlevel% neq 0 (
    echo Run failed. Exiting.
    exit /b %errorlevel%
)

echo.

:: Visualize results
echo Visualizing results...
uv run python .\visualize_results.py
if %errorlevel% neq 0 (
    echo Visualization failed. Exiting.
    exit /b %errorlevel%
)

echo.
echo All tasks completed successfully!