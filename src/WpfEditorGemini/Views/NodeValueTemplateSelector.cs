using JsonConfigEditor.ViewModels;
using JsonConfigEditor.Core.Dom;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using JsonConfigEditor.Wpf.Services;
using JsonConfigEditor.Contracts.Rendering;
using JsonConfigEditor.Contracts.Editors;
using JsonConfigEditor.Core.Schema;
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
                if (vm.IsAddItemPlaceholder)
                    return DisplayAddItemTemplate ?? DisplayStringTemplate;

                if (vm.IsSchemaOnlyNode)
                {
                    if (vm.SchemaContextNode?.AllowedValues?.Count > 0)
                        return DisplayEnumTemplate ?? DisplayStringTemplate;
                    if (vm.SchemaContextNode?.ClrType == typeof(bool))
                        return DisplayBooleanTemplate ?? DisplayStringTemplate;
                    if (vm.SchemaContextNode?.ClrType == typeof(int) || vm.SchemaContextNode?.ClrType == typeof(long) ||
                        vm.SchemaContextNode?.ClrType == typeof(double) || vm.SchemaContextNode?.ClrType == typeof(float) ||
                        vm.SchemaContextNode?.ClrType == typeof(decimal))
                        return DisplayNumberTemplate ?? DisplayStringTemplate;
                    // fallback
                    return DisplaySchemaOnlyTemplate ?? DisplayStringTemplate;
                }

                if (vm.DomNode is ValueNode valueNode)
                {
                    // Debug output
                    System.Diagnostics.Debug.WriteLine($"Template Selector - DOM Node: {valueNode.Path}, JSON Type: {valueNode.Value.ValueKind}, Schema Type: {vm.SchemaContextNode?.ClrType?.Name}, Schema Allowed Values: {vm.SchemaContextNode?.AllowedValues?.Count ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"Template Selector - SchemaContextNode is null: {vm.SchemaContextNode == null}, SchemaContextNode Name: {vm.SchemaContextNode?.Name}");
                    
                    // First check if schema has allowed values (enum)
                    if (vm.SchemaContextNode?.AllowedValues?.Count > 0)
                        return DisplayEnumTemplate ?? DisplayStringTemplate;
                    
                    // Then check JSON value type
                    var template = valueNode.Value.ValueKind switch
                    {
                        JsonValueKind.True or JsonValueKind.False => DisplayBooleanTemplate ?? DisplayStringTemplate,
                        JsonValueKind.Number => DisplayNumberTemplate ?? DisplayStringTemplate,
                        _ => null
                    };
                    
                    // If JSON type matched, use that template
                    if (template != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Template Selector - Using JSON type template for {valueNode.Path}");
                        return template;
                    }
                    
                    // Otherwise, fall back to schema type
                    if (vm.SchemaContextNode?.ClrType == typeof(bool))
                    {
                        System.Diagnostics.Debug.WriteLine($"Template Selector - Using schema boolean template for {valueNode.Path}");
                        return DisplayBooleanTemplate ?? DisplayStringTemplate;
                    }
                    if (vm.SchemaContextNode?.ClrType == typeof(int) || vm.SchemaContextNode?.ClrType == typeof(long) ||
                        vm.SchemaContextNode?.ClrType == typeof(double) || vm.SchemaContextNode?.ClrType == typeof(float) ||
                        vm.SchemaContextNode?.ClrType == typeof(decimal))
                    {
                        System.Diagnostics.Debug.WriteLine($"Template Selector - Using schema number template for {valueNode.Path}");
                        return DisplayNumberTemplate ?? DisplayStringTemplate;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Template Selector - Using default string template for {valueNode.Path}");
                    return DisplayStringTemplate;
                }

                if (vm.DomNode is RefNode)
                    return DisplayRefTemplate ?? DisplayStringTemplate;

                if (vm.DomNode is ArrayNode)
                    return DisplayArrayTemplate ?? DisplayStringTemplate;

                if (vm.DomNode is ObjectNode)
                    return DisplayObjectTemplate ?? DisplayStringTemplate;

                return DisplayStringTemplate;
            }
            else
            {
                if (vm.IsAddItemPlaceholder)
                    return EditAddItemTemplate ?? EditStringTemplate;

                if (vm.IsSchemaOnlyNode)
                {
                    if (vm.SchemaContextNode?.AllowedValues?.Count > 0)
                        return EditEnumTemplate ?? EditStringTemplate;
                    if (vm.SchemaContextNode?.ClrType == typeof(bool))
                        return EditBooleanTemplate ?? EditStringTemplate;
                    if (vm.SchemaContextNode?.ClrType == typeof(int) || vm.SchemaContextNode?.ClrType == typeof(long) ||
                        vm.SchemaContextNode?.ClrType == typeof(double) || vm.SchemaContextNode?.ClrType == typeof(float) ||
                        vm.SchemaContextNode?.ClrType == typeof(decimal))
                        return EditNumberTemplate ?? EditStringTemplate;
                    // fallback
                    return EditStringTemplate;
                }

                if (vm.DomNode is ValueNode valueNode)
                {
                    // Debug output
                    System.Diagnostics.Debug.WriteLine($"Template Selector (EDIT) - DOM Node: {valueNode.Path}, JSON Type: {valueNode.Value.ValueKind}, Schema Type: {vm.SchemaContextNode?.ClrType?.Name}, Schema Allowed Values: {vm.SchemaContextNode?.AllowedValues?.Count ?? 0}");
                    
                    // First check if schema has allowed values (enum)
                    if (vm.SchemaContextNode?.AllowedValues?.Count > 0)
                        return EditEnumTemplate ?? EditStringTemplate;

                    // Then check JSON value type
                    var template = valueNode.Value.ValueKind switch
                    {
                        JsonValueKind.True or JsonValueKind.False => EditBooleanTemplate ?? EditStringTemplate,
                        JsonValueKind.Number => EditNumberTemplate ?? EditStringTemplate,
                        _ => null
                    };
                    
                    // If JSON type matched, use that template
                    if (template != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Template Selector (EDIT) - Using JSON type template for {valueNode.Path}");
                        return template;
                    }
                    
                    // Otherwise, fall back to schema type
                    if (vm.SchemaContextNode?.ClrType == typeof(bool))
                    {
                        System.Diagnostics.Debug.WriteLine($"Template Selector (EDIT) - Using schema boolean template for {valueNode.Path}");
                        return EditBooleanTemplate ?? EditStringTemplate;
                    }
                    if (vm.SchemaContextNode?.ClrType == typeof(int) || vm.SchemaContextNode?.ClrType == typeof(long) ||
                        vm.SchemaContextNode?.ClrType == typeof(double) || vm.SchemaContextNode?.ClrType == typeof(float) ||
                        vm.SchemaContextNode?.ClrType == typeof(decimal))
                    {
                        System.Diagnostics.Debug.WriteLine($"Template Selector (EDIT) - Using schema number template for {valueNode.Path}");
                        return EditNumberTemplate ?? EditStringTemplate;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Template Selector (EDIT) - Using default string template for {valueNode.Path}");
                    return EditStringTemplate;
                }

                if (vm.DomNode is RefNode)
                    return EditRefTemplate ?? EditStringTemplate;

                return EditStringTemplate;
            }
        }
    }
} 