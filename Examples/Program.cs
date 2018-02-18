using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HookedMethod;
using Hook = HookedMethod.HookedMethod;

public class main {
	[MethodImpl(MethodImplOptions.NoInlining)]
	public int detouredMethod(int a, int b) {
		return a + b;
	}

	public static void Main() {
		var mainInst = new main();

		MethodInfoWithDef method = MethodInfoWithDef.FromCall(() => mainInst.detouredMethod(default(int), default(int)));

		Console.WriteLine(((MethodInfo) method).Name);
		Console.WriteLine(mainInst.detouredMethod(3, 5));

		Hook hookInst = new Hook(method, (hook, orig, args) => {
			var (a, b) = args.As<int, int>();

			return orig.As<int>(a, b) + 1;
		});

		Console.WriteLine(mainInst.detouredMethod(3, 5));
	}
}
