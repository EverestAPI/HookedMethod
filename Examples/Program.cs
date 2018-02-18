using System;
using System.Reflection;
using HookedMethod;

public class main {
	public static int detouredMethod(int a, int b) {
		return a + b;
	}

	public static void Main() {
		MethodInfoWithDef method = MethodInfoWithDef.FromCall(() => detouredMethod(default(int), default(int)));

		Console.WriteLine(((MethodInfo) method).Name);
		Console.WriteLine(detouredMethod(3, 5));

		HookedMethod.HookedMethod hookInst = new HookedMethod.HookedMethod(method, (hook, orig, args) => {
			var (a, b) = args.As<int, int>();

			return orig.As<int>(a, b) + 1;
		});

		Console.WriteLine(detouredMethod(3, 5));
	}
}
