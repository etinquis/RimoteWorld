@echo off

set "OutputFile=%~1"
set "RimWorldPath=%~2"
set "RimWorldVersion=%~3"
set "CCLModPath=%~4"
set "CCLVersion=%~5"

echo ^<^?xml version=^"1.0^" encoding=^"utf-8^" ^?^> 				 																						 > %OutputFile%
echo ^<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"^>																				>> %OutputFile%
echo ^<PropertyGroup^> 																																		>> %OutputFile%
echo 	^<RimWorldPath^>%RimWorldPath%\^</RimWorldPath^> 																									>> %OutputFile%
echo 	^<RimWorldVersion^>%RimWorldVersion%^<^/RimWorldVersion^> 																							>> %OutputFile%
echo 	^<RimWorldExeName^>RimWorldWin^<^/RimWorldExeName^> 																								>> %OutputFile%
echo 	^<RimWorldManagedPath^>$(RimWorldExeName)_Data\Managed\^</RimWorldManagedPath^>																		>> %OutputFile%
echo 	^<RimWorldManagedPath Condition=" '$(OS)' == 'Unix' AND Exists ('/Library/Frameworks') "^>Contents\Resources\Data\Managed\^</RimWorldManagedPath^>	>> %OutputFile%
echo 	^<ServerModPath^>%~dp0..\Mod\^<^/ServerModPath^> 																									>> %OutputFile%
echo 	^<CCLModPath^>%CCLModPath%\^<^/CCLModPath^>		 																									>> %OutputFile%
echo 	^<CCLVersion^>%CCLVersion%^<^/CCLVersion^> 																											>> %OutputFile%
echo ^<^/PropertyGroup^> 																																	>> %OutputFile%
echo ^</Project^>																																			>> %OutputFile%