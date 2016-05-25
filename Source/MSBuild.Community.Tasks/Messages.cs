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
	/// Logs several messages
	/// </summary>
	public class Messages: Task {

		MessageImportance importance = MessageImportance.Normal;
		public string Importance {
			get { return importance.ToString(); }
			set { importance = (MessageImportance)Enum.Parse(typeof(MessageImportance), value); }
		}

		public ITaskItem[] Texts { get; set; }

		public override bool Execute() {
			if (Texts != null) {
				foreach (var t in Texts) {
					Log.LogMessage(importance, t.ItemSpec);
				}
			}
			return true;
		}

	}
}
