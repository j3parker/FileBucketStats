using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace fb.grep {
	public static class Grep {
		private sealed class GitFile {
			public GitFile(
				string path,
				Blob blob
			) {
				Path = path;
				Blob = blob;
			}

			public string Path { get; }
			public Blob Blob { get; }
		}

		private static readonly Object m_consoleLock = new object();

		public static int Run(
			string bucketsPath,
			string filenamePattern,
			string pattern
		) {
			Run(
				bucketsPath.Replace( '\\', '/'),
				filenamePattern: new Regex( filenamePattern ),
				pattern: new Regex( pattern )
			);

			return 0;
		}

		private static void Run(
			string bucketsPath,
			Regex filenamePattern,
			Regex pattern
		) {
			var repos = GetBucketRepos( bucketsPath );

			foreach( var repo in repos ) {
				string bucketName = repo.Info.Path
					.Substring( 0, repo.Info.Path.Length - 5 ) // remove .git/
					.Replace( '\\', '/' )
					.Replace( bucketsPath, "" )
					.Replace( "/", "" );

				Parallel.ForEach(
					GetPaths( repo ),
					gitFile => {
						// Skip binary files
						if( gitFile.Blob.IsBinary ) {
							return;
						}

						// filenamePattern filters out files by path
						if( !filenamePattern.IsMatch( gitFile.Path ) ) {
							return;
						}

						foreach( var match in GrepBlob( gitFile.Blob, pattern ) ) {
							lock ( m_consoleLock ) {
								Console.WriteLine( $"{bucketName}/{gitFile.Path}:{match.Item1} {match.Item2.Groups[1]}" );
							}
						}
					} );
			}
		}

		private static IEnumerable<Tuple<int, Match>> GrepBlob( Blob blob, Regex pattern ) {
			using( var stream = blob.GetContentStream() ) {
				using( var reader = new StreamReader( stream ) ) {
					string line;
					int lineNum = 0;
					while( ( line = reader.ReadLine() ) != null ) {
						lineNum++;
						var match = pattern.Match( line );
						if( match.Success ) {
							yield return new Tuple<int, Match>( lineNum, match );
						}
					}
				}
			}
		}

		private static IEnumerable<Repository> GetBucketRepos( string bucketsPath ) {
			return Directory
				.EnumerateDirectories( bucketsPath, "*", SearchOption.TopDirectoryOnly )
				.Where( Repository.IsValid )
				.Select( path => new Repository( path ) );
		}

		private static IEnumerable<GitFile> GetPaths( Repository repo ) {
			Stack<Tree> trees = new Stack<Tree>();

			trees.Push( repo.Head.Tip.Tree );

			while( trees.Count != 0 ) {
				var tree = trees.Pop();
				foreach( var item in tree ) {
					switch( item.TargetType ) {
						case TreeEntryTargetType.Blob:
							yield return new GitFile(
								item.Path,
								item.Target as Blob
							);
							break;

						case TreeEntryTargetType.Tree:
							trees.Push( item.Target as Tree );
							break;

						default:
							// submodules not supported
							throw new NotImplementedException();
					}
				}
			}
		}
	}
}
