cd %1

del /f /q BuildArtifacts\FlexibleServiceHost.*

Tools\ILMerge\ILMerge.exe /t:library /closed /xmldocs /useFullPublicKeyForReferences /out:BuildArtifacts\FlexibleServiceHost.dll BuildArtifacts\Release\FlexibleServiceHost.dll BuildArtifacts\Release\NLog.dll