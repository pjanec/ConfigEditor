using System;
using System.Text.Json;

namespace ConfigEditor.Dom
{
    public class LeafNode : DomNode
    {
        private object? _value;

        public LeafNode(object? value)
        {
            _value = value;
        }

        public override JsonValueKind ValueKind => _value switch
        {
            null => JsonValueKind.Null,
            string => JsonValueKind.String,
            bool => JsonValueKind.True, // we don't distinguish between true/false in kind
            int or long or float or double or decimal => JsonValueKind.Number,
            _ => throw new InvalidOperationException($"Unsupported leaf value type: {_value?.GetType()}")
        };

        public override object? GetValue() => _value;

        public override void SetValue(object? value)
        {
            if (_value == null || !_value.Equals(value))
            {
                _value = value;
                MarkDirty();
            }
        }

        public string? GetAsString() => _value?.ToString();

        public T? GetAs<T>() => _value is T val ? val : default;
    }
}
