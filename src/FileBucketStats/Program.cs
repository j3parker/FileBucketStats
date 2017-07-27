using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using LibGit2Sharp;

namespace FileBuckets {
	class Program {
		// Outputs a JSON representation of the file tree containing the files
		// in bucketPaths, each directory has a key / that contains the subtree
		// count (total number of files contained within that directory,
		// recursively.) Only performs a single pass over the file paths and
		// uses O(max bucket depth) aux. space (the call stack)
		public static int Treeify(
			IEnumerator<string> bucketPaths,
			string prefix = "/"
		) {
			// pre: bucketPaths is pointing at the next thing to process
			// pre: bucketPaths's underlying collection is sorted
			// pre: every element in bucketPaths is an absolute path to a file
			// pre: prefix is an absolute path to a directory

			if ( !prefix.EndsWith( "/" ) ) {
				throw new Exception( "something is broken: prefix should end with /" );
			}

			WriteOpenObject();

			var prev = prefix; // last file processed
			int count = 0; // total count for directory
			int curCount = 0; // count of current file
			bool first = true;
			while( bucketPaths.Current != null ) {
				var bucketPath = bucketPaths.Current;

				if( bucketPath == prev ) {
					curCount++;
					bucketPaths.MoveNext();
					continue;
				} else if ( curCount != 0 ) {
					WriteFile( GetItemName( prev ), curCount, ref first );
					count += curCount;
				}

				curCount = 0;
				prev = bucketPath;

				var dir = GetDir( bucketPath );
				var itemName = GetItemName( bucketPath );

				var cmp = string.Compare( prefix, dir );

				if( cmp < 0 ) { // directory change
					// if it's not a subdirectory, return to parent
					if ( !dir.StartsWith( prefix ) ) {
						if( curCount != 0 ) {
							throw new Exception( "" );
						}
						break;
					}

					var relativeToPrefix = dir.Substring( prefix.Length );
					var subDirName = relativeToPrefix.Substring( 0, relativeToPrefix.IndexOf( '/' ) );
					var subDirPath = dir.Substring( 0, prefix.Length + subDirName.Length + 1 );

					WriteSubDirStart( subDirName, ref first );
					// otherwise, recurse
					count += Treeify( bucketPaths, subDirPath );

				} else if( cmp == 0 ) { // another file
					if( curCount != 0 ) {
						throw new Exception( "" );
					}
					curCount = 1;
					bucketPaths.MoveNext();
				} else {
					// TODO: explain
				}
			}

			WriteCountAndClose( count );

			// post: result is total number of calls to MoveNext() (equiv. to number of files processed)
			// post: result is the sum of all counts passed to WriteFile (recursively)
			return count;
		}

		private static void WriteSubDirStart( string itemName, ref bool first ) {
			string sep = first ? "" : ",";
			Console.Write( $"{sep}\"{itemName}\":" );
			first = false;
		}

		private static void WriteOpenObject() {
			Console.Write( "{" );
		}

		private static void WriteFile( string itemName, int count, ref bool first ) {
			string sep = first ? "" : ",";
			Console.Write( $"{sep}\"{itemName}\":{count}" );
			first = false;
		}

		private static void WriteCountAndClose( int count ) {
			Console.Write( $",\"/\":{count}}}" );
		}

		private static string GetDir( string path ) {
			// pre: path is absolute, path is a file
			var i = GetLastSlashIndex( path );

			// post: last char is /, the remainder is GetItemName( path )
			return path.Substring( 0, i + 1 );
		}

		private static string GetItemName( string path ) {
			// pre: path is absolute, path is a file
			var i = GetLastSlashIndex( path );

			// i+1 in bounds because post of GetLastSlashIndex says not last element
			// post: this doesn't contain a /
			return path.Substring( i + 1 );
		}

		private static int GetLastSlashIndex( string path ) {
			// pre: path is absolute, path is a file

			var i = path.LastIndexOf( '/' );

			if ( i == -1 ) {
				throw new Exception( "path in GetLastSlashIndex didn't contain a / (not an absolute path)" );
			} else if ( i == path.Length - 1 ) {
				throw new Exception( "path in GetLastSlashIndex wasn't a file: ended in /" );
			}

			// post: result is in bounds, not the last element and points to a /
			// post: no / after this index
			return i;
		}

		static void Run( string bucketsPath ) {
			var bucketPaths = Directory
				.EnumerateDirectories( bucketsPath, "*", SearchOption.TopDirectoryOnly )
				.Where( Repository.IsValid )
				.Select( d => new Repository( d ) )
				.SelectMany( GetFilePathsInBucket )
				.Select( StripOrgNameFromBucketPath )
				.Select( f => f.ToLower() )
				.Where( f => !IgnoreFile( f ) )
				.ToList();

			bucketPaths.Sort();

			using ( var e = bucketPaths.GetEnumerator() ) {
				e.MoveNext(); // prime the enumerator
				Treeify( e );
			}
		}

		private static bool IgnoreFile( string f ) {
			return f.EndsWith( ".gitignore" );
		}

		private static string StripOrgNameFromBucketPath( string bucketPath ) {
			return bucketPath.Substring( bucketPath.IndexOf( '/' ) );
		}

		private static IEnumerable<string> GetFilePathsInBucket( Repository repo ) {
			Stack<Tree> trees = new Stack<Tree>();

			trees.Push( repo.Head.Tip.Tree );

			while( trees.Count != 0 ) {
				var tree = trees.Pop();
				foreach( var item in tree ) {
					switch( item.TargetType ) {
						case TreeEntryTargetType.Blob:
							// hack: top-level files aren't really part of the filebuckets
							if ( !item.Path.Contains( '/' ) ) {
								break;
							}

							yield return item.Path;
							break;

						case TreeEntryTargetType.Tree:
							trees.Push( item.Target as Tree );
							break;

						default:
							throw new NotImplementedException();
					}
				}
			}
		}

		static void Main( string[] args ) {
			switch( args.Length ) {
				case 0:
					Run( "//pdfs01/ea/devops/filebucket" );
					break;
				case 1:
					Run( args[0] );
					break;

				default:
					throw new Exception( "too many arguments" );
			}
		}
	}
}