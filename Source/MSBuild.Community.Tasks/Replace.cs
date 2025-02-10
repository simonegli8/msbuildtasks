#region Copyright © 2005 Paul Welter. All rights reserved.
/*
Copyright © 2005 Paul Welter. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions
are met:

1. Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in the
   documentation and/or other materials provided with the distribution.
3. The name of the author may not be used to endorse or promote products
   derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE AUTHOR "AS IS" AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE. 
*/
#endregion

#region 2011-12-19: Default Encoding by Manuel Sprock (Phoenix Contact)
/* 
Support for default encoding (utf-8-without-bom) added. 
There is no WebName for utf-8-without-bom. It's the default 
encoding when not specifying an encoding and using the overload
File.WriteAllText(fileName, buffer) 
Cf. http://msdn.microsoft.com/en-us/library/ms143375.aspx
*/
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace MSBuild.Community.Tasks {
	/// <summary>
	/// Replace text in file(s) using a Regular Expression.
	/// </summary>
	/// <example>Search for a version number and update the revision.
	/// <code><![CDATA[
	/// <Replace Files="version.txt"
	///     Regex="(\d+)\.(\d+)\.(\d+)\.(\d+)"
	///     Replacement="$1.$2.$3.123" />
	/// ]]></code>
	/// </example>
	public class Replace: Task {
		/// <summary>
		/// Initializes a new instance of the <see cref="T:FileUpdate"/> class.
		/// </summary>
		public Replace() {
			ItemsNotUpdated = null;
			AllItemsUpdated = true;
			Count = -1;
			ScopeCount = -1;
			RegexOptions = RegexOptions.Singleline;
		}

		public struct Replacing {
			public string Scope;
			public string Expression;
			public RegexOptions Options;
			public string Replacement;
			public int Count;
			public int ScopeCount;
		}

		#region Properties

		/// <summary>
		/// Gets or sets the files to update.
		/// </summary>
		/// <value>The files.</value>
		public ITaskItem[] Files { get; set; }
		public ITaskItem[] Exclude { get; set; }

		/// <summary>
		/// Gets or sets the Scope regex. The scope is a regex, upon whose matches the replacement is done only.
		/// </summary>
		/// <value>The regex.</value>
		public string Scope { get; set; }

		/// <summary>
		/// Gets or sets the regex.
		/// </summary>
		/// <value>The regex.</value>
		public string Regex { get; set; }

		public RegexOptions RegexOptions = RegexOptions.None;
		/// <summary>
		/// Regex options as comma separated string corresponding to the RegexOptions enum:
		///     Compiled
		///     CultureInvariant
		///     ECMAScript 
		///     ExplicitCapture
		///     IgnoreCase
		///     IgnorePatternWhitespace
		///     Multiline
		///     None
		///     RightToLeft
		///     Singleline
		/// </summary>
		/// <enum cref="System.Text.RegularExpressions.RegexOptions" />
		public string Options {
			get { return RegexOptions.ToString(); }
			set { RegexOptions = ParseOptions(value); }
		}

		/// <summary>
		/// Gets or sets the maximum number of times the replacement can occur.
		/// </summary>
		/// <value>The replacement count.</value>
		public int Count { get; set; }

		/// <summary>
		/// Gets or sets the maximum number of times the replacement can occur.
		/// </summary>
		/// <value>The replacement count.</value>
		public int ScopeCount { get; set; }

		/// <summary>
		/// Gets or sets the replacement text.
		/// </summary>
		/// <value>The replacement text.</value>
		public string Replacement { get; set; }

		/// Maintain the behaviour of the original implementation for compatibility
		/// (i.e. initialize _useDefaultEncoding with false) and use utf-8-without-bom,  
		/// which is Microsoft's default encoding, only when Encoding property is set 
		/// to "utf-8-without-bom". 
		private bool _useDefaultEncoding;

		private Encoding _encoding = System.Text.Encoding.UTF8;
		/// <summary>
		/// The character encoding used to read and write the file.
		/// </summary>
		/// <remarks>Any value returned by <see cref="System.Text.Encoding.WebName"/> is valid input.
		/// <para>The default is <c>utf-8</c></para>
		/// <para>Additionally, <c>utf-8-without-bom</c>can be used.</para></remarks>
		public string Encoding {
			get {
				if (_useDefaultEncoding) return "utf-8-without-bom";
				else return _encoding.WebName;
			}
			set {
				if (value.ToLower().CompareTo("utf-8-without-bom") == 0) _useDefaultEncoding = true;
				else _encoding = System.Text.Encoding.GetEncoding(value);
			}
		}

		//--added for testing

		private bool _warnOnNoUpdate = false;
		/// <summary>
		/// When TRUE, a warning will be generated to show which file was not updated.
		/// </summary>
		/// <remarks>N/A</remarks>
		public bool WarnOnNoUpdate {
			get { return _warnOnNoUpdate; }
			set { _warnOnNoUpdate = value; }
		}

		public string Meta { get; set; }

		protected virtual IEnumerable<Replacing> GetReplacings(ITaskItem item, string text) {
			if (string.IsNullOrEmpty(Regex)) yield break;
			yield return new Replacing { Scope = Scope, Expression = Regex, Options = RegexOptions, Replacement = Replacement, ScopeCount = ScopeCount, Count = Count };
		}

		protected RegexOptions ParseOptions(string optionsText) {
			var regexOptions = RegexOptions.None;
			if (!string.IsNullOrEmpty(optionsText)) {
				var options = optionsText.Split(',', ';', '|');
				foreach (var option in options) {
					if (Enum.IsDefined(typeof(RegexOptions), option)) {
						regexOptions = regexOptions | (RegexOptions)Enum.Parse(typeof(RegexOptions), option);
					}
				}
			}
			return regexOptions;
		}

		public virtual bool ShowProgress { get; set; }

		#endregion

		#region Output Properties

		/// <summary>
		/// Returns list of items that were not updated
		/// </summary>
		[Output]
		public ITaskItem[] ItemsNotUpdated { get; set; }

		/// <summary>
		/// Returns true if all items were updated, else false
		/// </summary>
		[Output]
		public bool AllItemsUpdated { get; set; }

		#endregion

		/// <summary>
		/// When overridden in a derived class, executes the task.
		/// </summary>
		/// <returns>
		/// true if the task successfully executed; otherwise, false.
		/// </returns>
		public override bool Execute() {

			if (Files == null) return true;

			try {
				var itemsNotUpdated = new List<ITaskItem>();

				Files = Files.Where(f => Exclude?.Any(g => g.ItemSpec == f.ItemSpec) ?? true).ToArray();

				//System.Threading.Tasks.Parallel.ForEach(Files, item => {
				foreach (var item in Files) {
					if (!string.IsNullOrEmpty(Meta) && !string.IsNullOrEmpty(item.GetMetadata(Meta)) && item.GetMetadata(Meta) != "false") {
						lock (itemsNotUpdated) itemsNotUpdated.Add(item);
						//return;
						continue;
					}

					string file = item.ItemSpec;

					string text;
					if (_useDefaultEncoding) {
						text = File.ReadAllText(file);
					} else {
						text = File.ReadAllText(file, _encoding);
					}

					var replacings = GetReplacings(item, text).ToArray();
					if (!replacings.Any()) continue; // return;

					var any = false;
					foreach (var repl in replacings) {
						if (!string.IsNullOrEmpty(repl.Scope)) {
							var scope = new Regex(repl.Scope, repl.Options);
							var regex = new Regex(repl.Expression, repl.Options);
							text = scope.Replace(text, match => {
								any = true;
								if (match.Groups["scope"] != null && match.Groups["scope"].Success == true) {
									var s = match.Groups["scope"];
									return match.Result(match.Value.Substring(0, s.Index-match.Index) + regex.Replace(s.Value, repl.Replacement ?? "", repl.Count) + match.Value.Substring(s.Index+s.Length-match.Index));
								} else {
									return match.Result(regex.Replace(match.Value, repl.Replacement ?? "", repl.Count));
								}
							}, repl.ScopeCount);
						} else {
							var regex = new Regex(repl.Expression, repl.Options);
							text = regex.Replace(text,
								match => {
									any = true;
									return match.Result(repl.Replacement ?? "");
								},
								repl.Count);
						}
					}

					if (!any) {
						lock (itemsNotUpdated) itemsNotUpdated.Add(item);

						if (_warnOnNoUpdate) {
							Log.LogWarning(String.Format("No updates were performed on file : {0}.", file));
						}
					} else {
						if (ShowProgress) Log.LogMessage("Replace on {0}", file);

						if (_useDefaultEncoding) {
							File.WriteAllText(file, text);
						} else {
							File.WriteAllText(file, text, _encoding);
						}
					}
					//});
				}

				if (itemsNotUpdated.Count > 0) {
					ItemsNotUpdated = itemsNotUpdated.ToArray();
					AllItemsUpdated = false;
				}
				Log.LogMessage("{0} on {1} files.", this.GetType().Name, Files.Length - itemsNotUpdated.Count);

			} catch (Exception ex) {
				Log.LogErrorFromException(ex);
				AllItemsUpdated = false;
				return false;
			}
			return true;
		}
	}
}
