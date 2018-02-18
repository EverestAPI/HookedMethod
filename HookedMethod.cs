using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Detour;

namespace HookedMethod
{
    using Hook = HookedMethod;

    class HookedMethod
    {
        /// <summary>
        /// A detour used when hooking a method.
        /// </summary>
        /// <param name="orig">The original method, encapsulated in an OriginalMethod.</param>
        /// <param name="args">The arguments passed to the method, encapsulated in a Parameters object.</param>
        /// <returns>The detoured result.</returns>
        public delegate object Detour(OriginalMethod orig, Parameters args);

        public class OriginalMethod {
            /// <summary>
            /// A compiled trampoline from RuntimeDetour.
            /// </summary>
            /// <param name="args">The arguments to be passed</param>
            /// <returns>The trampoline's return value, casted to an object.</returns>
            delegate object CompiledTrampoline(params object[] args);

            CompiledTrampoline trampoline;

            /// <summary>
            /// Use Linq.Expressions to "compile" the Delegate returned by RuntimeDetour into a statically-typed delegate. This has better performace than a simple Delegate.DynamicInvoke.
            /// </summary>
            /// <param name="del">The Delegate being compiled.</param>
            internal OriginalMethod(Delegate del) {
                var args = Expression.Parameter(typeof(object[]), "args");

                List<Expression> passedArgs = new List<Expression>();

                foreach (var e in del.Method.GetParameters()) {
                    passedArgs.Add(Expression.Convert(Expression.ArrayAccess(args, Expression.Constant(e.Position)), e.ParameterType));
                }

                var call = Expression.Call(Expression.Convert(Expression.Constant(del.Target), del.Method.DeclaringType), del.Method, passedArgs);

                var convert = Expression.Convert(call, typeof(object));

                trampoline = Expression.Lambda<CompiledTrampoline>(call).Compile();
            }

            /// <summary>
            /// Call an OriginalMethod, passing a return type.
            /// </summary>
            /// <param name="args">The arguments to be used.</param>
            /// <returns>The return value of the original method.</returns>
            public T As<T>(params object[] args) => (T) trampoline(args);
        }

        public class Parameters {
            public object[] RawParams { get; set; }
            
            internal Parameters(object[] args) {
                this.RawParams = args;
            }

            public T1 As<T1>() => ((T1) RawParams[0]);
            public (T1, T2) As<T1, T2>() => ((T1) RawParams[0], (T2) RawParams[1]);
            public (T1, T2, T3) As<T1, T2, T3>() => ((T1) RawParams[0], (T2) RawParams[1], (T3) RawParams[2]);
            public (T1, T2, T3, T4) As<T1, T2, T3, T4>() => ((T1) RawParams[0], (T2) RawParams[1], (T3) RawParams[2], (T4) RawParams[3]);
            public (T1, T2, T3, T4, T5) As<T1, T2, T3, T4, T5>() => ((T1) RawParams[0], (T2) RawParams[1], (T3) RawParams[2], (T4) RawParams[3], (T5) RawParams[4]);
            public (T1, T2, T3, T4, T5, T6) As<T1, T2, T3, T4, T5, T6>() => ((T1) RawParams[0], (T2) RawParams[1], (T3) RawParams[2], (T4) RawParams[3], (T5) RawParams[4], (T6) RawParams[5]);
            public (T1, T2, T3, T4, T5, T6, T7) As<T1, T2, T3, T4, T5, T6, T7>() => ((T1) RawParams[0], (T2) RawParams[1], (T3) RawParams[2], (T4) RawParams[3], (T5) RawParams[4], (T6) RawParams[5], (T7) RawParams[6]);
        }

        public delegate object CompiledDetour(params object[] args);
    
        static Dictionary<Guid, CompiledDetour> compiledDetoursByID = new Dictionary<Guid, CompiledDetour>();
    
        static object callCompiledDetour(string id, params object[] args) {
            return compiledDetoursByID[Guid.Parse(id)](args);
        }
    
        static CompiledDetour compileDetour(Detour detour, OriginalMethod original) {
            return args => detour(original, new Parameters(args));
        }

        static MethodBase generateRawDetour(Detour detour, Delegate trampoline) {
            var origMethod = new OriginalMethod(trampoline);

            CompiledDetour compiledDetour = compileDetour(detour, origMethod);
 
            var compiledDetourID = Guid.NewGuid();

            compiledDetoursByID[compiledDetourID] = compiledDetour;
    
            DynamicMethod dynamicDetour = new DynamicMethod("MethodModifier_Hook", trampoline.Method.ReturnType, trampoline.Method.GetParameters().Select(e => e.ParameterType).ToArray(), true);
            ILGenerator generator = dynamicDetour.GetILGenerator(1024);
    
            var argsArr = generator.DeclareLocal(typeof(object[]));
    
            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ldstr, compiledDetourID.ToString());
            generator.Emit(OpCodes.Ldc_I4, trampoline.Method.GetParameters().Length);
            generator.Emit(OpCodes.Newarr, typeof(object));
            generator.Emit(OpCodes.Stloc, argsArr);
            foreach (var e in trampoline.Method.GetParameters()) {
                generator.Emit(OpCodes.Ldloc, argsArr);
                generator.Emit(OpCodes.Ldc_I4, e.Position);
                generator.Emit(OpCodes.Ldarg, e.Position);

                if (e.ParameterType.IsValueType) {
                    generator.Emit(OpCodes.Box, e.ParameterType);
                }

                generator.Emit(OpCodes.Stelem, typeof(object));
            }

            generator.Emit(OpCodes.Ldloc, argsArr);
            generator.EmitCall(OpCodes.Call, typeof(HookedMethod).GetMethod("callCompiledDetour"), null);

            if (trampoline.Method.ReturnType.IsValueType) {
                generator.Emit(OpCodes.Unbox_Any, trampoline.Method.ReturnType);
            } else if (trampoline.Method.ReturnType == typeof(void)) {
                generator.Emit(OpCodes.Ldnull);
            } else {
                generator.Emit(OpCodes.Isinst, trampoline.Method.ReturnType);
            }

            generator.Emit(OpCodes.Ret);

            Delegate rawDetour = dynamicDetour.CreateDelegate(trampoline.GetType());

            return rawDetour.Method;
        }
    }
}