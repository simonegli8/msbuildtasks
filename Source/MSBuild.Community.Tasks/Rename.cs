using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace MSBuild.Community.Tasks {

	/// <summary>
	/// Renames items with a Regular Expression replace.
	/// </summary>
	public class Rename: Task {

		public ITaskItem[] Items { get; set; }

		[Output]
		public ITaskItem[] Output { get; set; }

		[Required]
		public string Regex { get; set; }
		public string Text { get; set; }

		public override bool Execute() {
			var regex = new Regex(Regex);
			Output = Items?.Select(x => new TaskItem(regex.Replace(x.ItemSpec, Text ?? "")))?.ToArray();
			return true;
		}
	}

}
