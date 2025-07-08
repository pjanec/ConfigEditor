using JsonConfigEditor.ViewModels;
using RuntimeConfig.Core.Dom;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using JsonConfigEditor.Wpf.Services;
using JsonConfigEditor.Contracts.Rendering;
using JsonConfigEditor.Contracts.Editors;
using RuntimeConfig.Core.Schema;
using System;

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
        public DataTemplate? DisplayEnumTemplate { get; set; }

        public DataTemplate? EditStringTemplate { get; set; }
        public DataTemplate? EditBooleanTemplate { get; set; }
        public DataTemplate? EditNumberTemplate { get; set; }
        public DataTemplate? EditEnumTemplate { get; set; }
        public DataTemplate? EditRefTemplate { get; set; }
        public DataTemplate? EditSchemaOnlyTemplate { get; set; }
        public DataTemplate? EditAddItemTemplate { get; set; }

        public DataTemplate? ModalEditorButtonTemplate { get; set; }

        public CustomUIRegistryService? UiRegistry { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is not DataGridRowItemViewModel vm)
                return base.SelectTemplate(item, container);

            Type? targetType = vm.SchemaContextNode?.ClrType;
            DataTemplate? customTemplate = null;

            if (UiRegistry != null && targetType != null)
            {
                if (!vm.IsInEditMode || !vm.IsEditable) // DISPLAY MODE
                {
                    IValueRenderer? customRenderer = UiRegistry.GetValueRenderer(targetType);
                    if (customRenderer != null)
                    {
                        customTemplate = customRenderer.GetDisplayTemplate(vm);
                    }
                }
                else // EDIT MODE
                {
                    var editorInfo = UiRegistry.GetValueEditor(targetType);
                    if (editorInfo?.Editor != null)
                    {
                        if (editorInfo.Value.RequiresModal)
                        {
                            vm.ModalEditorInstance = editorInfo.Value.Editor;
                            return ModalEditorButtonTemplate; // This remains a specific template
                        }
                        else
                        {
                            customTemplate = editorInfo.Value.Editor.GetEditTemplate(vm);
                        }
                    }
                }
            }

            if (customTemplate != null) return customTemplate;

            // Fallback to existing logic if no custom UI component found or UiRegistry not set
            if (!vm.IsInEditMode || !vm.IsEditable)
            {
                return SelectDisplayTemplate(vm);
            }
            else
            {
                return SelectEditTemplate(vm);
            }
        }

        private DataTemplate? SelectDisplayTemplate(DataGridRowItemViewModel vm)
        {
            if (vm.IsAddItemPlaceholder)
                return DisplayAddItemTemplate ?? DisplayStringTemplate;

            if (vm.IsSchemaOnlyNode)
                return SelectSchemaOnlyDisplayTemplate(vm);

            if (vm.DomNode is ValueNode valueNode)
                return SelectValueNodeDisplayTemplate(vm, valueNode);

            if (vm.DomNode is RefNode)
                return DisplayRefTemplate ?? DisplayStringTemplate;

            if (vm.DomNode is ArrayNode)
                return DisplayArrayTemplate ?? DisplayStringTemplate;

            if (vm.DomNode is ObjectNode)
                return DisplayObjectTemplate ?? DisplayStringTemplate;

            return DisplayStringTemplate;
        }

        private DataTemplate? SelectEditTemplate(DataGridRowItemViewModel vm)
        {
            if (vm.IsAddItemPlaceholder)
                return EditAddItemTemplate ?? EditStringTemplate;

            if (vm.IsSchemaOnlyNode)
                return SelectSchemaOnlyEditTemplate(vm);

            if (vm.DomNode is ValueNode valueNode)
                return SelectValueNodeEditTemplate(vm, valueNode);

            if (vm.DomNode is RefNode)
                return EditRefTemplate ?? EditStringTemplate;

            return EditStringTemplate;
        }

        private DataTemplate? SelectSchemaOnlyDisplayTemplate(DataGridRowItemViewModel vm)
        {
            if (vm.IsEnumBased)
                return DisplayEnumTemplate ?? DisplayStringTemplate;
            
            return SelectTemplateBySchemaType(vm.SchemaContextNode?.ClrType, DisplayBooleanTemplate, DisplayNumberTemplate, DisplaySchemaOnlyTemplate, DisplayStringTemplate);
        }

        private DataTemplate? SelectSchemaOnlyEditTemplate(DataGridRowItemViewModel vm)
        {
            if (vm.IsEnumBased)
                return EditEnumTemplate ?? EditStringTemplate;
            
            return SelectTemplateBySchemaType(vm.SchemaContextNode?.ClrType, EditBooleanTemplate, EditNumberTemplate, EditSchemaOnlyTemplate, EditStringTemplate);
        }

        private DataTemplate? SelectValueNodeDisplayTemplate(DataGridRowItemViewModel vm, ValueNode valueNode)
        {
            // Debug output
            System.Diagnostics.Debug.WriteLine($"Template Selector - DOM Node: {valueNode.Path}, JSON Type: {valueNode.Value.ValueKind}, Schema Type: {vm.SchemaContextNode?.ClrType?.Name}, Schema Allowed Values: {vm.SchemaContextNode?.AllowedValues?.Count ?? 0}");
            System.Diagnostics.Debug.WriteLine($"Template Selector - SchemaContextNode is null: {vm.SchemaContextNode == null}, SchemaContextNode Name: {vm.SchemaContextNode?.Name}");
            
            // First check if schema has allowed values (enum)
            if (vm.IsEnumBased )
                return DisplayEnumTemplate ?? DisplayStringTemplate;
            
            // Then check JSON value type
            var template = SelectTemplateByJsonType(valueNode.Value.ValueKind, DisplayBooleanTemplate, DisplayNumberTemplate, DisplayStringTemplate);
            
            // If JSON type matched, use that template
            if (template != null)
            {
                System.Diagnostics.Debug.WriteLine($"Template Selector - Using JSON type template for {valueNode.Path}");
                return template;
            }
            
            // Otherwise, fall back to schema type
            template = SelectTemplateBySchemaType(vm.SchemaContextNode?.ClrType, DisplayBooleanTemplate, DisplayNumberTemplate, null, DisplayStringTemplate);
            
            System.Diagnostics.Debug.WriteLine($"Template Selector - Using schema type template for {valueNode.Path}");
            return template;
        }

        private DataTemplate? SelectValueNodeEditTemplate(DataGridRowItemViewModel vm, ValueNode valueNode)
        {
            // Debug output
            System.Diagnostics.Debug.WriteLine($"Template Selector (EDIT) - DOM Node: {valueNode.Path}, JSON Type: {valueNode.Value.ValueKind}, Schema Type: {vm.SchemaContextNode?.ClrType?.Name}, Schema Allowed Values: {vm.SchemaContextNode?.AllowedValues?.Count ?? 0}");
            
            // First check if schema has allowed values (enum)
            if (vm.IsEnumBased )
                return EditEnumTemplate ?? EditStringTemplate;

            // Then check JSON value type
            var template = SelectTemplateByJsonType(valueNode.Value.ValueKind, EditBooleanTemplate, EditNumberTemplate, EditStringTemplate);
            
            // If JSON type matched, use that template
            if (template != null)
            {
                System.Diagnostics.Debug.WriteLine($"Template Selector (EDIT) - Using JSON type template for {valueNode.Path}");
                return template;
            }
            
            // Otherwise, fall back to schema type
            template = SelectTemplateBySchemaType(vm.SchemaContextNode?.ClrType, EditBooleanTemplate, EditNumberTemplate, null, EditStringTemplate);
            
            System.Diagnostics.Debug.WriteLine($"Template Selector (EDIT) - Using schema type template for {valueNode.Path}");
            return template;
        }

        private DataTemplate? SelectTemplateByJsonType(JsonValueKind jsonType, DataTemplate? booleanTemplate, DataTemplate? numberTemplate, DataTemplate? stringTemplate)
        {
            return jsonType switch
            {
                JsonValueKind.True or JsonValueKind.False => booleanTemplate ?? stringTemplate,
                JsonValueKind.Number => numberTemplate ?? stringTemplate,
                _ => null
            };
        }

        private DataTemplate? SelectTemplateBySchemaType(Type? schemaType, DataTemplate? booleanTemplate, DataTemplate? numberTemplate, DataTemplate? schemaOnlyTemplate, DataTemplate? stringTemplate)
        {
            if (schemaType == typeof(bool))
                return booleanTemplate ?? stringTemplate;
            
            if (IsNumericType(schemaType))
                return numberTemplate ?? stringTemplate;
            
            if (schemaOnlyTemplate != null)
                return schemaOnlyTemplate ?? stringTemplate;
            
            return stringTemplate;
        }

        private bool IsNumericType(Type? type)
        {
            if (type == null) return false;
            
            return type == typeof(int) || type == typeof(long) ||
                   type == typeof(double) || type == typeof(float) ||
                   type == typeof(decimal);
        }
    }
} 