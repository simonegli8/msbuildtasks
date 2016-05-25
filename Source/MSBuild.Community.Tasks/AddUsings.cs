using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace MSBuild.Community.Tasks {


	/// <summary>
	/// Adds using statements to C# files.
	/// </summary>
	public class AddUsings: Replace {

		public AddUsings() {
			Regex = @"^(?:(?://[^\r\n]*)|(?:/\*(?:[^*]*\*)*/)|\s*)*";
		}
		[Required]
		public string Namespaces { get; set; }

		protected override IEnumerable<Replacing> GetReplacings(ITaskItem item, string text) {
			var ns = Namespaces + item.GetMetadata("AddUsings") ?? "";
			var nss = ns.Trim(' ', '\t', '\n', '\r', ';').Replace("using", "").Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "").Split(';')
				.Distinct()
				.OrderBy(s => s)
				.ToArray();
			Replacement = "$0#pragma warning disable 105\r\nusing " + string.Join(";\r\nusing ", nss) + ";\r\n\r\n";
			Count = 1;
			return base.GetReplacings(item, text);
		}

	}

}
