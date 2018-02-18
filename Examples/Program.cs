using System;
using System.Reflection;
using HookedMethod;

public class main {
	public static void Main() {
		Console.WriteLine(((MethodInfo) (MethodInfoWithDef.FromCall(() => Console.WriteLine()))).Name);
	}
}
