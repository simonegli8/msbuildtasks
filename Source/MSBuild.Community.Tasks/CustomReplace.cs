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
	/// Replace text in file(s) using a Regular Expression contained in an items Metadata.
	/// The Metadata fields that can be used on items are:
	/// ReplaceExpression: The Regular Expression used to replace.
	/// ReplaceText: The text used to replace (including regex references).
	/// ReplaceOptions: Regex options for the operation.
	/// </summary>
	/// <example>Search for a version number and update the revision.
	/// <code><![CDATA[
	/// <CustomUpdate Files="@(Compile)" />
	/// ]]></code>
	/// </example>
	public class CustomReplace: Replace {

		public CustomReplace(): base() { }

		protected override IEnumerable<Replacing> GetReplacings(ITaskItem item, string text) {
			foreach (var repl in base.GetReplacings(item, text)) yield return repl;
			int i = 0;
			string n = i > 0 ? i.ToString() : "";
			string regex;
			while (!string.IsNullOrEmpty(regex = item.GetMetadata("Replacing"+n))) {
				int count;
				if (!int.TryParse(item.GetMetadata("ReplaceCount"+n) ?? "", out count)) count = -1;
				var options = ParseOptions(item.GetMetadata("ReplaceOptions"));
				yield return new Replacing { Expression = regex, Options = options, Count = count, Replacement = item.GetMetadata("Replacement"+n) };
				i++;
				n = i > 0 ? i.ToString() : "";
			}
		}

	}
}
