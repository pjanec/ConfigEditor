using JsonConfigEditor.ViewModels;
using JsonConfigEditor.Core.Dom;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;

namespace JsonConfigEditor.Views
{
    public class NodeValueTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? DisplayStringTemplate { get; set; }
        public DataTemplate? DisplayBooleanTemplate { get; set; }
        public DataTemplate? DisplayNumberTemplate { get; set; }
        public DataTemplate? DisplayArrayTemplate { get; set; }
        public DataTemplate? DisplayObjectTemplate { get; set; }
        public DataTemplate? DisplayRefTemplate { get; set; }
        public DataTemplate? DisplaySchemaOnlyTemplate { get; set; }
        public DataTemplate? DisplayAddItemTemplate { get; set; }

        public DataTemplate? EditStringTemplate { get; set; }
        public DataTemplate? EditBooleanTemplate { get; set; }
        public DataTemplate? EditNumberTemplate { get; set; }
        public DataTemplate? EditEnumTemplate { get; set; }
        public DataTemplate? EditRefTemplate { get; set; }
        public DataTemplate? EditSchemaOnlyTemplate { get; set; }
        public DataTemplate? EditAddItemTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is not DataGridRowItemViewModel vm)
                return base.SelectTemplate(item, container);

            // If not in edit mode or not editable, select display template
            if (!vm.IsInEditMode || !vm.IsEditable)
            {
                if (vm.IsAddItemPlaceholder)
                    return DisplayAddItemTemplate ?? DisplayStringTemplate;

                if (vm.IsSchemaOnlyNode)
                    return DisplaySchemaOnlyTemplate ?? DisplayStringTemplate;

                if (vm.DomNode is ValueNode valueNode)
                {
                    return valueNode.Value.ValueKind switch
                    {
                        JsonValueKind.True or JsonValueKind.False => DisplayBooleanTemplate ?? DisplayStringTemplate,
                        JsonValueKind.Number => DisplayNumberTemplate ?? DisplayStringTemplate,
                        _ => DisplayStringTemplate
                    };
                }

                if (vm.DomNode is RefNode)
                    return DisplayRefTemplate ?? DisplayStringTemplate;

                if (vm.DomNode is ArrayNode)
                    return DisplayArrayTemplate ?? DisplayStringTemplate;

                if (vm.DomNode is ObjectNode)
                    return DisplayObjectTemplate ?? DisplayStringTemplate;

                return DisplayStringTemplate;
            }
            else // Edit mode
            {
                if (vm.IsAddItemPlaceholder)
                    return EditAddItemTemplate ?? EditStringTemplate;

                if (vm.IsSchemaOnlyNode)
                    return EditSchemaOnlyTemplate ?? EditStringTemplate;

                if (vm.DomNode is ValueNode valueNode)
                {
                    // Check schema first for enum/allowed values
                    if (vm.SchemaContextNode?.AllowedValues?.Count > 0)
                        return EditEnumTemplate ?? EditStringTemplate;

                    return valueNode.Value.ValueKind switch
                    {
                        JsonValueKind.True or JsonValueKind.False => EditBooleanTemplate ?? EditStringTemplate,
                        JsonValueKind.Number => EditNumberTemplate ?? EditStringTemplate,
                        _ => EditStringTemplate
                    };
                }

                if (vm.DomNode is RefNode)
                    return EditRefTemplate ?? EditStringTemplate;

                return EditStringTemplate;
            }
        }
    }
} 