using Xunit;

namespace ConfigEditor.Tests
{
    public class LeafNodeTests
    {
        [Fact]
        public void LeafNode_HoldsValueCorrectly()
        {
            var node = new LeafNode(123);
            Assert.Equal(123, node.GetValue());
        }

        [Fact]
        public void LeafNode_MarksDirtyOnChange()
        {
            var node = new LeafNode(100);
            Assert.False(node.IsDirty);
            node.SetValue(200);
            Assert.True(node.IsDirty);
        }
    }
}
