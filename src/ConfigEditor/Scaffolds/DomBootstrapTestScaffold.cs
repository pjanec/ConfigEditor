using ConfigEditor.Dom;
using ConfigEditor.EditCtx;
using ConfigEditor.IO;
using ConfigEditor.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ConfigEditor.Scaffolds
{
	public static class DomBootstrapTestScaffold
	{
		public static EditorWorkspace BootstrapEditor()
		{
			var root = new ObjectNode( "root" );
			var workspace = new EditorWorkspace( root );

			var folders = new List<string> { "base", "site", "local" };
			var context = Json5CascadeEditorContextFactory.LoadFromFolders( "config/env1", folders );
			workspace.RegisterContext( "config/env1", context );

			var meta = new ObjectNode( "meta" );
			meta.AddChild( new ValueNode( "env", JsonDocument.Parse( "\"editor\"" ).RootElement ) );
			workspace.RegisterProvider( "meta", new StaticDomBranchProvider( meta ) );

			return workspace;
		}

		public static RuntimeDomTree BootstrapRuntime()
		{
			var root = new ObjectNode( "root" );
			var runtime = new RuntimeDomTree( root );

			var bson = File.ReadAllBytes( "compiled/env1.bson" );
			var staticDom = BsonImporter.Import( bson );
			runtime.RegisterProvider( "config/env1", new StaticDomBranchProvider( staticDom, "env1" ) );

			runtime.RegisterProvider( "runtime/metrics", new DynamicMetricsProvider() );

			return runtime;
		}
	}
}
