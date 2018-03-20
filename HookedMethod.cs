using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Detour;

namespace HookedMethod
{
    public class Hook : HookedMethod {
        public Hook(MethodInfoWithDef infoWithDef, Detour detour) : base(infoWithDef, detour) {}
    }

    public class HookedMethod
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
            delegate object CompiledTrampoline(params object[] args);

            CompiledTrampoline trampoline;

            /// <summary>
            /// Use Linq.Expressions to "compile" a method returned by RuntimeDetour into a statically-typed delegate. This has better performace than a simple Method.Invoke.
            /// </summary>
            /// <param name="del">The method being called.</param>
            internal OriginalMethod(Delegate method) {
                if (method == null)
                    return;

                var args = Expression.Parameter(typeof(object[]), "args");

                List<Expression> passedArgs = new List<Expression>();

                foreach (var e in method.Method.GetParameters()) {
                    passedArgs.Add(Expression.Convert(Expression.ArrayAccess(args, Expression.Constant(e.Position)), e.ParameterType));
                }

                var call = Expression.Invoke(Expression.Constant(method), passedArgs);

                Expression returnValue = method.Method.ReturnType != typeof(void) ? 
                    (Expression) Expression.Convert(call, typeof(object)) : 
                    (Expression) Expression.Block(call, Expression.Constant(null));

                trampoline = Expression.Lambda<CompiledTrampoline>(returnValue, new[] {args}).Compile();
            }

            /// <summary>
            /// Call an OriginalMethod, passing a return type.
            /// </summary>
            /// <param name="args">The arguments to be used.</param>
            /// <returns>The return value of the original method.</returns>
            public virtual T As<T>(params object[] args) => (T) trampoline(args);

            /// <summary>
            /// Call an OriginalMethod, ignoring the return value.
            /// </summary>
            /// <param name="args">The arguments to be used.</param>
            public virtual void Invoke(params object[] args) => trampoline(args);
        }

        internal class InbetweenMethod : OriginalMethod {
            CompiledDetour previousDetour;

            internal InbetweenMethod(CompiledDetour previousDetour)
                : base(null) {
                this.previousDetour = previousDetour;
            }

            public override T As<T>(params object[] args) => (T) previousDetour(args);

            public override void Invoke(params object[] args) => previousDetour(args);
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
    
        static Dictionary<MethodBase, Stack<CompiledDetour>> compiledDetoursByID = new Dictionary<MethodBase, Stack<CompiledDetour>>();
    
        public static object callCompiledDetour(MethodBase id, params object[] args) {
            Stack<CompiledDetour> compiledDetours = compiledDetoursByID[id];
            return compiledDetours.Peek()(args);
        }
    
        CompiledDetour compileDetour(Detour detour, OriginalMethodContainer original) {
            return args => detour(this, original.origMethod, new Parameters(args));
        }

        MethodInfo generateRawDetour(MethodInfo origMethod, OriginalMethodContainer trampoline) {
            var parameters = origMethod.GetParameters().Select(e => e.ParameterType).ToArray();
            
            if (!origMethod.Attributes.HasFlag(MethodAttributes.Static)) parameters = new[] {origMethod.DeclaringType}.Concat(parameters).ToArray();

            DynamicMethod dynamicDetour = new DynamicMethod("Hook", origMethod.ReturnType, parameters, true);
            ILGenerator generator = dynamicDetour.GetILGenerator();
    
            var argsArr = generator.DeclareLocal(typeof(object[]));

            generator.Emit(OpCodes.Ldtoken, origMethod);
            generator.Emit(OpCodes.Call, typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[] { typeof(RuntimeMethodHandle) }));

            generator.Emit(OpCodes.Ldc_I4, parameters.Length);
            generator.Emit(OpCodes.Newarr, typeof(object));
            generator.Emit(OpCodes.Stloc, argsArr);

            var pos = 0;

            foreach (var e in parameters) {
                generator.Emit(OpCodes.Ldloc, argsArr);
                generator.Emit(OpCodes.Ldc_I4, pos);
                generator.Emit(OpCodes.Ldarg, pos);

                if (e.IsValueType) {
                    generator.Emit(OpCodes.Box, e);
                }

                generator.Emit(OpCodes.Stelem, typeof(object));

                pos++;
            }

            generator.Emit(OpCodes.Ldloc, argsArr);
            generator.Emit(OpCodes.Call, typeof(HookedMethod).GetMethod("callCompiledDetour"));

            if (origMethod.ReturnType == typeof(void)) {
                generator.Emit(OpCodes.Pop);
            } else if (origMethod.ReturnType.IsValueType) {
                generator.Emit(OpCodes.Unbox_Any, origMethod.ReturnType);
            } else {
                generator.Emit(OpCodes.Isinst, origMethod.ReturnType);
            }

            generator.Emit(OpCodes.Ret);

            return dynamicDetour;
        }

        class OriginalMethodContainer {
            public OriginalMethod origMethod { get; set; }

            public OriginalMethodContainer(OriginalMethod contained) => origMethod = contained;
        }

        public HookedMethod(MethodInfoWithDef infoWithDef, Detour detour) {
            var trampoline = new OriginalMethodContainer(null);

            CompiledDetour compiledDetour = compileDetour(detour, trampoline);

            Stack<CompiledDetour> history;
            if (!compiledDetoursByID.TryGetValue(infoWithDef, out history)) {
                compiledDetoursByID[infoWithDef] = history = new Stack<CompiledDetour>();
            } else {
                trampoline.origMethod = new InbetweenMethod(history.Peek());
            }
            history.Push(compiledDetour);
            if (history.Count > 1)
                return;

            Func<Type[], Type> getType;
            var isAction = ((MethodInfo)infoWithDef).ReturnType.Equals((typeof(void)));
            var types = ((MethodInfo)infoWithDef).GetParameters().Select(p => p.ParameterType);
            if (!((MethodInfo) infoWithDef).Attributes.HasFlag(MethodAttributes.Static)) types = new[] {((MethodInfo) infoWithDef).DeclaringType}.Concat(types).ToArray();

            if (isAction) {
                getType = Expression.GetActionType;
            } else {
                getType = Expression.GetFuncType;
                types = types.Concat(new[] { ((MethodInfo)infoWithDef).ReturnType });
            }
            
            var rawDetour = generateRawDetour(infoWithDef, trampoline);

            var RTDetour = typeof(RuntimeDetour).GetMethods().Single(e => e.Name == "Detour" && e.GetParameters()[0].ParameterType == typeof(MethodBase) && e.GetParameters()[1].ParameterType == typeof(MethodBase) && e.IsGenericMethod).MakeGenericMethod(getType(types.ToArray()));
            var rawTrampoline = RTDetour.Invoke(null, new object[] {((MethodBase) ((MethodInfo) infoWithDef)), rawDetour});

            trampoline.origMethod = new OriginalMethod((Delegate) rawTrampoline);
        }
    }
}
