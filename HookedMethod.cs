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
        public delegate object Detour(HookedMethod hook, OriginalMethod orig, Parameters args);

        public class OriginalMethod {
            /// <summary>
            /// A compiled trampoline from RuntimeDetour.
            /// </summary>
            /// <param name="args">The arguments to be passed</param>
            /// <returns>The trampoline's return value, casted to an object.</returns>
            delegate object CompiledTrampoline(object self, params object[] args);

            CompiledTrampoline trampoline;

            /// <summary>
            /// Use Linq.Expressions to "compile" a method returned by RuntimeDetour into a statically-typed delegate. This has better performace than a simple Method.Invoke.
            /// </summary>
            /// <param name="del">The method being called.</param>
            internal OriginalMethod(MethodBase method) {
                var self = Expression.Parameter(typeof(object), "self");
                var args = Expression.Parameter(typeof(object[]), "args");

                List<Expression> passedArgs = new List<Expression>();

                foreach (var e in method.GetParameters()) {
                    passedArgs.Add(Expression.Convert(Expression.ArrayAccess(args, Expression.Constant(e.Position)), e.ParameterType));
                }

                var call = Expression.Call(self, method as MethodInfo, passedArgs);

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
    
        CompiledDetour compileDetour(Detour detour, object original) {
            return args => detour(this, ((ValueTuple<OriginalMethod>)original).Item1, new Parameters(args));
        }

        MethodBase generateRawDetour(Detour detour, MethodInfo origMethod, ValueTuple<OriginalMethod> trampoline) {
            CompiledDetour compiledDetour = compileDetour(detour, trampoline);
 
            var compiledDetourID = Guid.NewGuid();

            compiledDetoursByID[compiledDetourID] = compiledDetour;
    
            DynamicMethod dynamicDetour = new DynamicMethod("Hook", origMethod.ReturnType, origMethod.GetParameters().Select(e => e.ParameterType).ToArray(), true);
            ILGenerator generator = dynamicDetour.GetILGenerator(1024);
    
            var argsArr = generator.DeclareLocal(typeof(object[]));
    
            generator.Emit(OpCodes.Nop);
            generator.Emit(OpCodes.Ldstr, compiledDetourID.ToString());
            generator.Emit(OpCodes.Ldc_I4, origMethod.GetParameters().Length);
            generator.Emit(OpCodes.Newarr, typeof(object));
            generator.Emit(OpCodes.Stloc, argsArr);
            foreach (var e in origMethod.GetParameters()) {
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

            if (origMethod.ReturnType.IsValueType) {
                generator.Emit(OpCodes.Unbox_Any, origMethod.ReturnType);
            } else if (origMethod.ReturnType == typeof(void)) {
                generator.Emit(OpCodes.Ldnull);
            } else {
                generator.Emit(OpCodes.Isinst, origMethod.ReturnType);
            }

            generator.Emit(OpCodes.Ret);

            Func<Type[], Type> getType;
            var isAction = origMethod.ReturnType.Equals((typeof(void)));
            var types = origMethod.GetParameters().Select(p => p.ParameterType);

            if (isAction) {
                getType = Expression.GetActionType;
            } else {
                getType = Expression.GetFuncType;
                types = types.Concat(new[] { origMethod.ReturnType });
            }

            Delegate rawDetour = dynamicDetour.CreateDelegate(getType(types.ToArray()));

            return rawDetour.Method;
        }

        public HookedMethod(MethodInfoWithDef infoWithDef, Detour detour) {
            var trampoline = new ValueTuple<OriginalMethod>(null);

            Func<Type[], Type> getType;
            var isAction = ((MethodInfo)infoWithDef).ReturnType.Equals((typeof(void)));
            var types = ((MethodInfo)infoWithDef).GetParameters().Select(p => p.ParameterType);

            if (isAction) {
                getType = Expression.GetActionType;
            } else {
                getType = Expression.GetFuncType;
                types = types.Concat(new[] { ((MethodInfo)infoWithDef).ReturnType });
            }

            var RTDetour = typeof(RuntimeDetour).GetMethod("Detour", new[] {typeof(MethodBase), typeof(MethodBase)}).MakeGenericMethod(getType(types.ToArray()));

            trampoline.Item1 = new OriginalMethod((MethodBase) RTDetour.Invoke(null, new object[] {infoWithDef, generateRawDetour(detour, infoWithDef, trampoline)}));
        }
    }
}