# FileBucketStats

Creates a tree-view representation of the filebuckets modulo org

## Instructions

Install .NET core (Windows or Linux) and run

```cs
dotnet run -c Release --project src/FileBucketStats/FileBucketStats.csproj > buckets.json
```

This will look on `//pdfs01/ea/devops/filebucket/` for filebuckets (this can be configured with a command line argument) and write the output into `buckets.json`.
Combine that file with `www/bucket-browse.html` to get a web-based tree-view thingy.
