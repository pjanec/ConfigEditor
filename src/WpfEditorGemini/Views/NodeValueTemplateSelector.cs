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
            else
            {
                if (vm.IsAddItemPlaceholder)
                    return EditAddItemTemplate ?? EditStringTemplate;

                if (vm.IsSchemaOnlyNode)
                    return EditSchemaOnlyTemplate ?? EditStringTemplate;

                if (vm.DomNode is ValueNode valueNode)
                {
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