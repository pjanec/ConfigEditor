using JsonConfigEditor.Contracts.Editors;
using JsonConfigEditor.Contracts.Rendering;
using JsonConfigEditor.Contracts.Tooltips;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent; // For thread-safe dictionaries if needed

namespace JsonConfigEditor.Wpf.Services
{
    /// <summary>
    /// Central registry for custom UI components (renderers, editors, tooltip providers)
    /// discovered via attributes from scanned assemblies.
    /// </summary>
    public class CustomUIRegistryService
    {
        // Dictionaries to store registered components. Key is the TargetClrType.
        // Value could be the Type of the component or an instance. Storing Type is often more flexible.
        private readonly Dictionary<Type, Type> _renderers = new Dictionary<Type, Type>();
        private readonly Dictionary<Type, (Type EditorType, bool RequiresModal)> _editors = new Dictionary<Type, (Type, bool)>();
        private readonly Dictionary<Type, Type> _tooltipProviders = new Dictionary<Type, Type>();

        // Consider thread-safety if registration can happen from multiple threads,
        // though typically it's done at startup. ConcurrentDictionary might be overkill if single-threaded init.

        /// <summary>
        /// Registers a custom value renderer type for a specific CLR data type.
        /// </summary>
        public void RegisterRenderer(Type targetClrType, Type rendererType)
        {
            if (!typeof(IValueRenderer).IsAssignableFrom(rendererType))
                throw new ArgumentException($"{rendererType.FullName} must implement IValueRenderer.", nameof(rendererType));
            _renderers[targetClrType] = rendererType;
            // Log registration
        }

        /// <summary>
        /// Registers a custom value editor type for a specific CLR data type.
        /// </summary>
        public void RegisterEditor(Type targetClrType, Type editorType, bool requiresModal)
        {
            if (!typeof(IValueEditor).IsAssignableFrom(editorType))
                throw new ArgumentException($"{editorType.FullName} must implement IValueEditor.", nameof(editorType));
            _editors[targetClrType] = (editorType, requiresModal);
            // Log registration
        }

        /// <summary>
        /// Registers a custom tooltip provider type for a specific CLR data type.
        /// </summary>
        public void RegisterTooltipProvider(Type targetClrType, Type tooltipProviderType)
        {
            if (!typeof(ITooltipProvider).IsAssignableFrom(tooltipProviderType))
                throw new ArgumentException($"{tooltipProviderType.FullName} must implement ITooltipProvider.", nameof(tooltipProviderType));
            _tooltipProviders[targetClrType] = tooltipProviderType;
            // Log registration
        }

        /// <summary>
        /// Retrieves a custom value renderer instance for the given CLR data type.
        /// </summary>
        /// <returns>An instance of IValueRenderer, or null if no custom renderer is registered for the type.</returns>
        public IValueRenderer? GetValueRenderer(Type targetClrType)
        {
            if (_renderers.TryGetValue(targetClrType, out Type? rendererType))
            {
                try { return Activator.CreateInstance(rendererType) as IValueRenderer; }
                catch (Exception ex) { /* Log error instantiating renderer */ return null; }
            }
            return null;
        }

        /// <summary>
        /// Retrieves a custom value editor instance and its modal requirement for the given CLR data type.
        /// </summary>
        /// <returns>An instance of IValueEditor and its modal requirement, or null if no custom editor is registered.</returns>
        public (IValueEditor? Editor, bool RequiresModal)? GetValueEditor(Type targetClrType)
        {
            if (_editors.TryGetValue(targetClrType, out var editorInfo))
            {
                try
                {
                    var editorInstance = Activator.CreateInstance(editorInfo.EditorType) as IValueEditor;
                    return (editorInstance, editorInfo.RequiresModal);
                }
                catch (Exception ex) { /* Log error instantiating editor */ return null; }
            }
            return null;
        }

        /// <summary>
        /// Retrieves a custom tooltip provider instance for the given CLR data type.
        /// </summary>
        /// <returns>An instance of ITooltipProvider, or null if no custom provider is registered for the type.</returns>
        public ITooltipProvider? GetTooltipProvider(Type targetClrType)
        {
            if (_tooltipProviders.TryGetValue(targetClrType, out Type? providerType))
            {
                try { return Activator.CreateInstance(providerType) as ITooltipProvider; }
                catch (Exception ex) { /* Log error instantiating provider */ return null; }
            }
            return null;
        }

        /// <summary>
        /// Clears all registered custom UI components.
        /// </summary>
        public void ClearRegistry()
        {
            _renderers.Clear();
            _editors.Clear();
            _tooltipProviders.Clear();
        }
    }
} 