using System;
using System.Reflection;
using HookedMethod;

public class main {
	public static void Main() {
		MethodInfo method = MethodInfoWithDef.FromCall(() => Console.WriteLine());

		Console.WriteLine(method.Name);
	}
}
