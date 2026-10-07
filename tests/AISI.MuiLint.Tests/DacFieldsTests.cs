using System.Linq;
using Xunit;

namespace AISI.MuiLint.Tests
{
    public sealed class DacFieldsTests
    {
        [Fact]
        public void ReadsEveryPropertyOfEachCacheExtension()
        {
            const string cs =
                "using PX.Data;\n" +
                "namespace MyExt\n" +
                "{\n" +
                "    // public virtual string UsrCommentedOut { get; set; }\n" +
                "    public sealed class SOOrderExt : PXCacheExtension<PX.Objects.SO.SOOrder>\n" +
                "    {\n" +
                "        [PXDBString(10)]\n" +
                "        [PXUIField(DisplayName = \"Priority { not a body }\")]\n" +
                "        public string UsrPriority { get; set; }\n" +
                "        public abstract class usrPriority : PX.Data.BQL.BqlString.Field<usrPriority> { }\n" +
                "\n" +
                "        [PXString]\n" +
                "        public virtual string ShippingNote { get; set; }\n" +
                "    }\n" +
                "\n" +
                "    public class SOLineExt2 : PXCacheExtension<SOLineExt, SOLine> { public virtual int? UsrQty { get; set; } }\n" +
                "\n" +
                "    public class NotAnExtension { public string UsrNope { get; set; } }\n" +
                "}\n";

            Assert.Equal(
                new[] { "UsrPriority SOOrderExt SOOrder", "ShippingNote SOOrderExt SOOrder", "UsrQty SOLineExt2 SOLine" },
                DacFields.Read(cs).Select(f => f.Name + " " + f.Extension + " " + f.Dac));
        }

        [Fact]
        public void FilesWithoutCacheExtensionsAreSkipped()
        {
            Assert.Empty(DacFields.Read("public class Graph : PXGraph<Graph> { public string UsrX { get; set; } }"));
        }
    }
}
