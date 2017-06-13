csc /t:library /out:MobilizerRt.dll /debug+ ..\MobilizerRt\*.cs
csc /t:library /out:Mobilizer.dll /debug+ ..\Mobilizer\*.cs /r:MobilizerRt.dll
csc /t:exe /out:Mobilize.exe /debug+ ..\Mobilize\*.cs /r:Mobilizer.dll
csc /t:exe /out:fact.exe ..\fact\*.cs /r:MobilizerRt.dll
clix mobilize fact.exe
del /Q fact.exe
ren m_fact.exe fact.exe
csc /t:exe /out:host.exe /debug+ ..\host\*.cs /r:MobilizerRt.dll


