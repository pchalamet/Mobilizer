del /Q m_fact.exe
csc /t:library /out:MobilizerRt.dll /debug+ ..\MobilizerRt\*.cs
csc /t:library /out:Mobilizer.dll /debug+ ..\Mobilizer\*.cs /r:MobilizerRt.dll
csc /t:exe /out:Mobilize.exe /debug+ ..\Mobilize\*.cs /r:Mobilizer.dll
csc /t:exe /out:fact.exe ..\fact\*.cs /r:MobilizerRt.dll
mobilize fact.exe
ildasm /out=m_fact.il m_fact.exe
ilasm /debug /out=m_fact.exe m_fact.il
csc /t:exe /out:host.exe /debug+ ..\host\*.cs /r:MobilizerRt.dll


