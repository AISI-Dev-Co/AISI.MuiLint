using System;
using System.Collections.Generic;

namespace AISI.MuiLint
{
    /// <summary>Depth-first walk of nested source graphs, such as <c>IProjectionBufferBase.SourceBuffers</c> for the HTML editor tagger.</summary>
    internal static class NestedSourceWalk
    {
        /// <summary>Returns every node reachable through <paramref name="sourcesOf"/> except <paramref name="root"/>, skipping cycles.</summary>
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
                if (!seen.Add(current))
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
