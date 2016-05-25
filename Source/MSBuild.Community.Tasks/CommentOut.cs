using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RE = System.Text.RegularExpressions;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace MSBuild.Community.Tasks {

	/// <summary>
	/// Comments out regions in code files.
	/// </summary>
	public class CommentOut: Replace {

		/// <summary>
		/// Initializes a new instance of the <see cref="T:CommentOut"/> class.
		/// </summary>
		public CommentOut() {
			Replacement = "/* $0 */";
			Custom = false;
		}

		Replacing Filter(Replacing repl) {
			var i = repl.Replacement.IndexOf('$');
			if (i > 0) { // add regex to filter out already commented out regions
				repl.Expression = "(?<!" + RE.Regex.Escape(repl.Replacement.Substring(0, i)).Replace(@"\ ", @"\s*") + ")(?:" + repl.Expression + ")";
			}
			return repl;
		}

		public bool Custom { get; set; }

		protected override IEnumerable<Replacing> GetReplacings(ITaskItem item, string text) {
			foreach (var repl in base.GetReplacings(item, text)) yield return Filter(repl);
			if (Custom || string.IsNullOrEmpty(Regex)) {
				int i = 0;
				string n = i > 0 ? i.ToString() : "";
				string regex;
				while (!string.IsNullOrEmpty(regex = item.GetMetadata("CommentOut"+n))) {
					int count;
					if (!int.TryParse(item.GetMetadata("CommentOutCount"+n) ?? "", out count)) count = -1;
					var optmeta = item.GetMetadata("CommentOutOptions");
					var options = string.IsNullOrEmpty(optmeta) ? RegexOptions : ParseOptions(optmeta);
					var repl = item.GetMetadata("CommentOutReplacement"+n);
					if (string.IsNullOrEmpty(repl)) repl = Replacement ?? "";
					yield return Filter(new Replacing { Expression = regex, Options = options, Count = count, Replacement = repl });
					i++;
					n = i > 0 ? i.ToString() : "";
				}
			}
		}

	}
}
