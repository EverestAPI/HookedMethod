using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HookedMethod;
using Hook = HookedMethod.HookedMethod; // Create an alias for HookedMethod.

public class main {
	[MethodImpl(MethodImplOptions.NoInlining)] // This line may be automatically added by MonoMod in the future; ignore this for now. It fixes an issue where the patched method gets replaced by (in this example) 8 instead of calling the detour.
	public int detouredMethod(int a, int b) {
		return a + b;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static int detouredStaticMethod(int a, int b) {
		return a * b;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void detouredVoidMethod(int a, int b) {
		Console.WriteLine("original voidmethod " + a + " " + b);
	}

	public static void Main() {
		MethodInfoWithDef instanceMethodInfo = MethodInfoWithDef.FromCall(() => (new main()).detouredMethod(default(int), default(int))); // Create a new main object and "call" the detoured method. The instantiation and call get converted by the compiler into a MethodInfoWithDef without actually being executed, so it has no side-effects.
		MethodInfoWithDef staticMethodInfo = MethodInfoWithDef.FromCall(() => detouredStaticMethod(default(int), default(int))); // You can do the same thing for a static method.
		MethodInfoWithDef voidMethodInfo = MethodInfoWithDef.FromCall(() => detouredVoidMethod(default(int), default(int))); // You can do the same thing for a method with a void return type.

		Console.WriteLine(((MethodInfo) instanceMethodInfo).Name); // Get the name of the instance method. This is just an example of MethodInfoWithDef.
		Console.WriteLine((new main()).detouredMethod(3, 5)); // Create a new main object, and show what it returns before it's detoured.
		Console.WriteLine(detouredStaticMethod(2, 3)); // Show what the static method being detoured returns before being hooked.
		detouredVoidMethod(1, 2); // Show what the void-returning method logs before being hooked.

		Hook hookInst = new Hook(instanceMethodInfo, (hook, orig, args) => {
			var (self, a, b) = args.As<main, int, int>(); // Get the parameters and the instance.

			return orig.As<int>(self, a, b) + 1; // Forward everything to the original method.
		});

		Hook hookStatic = new Hook(staticMethodInfo, (hook, orig, args) => {
			var (a, b) = args.As<int, int>(); // You don't specify the type in a static method.

			return orig.As<int>(a, b) + 1; // There isn't a fake first parameter.
		});

		Hook hookVoid = new Hook(voidMethodInfo, (hook, orig, args) => {
			var (a, b) = args.As<int, int>(); // You don't specify the type in a static method.

			Console.WriteLine("detoured voidmethod " + a + " " + b);
			orig.Invoke(a, b); // You use Invoke instead of As for a method that returns void.

			return null; // Return null for a method that returns void.
		});

		Console.WriteLine((new main()).detouredMethod(3, 5)); // Show what the patched instance method now returns.
		Console.WriteLine(detouredStaticMethod(2, 3)); // Show what the patched static method now returns.
		detouredVoidMethod(1, 2); // Show what the patched void-returning method now returns.
	}
}
