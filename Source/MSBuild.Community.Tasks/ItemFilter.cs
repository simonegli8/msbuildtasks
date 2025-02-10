using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace MSBuild.Community.Tasks {

	/// <summary>
	/// Filters the Input items. If Include is set, only items from the Include list are taken, if Exclude is set, all exclude items are excluded.
	/// If Items is set, items the same in Input and Items, the item from Items is taken. 
	/// </summary>
	public class ItemFilter: Task {

		public ITaskItem[] Input { get; set; }
		public ITaskItem[] Include { get; set; }
		public ITaskItem[] Exclude { get; set; }
		public ITaskItem[] Items { get; set; }
		[Output]
		public ITaskItem[] Output { get; set; }
		public string Meta { get; set; }

		public override bool Execute() {

			if (Input == null) return true;

			Dictionary<string, ITaskItem> include = Include?.ToDictionary(inc => inc.ItemSpec);
			Dictionary<string, ITaskItem> exclude = Exclude?.ToDictionary(ex => ex.ItemSpec);
			Dictionary<string, ITaskItem> items = Items?.ToDictionary(item => item.ItemSpec);

			// filter
			var output = Input.Where(item => (include == null || include.ContainsKey(item.ItemSpec))
				&& (exclude == null || !exclude.ContainsKey(item.ItemSpec)));
			// use items
			if (items != null) {
				ITaskItem x;
				output = output.Select(o => items.TryGetValue(o.ItemSpec, out x) ? x : o);
			}
			// filter Meta items
			output = output.Where(item => (string.IsNullOrEmpty(Meta) || string.IsNullOrEmpty(item.GetMetadata(Meta)) || item.GetMetadata(Meta) == "false"));

			Output = output.ToArray();

			return true;
		}

	}

}
