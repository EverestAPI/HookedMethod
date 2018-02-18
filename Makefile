.PHONY: MethodInfoWithDef.cs HookedMethod.cs Examples/Program.cs

all: examples

bin/HookedMethod.dll: MethodInfoWithDef.cs HookedMethod.cs
	mkdir -p bin
	@csc /out:./bin/HookedMethod.dll /t:library /debug /reference:MonoMod.exe /reference:Mono.Cecil.0.9.6.4/lib/net45/Mono.Cecil.dll HookedMethod.cs MethodInfoWithDef.cs
	cp Mono.Cecil.0.9.6.4/lib/net45/Mono.Cecil.dll MonoMod.exe bin/

bin/Examples.exe: Examples/Program.cs bin/HookedMethod.dll
	@csc /out:./bin/Examples.exe /debug /reference:bin/HookedMethod.dll Examples/Program.cs

examples: bin/Examples.exe
