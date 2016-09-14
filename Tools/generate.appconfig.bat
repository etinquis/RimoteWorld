@echo off

set "OutputFile=%~1"
set "RimWorldPath=%~2"
set "RimWorldVersion=%~3"
set "CCLModPath=%~4"
set "CCLVersion=%~5"

echo ^<^?xml version=^"1.0^" encoding=^"utf-8^" ^?^> 				 						 > %OutputFile%
echo ^<^configuration^> 				 													>> %OutputFile%
echo 	^<^appSettings^> 				 													>> %OutputFile%
echo 		^<^add key="RimWorldPath" value="%RimWorldPath%\" /^> 							>> %OutputFile%
echo 		^<^add key="RimWorldVersion" value="%RimWorldVersion%" /^>						>> %OutputFile%
echo 		^<^add key="RimWorldExeName" value="RimWorldWin.exe" /^>						>> %OutputFile%
echo 		^<^add key="ServerModPath" value="%~dp0..\Mod" /^> 								>> %OutputFile%
echo 		^<^add key="CCLModPath" value="%CCLModPath%" /^> 								>> %OutputFile%
echo 		^<^add key="CCLVersion" value="%CCLVersion%" /^> 								>> %OutputFile%
echo 	^<^/appSettings^> 				 													>> %OutputFile%
echo ^<^/configuration^> 				 													>> %OutputFile%