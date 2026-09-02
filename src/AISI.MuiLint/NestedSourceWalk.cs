using System;
using System.Collections.Generic;

namespace AISI.MuiLint
{
    /// <summary>
    /// Depth-first walk of nested source graphs. Used by the HTML editor tagger to
    /// traverse <c>IProjectionBufferBase.SourceBuffers</c> (elision implements the base,
    /// not <c>IProjectionBuffer</c>).
    /// </summary>
    internal static class NestedSourceWalk
    {
        /// <summary>
        /// Returns every reachable node except <paramref name="root"/>, skipping cycles.
        /// <paramref name="sourcesOf"/> returning <c>null</c> means no children.
        /// </summary>
        public static IReadOnlyList<T> Flatten<T>(T root, Func<T, IEnumerable<T>?> sourcesOf)
            where T : class
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (sourcesOf is null)
            {
                throw new ArgumentNullException(nameof(sourcesOf));
            }

            var result = new List<T>();
            var seen = new HashSet<T>();
            var stack = new Stack<T>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                T current = stack.Pop();
                if (current is null || !seen.Add(current))
                {
                    continue;
                }

                if (!object.ReferenceEquals(current, root))
                {
                    result.Add(current);
                }

                IEnumerable<T>? children = sourcesOf(current);
                if (children is null)
                {
                    continue;
                }

                foreach (T child in children)
                {
                    if (child is object)
                    {
                        stack.Push(child);
                    }
                }
            }

            return result;
        }
    }
}
