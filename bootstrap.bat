@echo off

echo hint: If you cannot paste with ctrl+v, right-click this window and hit the 'Paste' option
echo.

call :ColorText 02 "Please enter the path to your RimWorld installation directory"
echo :
echo|set /p="> "
set /p RimWorldPath=

if NOT EXIST "%RimWorldPath%" (
	call :ColorText 4f "Path does not exist."
	echo.
	exit /b -1
)
if NOT EXIST "%RimWorldPath%/Mods" (
	call :ColorText 4f "The given path does not contain a mods folder."
	echo.
	exit /b -1
)

echo.
call :ColorText 02 "Please enter the build number"
echo|set /p=": "
echo (for '0.15.1284 rev134' enter 0.15.1284.134)
echo|set /p="> "
set /p RimWorldVersion=

echo.
call :ColorText 02 "Please enter the path to the Community Core Library Mod folder"
echo :
echo|set /p="> "
set /p CCLModPath=

if NOT EXIST "%CCLModPath%" (
	call :ColorText 4f "Path does not exist."
	echo.
	exit /b -1
)
if NOT EXIST "%CCLModPath%/Assemblies" (
	call :ColorText 4f "The given path does not contain an Assemblies folder."
	echo.
	exit /b -1
)
if NOT EXIST "%CCLModPath%/Assemblies/Community Core Library.dll" (
	call :ColorText 4f "The assemblies folder does not contain Community Core Library.dll."
	echo.
	exit /b -1
)

echo.
call :ColorText 02 "Please enter the build number"
echo :
echo|set /p="> "
set /p CCLVersion=

call Tools\generate.targets.bat "RimWorld.targets" "%RimWorldPath%" "%RimWorldVersion%" "%CCLModPath%" "%CCLVersion%"
call Tools\generate.appconfig.bat "RimoteWorld.FullStack.Tests\app.config" "%RimWorldPath%" "%RimWorldVersion%" "%CCLModPath%" "%CCLVersion%"

goto :eof

REM http://stackoverflow.com/a/8553962
:ColorText Color String
@echo off
::
:: Prints String in color specified by Color.
::
::   Color should be 2 hex digits
::     The 1st digit specifies the background
::     The 2nd digit specifies the foreground
::     See COLOR /? for more help
::
::   String is the text to print. All quotes will be stripped.
::     The string cannot contain any of the following: * ? < > | : \ /
::     Also, any trailing . or <space> will be stripped.
::
::   The string is printed to the screen without issuing a <newline>,
::   so multiple colors can appear on one line. To terminate the line
::   without printing anything, use the ECHO( command.
::
setlocal
pushd %temp%
for /F "tokens=1 delims=#" %%a in ('"prompt #$H#$E# & echo on & for %%b in (1) do rem"') do (
  <nul set/p"=%%a" >"%~2"
)
findstr /v /a:%1 /R "^$" "%~2" nul
del "%~2" > nul 2>&1
popd
exit /b