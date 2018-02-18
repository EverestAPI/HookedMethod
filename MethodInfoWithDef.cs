using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Mono.Cecil;

namespace HookedMethod
{
    public class MethodInfoWithDef
    {
        MethodDefinition definition;
        MethodInfo info;

        /// <summary>
        /// Creates a MethodInfoWithDef with both a MethodInfo and a MethodDefinition.
        /// </summary>
        /// <param name="info">The MethodInfo to contain.</param>
        /// <param name="def">The MethodInfo to contain.</param>
        public MethodInfoWithDef(MethodInfo info, MethodDefinition def) {
            this.info = info;
            this.definition = def;
        }

        /// <summary>
        /// Creates a MethodInfoWithDef from only a MethodInfo, automatically resolving the definition.
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public static MethodInfoWithDef CreateAndResolveDef(MethodInfo info) {
            var module = ModuleDefinition.ReadModule (new MemoryStream(File.ReadAllBytes(info.DeclaringType.Module.FullyQualifiedName)));
			var declaring_type = (TypeDefinition) module.LookupToken (info.DeclaringType.MetadataToken);
			var def = (MethodDefinition) declaring_type.Module.LookupToken(info.MetadataToken);

            return new MethodInfoWithDef(info, def);
        }

        /// <summary>
        /// Convert a void-returning lambda MethodCallExpression to a MethodInfoWithDef.
        /// </summary>
        /// <param name="expr">The expression to convert.</param>
        /// <returns>The converted MethodInfoWithDef.</returns>
        public static MethodInfoWithDef FromCall<T>(Expression<Func<T>> expr) {
            return CreateAndResolveDef((expr.Body as MethodCallExpression).Method);
        }

        /// <summary>
        /// Convert a void-returning lambda MethodCallExpression to a MethodInfoWithDef.
        /// </summary>
        /// <param name="expr">The expression to convert.</param>
        /// <returns>The converted MethodInfoWithDef.</returns>
        public static MethodInfoWithDef FromCall(Expression<Action> expr) {
            return CreateAndResolveDef((expr.Body as MethodCallExpression).Method);
        }

        /// <summary>
        /// Get the MethodDefinition out of a MethodInfoWithDef.
        /// </summary>
        /// <param name="self">The MethodInfoWithDef being accessed.</param>
        public static implicit operator MethodDefinition(MethodInfoWithDef self) => self.definition;

        /// <summary>
        /// Get the MethodDefinition out of a MethodInfoWithDef.
        /// </summary>
        /// <param name="self">The MethodInfoWithDef being accessed.</param>
        public static implicit operator MethodInfo(MethodInfoWithDef self) => self.info;

        /// <summary>
        /// Sugar for CreateAndResolveDef.
        /// </summary>
        /// <param name="info">The MethodInfo being processed.</param>
        public static explicit operator MethodInfoWithDef(MethodInfo info) => CreateAndResolveDef(info);
    }
}
