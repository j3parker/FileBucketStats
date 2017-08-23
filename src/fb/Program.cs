using CommandLine;
using fb.grep;

namespace fb {
	class Program {
		private class BaseOptions {
			[Option(HelpText = "Folder containing filebucket git repos", Default = "//pdfs01/ea/devops/filebucket")]
			public string BucketPath { get; set; }
		}

		[Verb("grep", HelpText = "Search file buckets with regular expressions")]
		private class GrepOptions : BaseOptions {
			[Option('f', "file-pattern", HelpText = "Regular expression to filter searched file names", Default = "")]
			public string FilenamePattern { get; set; }

			[Option(Required = true, HelpText = "Regular expression to run on each line of every file" )]
			public string Pattern { get; set; }
		}

		[Verb("gen-path-tree")]
		private class GenPathTree : BaseOptions {
		}

		static void Main( string[] args ) {
			CommandLine.Parser.Default.ParseArguments<GrepOptions, GenPathTree>( args )
				.MapResult(
					(GrepOptions opts) => Grep.Run(
						bucketsPath: opts.BucketPath,
						filenamePattern: opts.FilenamePattern,
						pattern: opts.Pattern
					),
					(GenPathTree opts ) => 0,
					notParsedFunc: errs => 1
				);
		}
	}
}