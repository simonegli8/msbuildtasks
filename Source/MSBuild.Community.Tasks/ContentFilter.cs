#region Copyright � 2006 Andy Johns. All rights reserved.
/*
Copyright � 2006 Andy Johns. All rights reserved.

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



using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;

namespace MSBuild.Community.Tasks {
	/// <summary>
	/// Task to filter an Input list or a file list by the file's content with a Regex expression.
	/// Output list contains items from Input list or the file items whose content matched given expression
	/// </summary>
	/// <example>Matches from TestGroup those names ending in a, b or c
	/// <code><![CDATA[
	/// <ItemGroup>
	///    <TestGroup Include="foo.my.foo.foo.test.o" />
	///    <TestGroup Include="foo.my.faa.foo.test.a" />
	///    <TestGroup Include="foo.my.fbb.foo.test.b" />
	///    <TestGroup Include="foo.my.fcc.foo.test.c" />
	///    <TestGroup Include="foo.my.fdd.foo.test.d" />
	///    <TestGroup Include="foo.my.fee.foo.test.e" />
	///    <TestGroup Include="foo.my.fff.foo.test.f" />
	/// </ItemGroup>
	/// <Target Name="Test">
	///    <!-- Outputs only items that end with a, b or c -->
	///    <RegexMatch Input="@(TestGroup)" Expression="[a-c]$">
	///       <Output ItemName ="MatchReturn" TaskParameter="Output" />
	///    </RegexMatch>
	///    <Message Text="&#xA;Output Match:&#xA;@(MatchReturn, '&#xA;')" />
	/// </Target>
	/// ]]></code>
	/// </example>
	public class ContentFilter : RegexBase {

		public enum Modes { Content, Name };

		public Modes Mode { get; set; } = Modes.Content;

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
		public string Encoding
		{
			get
			{
				if (_useDefaultEncoding) return "utf-8-without-bom";
				else return _encoding.WebName;
			}
			set
			{
				if (value.ToLower().CompareTo("utf-8-without-bom") == 0) _useDefaultEncoding = true;
				else _encoding = System.Text.Encoding.GetEncoding(value);
			}
		}

		public string Meta { get; set; }

		/// <summary>
		/// Performs the Match task
		/// </summary>
		/// <returns><see langword="true"/> if the task ran successfully; 
		/// otherwise <see langword="false"/>.</returns>
		public override bool Execute() {

			if (Input == null) return true;
			
			var regex = new System.Text.RegularExpressions.Regex(Expression.ItemSpec, ExpressionOptions);

			List<ITaskItem> returnItems = new List<ITaskItem>();

			foreach (ITaskItem item in Input) {

				if (File.Exists(item.ItemSpec) && (string.IsNullOrEmpty(Meta) || string.IsNullOrEmpty(item.GetMetadata(Meta)) || item.GetMetadata(Meta) == "false") &&
				  ((Mode == Modes.Content && regex.IsMatch(File.ReadAllText(item.ItemSpec, _encoding))) ||
				  (Mode == Modes.Name && regex.IsMatch(item.ItemSpec)))) {
					returnItems.Add(new TaskItem(item));
				}
			}

			Output = returnItems.ToArray();

			return !Log.HasLoggedErrors;
		}
	}

	public class NameFilter : ContentFilter {
		public NameFilter() {
			Mode = Modes.Name;
		}
	}
}