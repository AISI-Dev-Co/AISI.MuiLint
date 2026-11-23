using System.Collections.Generic;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class NestedSourceWalkTests
    {
        [Fact]
        public void Flatten_FindsDocument_WhenWalkerUsesProjectionBase()
        {
            Node document = new Node("doc");
            Node elision = new Node("elision")
            {
                SourcesIfProjection = null,
                SourcesIfBase = new[] { document },
            };

            IReadOnlyList<Node> found = NestedSourceWalk.Flatten(elision, n => n.SourcesIfBase);

            Assert.Contains(document, found);
        }

        [Fact]
        public void Flatten_MissesElision_WhenWalkerUsesProjectionOnly()
        {
            Node document = new Node("doc");
            Node elision = new Node("elision")
            {
                SourcesIfProjection = null,
                SourcesIfBase = new[] { document },
            };

            IReadOnlyList<Node> found = NestedSourceWalk.Flatten(elision, n => n.SourcesIfProjection);

            Assert.DoesNotContain(document, found);
        }

        [Fact]
        public void Flatten_WalksNestedElision()
        {
            Node document = new Node("doc");
            Node inner = new Node("inner") { SourcesIfBase = new[] { document } };
            Node outer = new Node("outer") { SourcesIfBase = new[] { inner } };

            IReadOnlyList<Node> found = NestedSourceWalk.Flatten(outer, n => n.SourcesIfBase);

            Assert.Contains(inner, found);
            Assert.Contains(document, found);
        }

        private sealed class Node
        {
            public Node(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public IEnumerable<Node>? SourcesIfProjection { get; set; }

            public IEnumerable<Node>? SourcesIfBase { get; set; }
        }
    }
}
