using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace MSBuild.Community.Tasks {

	/// <summary>
	/// Changes the namespace of C# files.
	/// </summary>
	public class ChangeNamespace: Replace {

		/// <summary>
		/// Initializes a new instance of the <see cref="T:CommentOut"/> class.
		/// </summary>
		public ChangeNamespace(): base() {
			Regex = @"(?<=(?<!(//[^\n\r]*)|(/\*[^*]*(\*[^/]?[^*]*)))namespace[ \t\r\n]+)(?<ns>[a-zA-Z0-9_.]+)";
			Replacement = "${ns}";
		}

		public string Namespace { get; set; }

		protected override IEnumerable<Replacing> GetReplacings(ITaskItem item, string text) {
			var meta = item.GetMetadata("Namespace");
			Replacement = string.IsNullOrEmpty(meta) ? Namespace : meta;
			return base.GetReplacings(item, text);
		}

	}
}
