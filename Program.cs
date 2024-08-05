using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using CommandLine;

class FatFileFinder
{
    private string DirectoryPath { get; set; }
    private long MinSize { get; set; }
    private string[] FileExtensions { get; set; }
    private string RegexPattern { get; set; }

    public FatFileFinder(string directoryPath, long minSize, string[] fileExtensions, string regexPattern)
    {
        DirectoryPath = directoryPath;
        MinSize = minSize;
        FileExtensions = fileExtensions;
        RegexPattern = regexPattern;
    }

    public void Run()
    {
        try
        {
            var file = FindLargeFile(DirectoryPath);
            if (file != null)
            {
                Console.WriteLine($"File found: {file.FullName} - {file.Length} bytes");

                Console.WriteLine("Do you want to compress this file into a ZIP archive? (y/n)");
                if (Console.ReadLine().Trim().ToLower() == "y")
                {
                    Console.WriteLine("Enter the output ZIP file path:");
                    string zipPath = Console.ReadLine();
                    CompressFile(file, zipPath);
                    Console.WriteLine("File compressed successfully.");
                }

                Console.WriteLine("Do you want to move this file to a temporary directory? (y/n)");
                if (Console.ReadLine().Trim().ToLower() == "y")
                {
                    MoveFileToTemp(file);
                    Console.WriteLine("File moved successfully.");
                }
            }
            else
            {
                Console.WriteLine("No files found that match the criteria.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"An error occurred: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private FileInfo FindLargeFile(string currentDirectory)
    {
        try
        {
            foreach (var file in Directory.GetFiles(currentDirectory))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if ((string.IsNullOrEmpty(RegexPattern) || Regex.IsMatch(fileInfo.Name, RegexPattern)) &&
                        fileInfo.Length > MinSize &&
                        (FileExtensions.Length == 0 || FileExtensions.Contains(fileInfo.Extension)))
                    {
                        return fileInfo;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error accessing file '{file}': {ex.Message}\n{ex.StackTrace}");
                }
            }

            foreach (var directory in Directory.GetDirectories(currentDirectory))
            {
                var result = FindLargeFile(directory);
                if (result != null)
                {
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error accessing directory '{currentDirectory}': {ex.Message}\n{ex.StackTrace}");
        }

        return null;
    }

    private void CompressFile(FileInfo file, string zipPath)
    {
        try
        {
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(file.FullName, file.Name, CompressionLevel.Optimal);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error compressing file '{file.FullName}': {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void MoveFileToTemp(FileInfo file)
    {
        try
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "FatFileFinder");
            Directory.CreateDirectory(tempDirectory);

            string destFile = Path.Combine(tempDirectory, file.Name);
            File.Move(file.FullName, destFile);
            Console.WriteLine($"Moved: {file.FullName} to {destFile}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error moving file '{file.FullName}': {ex.Message}\n{ex.StackTrace}");
        }
    }

    public static long ParseSize(string sizeInput)
    {
        sizeInput = sizeInput.ToUpper().Trim();
        long multiplier = 1;

        if (sizeInput.EndsWith("KB"))
        {
            multiplier = 1024;
            sizeInput = sizeInput.Substring(0, sizeInput.Length - 2);
        }
        else if (sizeInput.EndsWith("MB"))
        {
            multiplier = 1024 * 1024;
            sizeInput = sizeInput.Substring(0, sizeInput.Length - 2);
        }
        else if (sizeInput.EndsWith("GB"))
        {
            multiplier = 1024 * 1024 * 1024;
            sizeInput = sizeInput.Substring(0, sizeInput.Length - 2);
        }

        if (long.TryParse(sizeInput, out long size))
        {
            return size * multiplier;
        }

        throw new ArgumentException("Invalid size format.");
    }
}

class Program
{
    public class Options
    {
        [Option('d', "directory", Required = true, HelpText = "The directory to search for files.")]
        public string Directory { get; set; }

        [Option('s', "size", Required = true, HelpText = "The minimum file size (e.g., 10MB).")]
        public string Size { get; set; }

        [Option('e', "extensions", Default = "", HelpText = "Comma-separated list of file extensions to filter by (or leave blank for all).")]
        public string Extensions { get; set; }

        [Option('p', "pattern", Default = "", HelpText = "The regex pattern to filter file names by (or leave blank for none).")]
        public string Pattern { get; set; }
    }

    static int Main(string[] args)
    {
        return Parser.Default.ParseArguments<Options>(args)
            .MapResult(
                (Options opts) => RunOptionsAndReturnExitCode(opts),
                errs => 1);
    }

    private static int RunOptionsAndReturnExitCode(Options opts)
    {
        try
        {
            long minSize = FatFileFinder.ParseSize(opts.Size);
            var fileExtensions = opts.Extensions.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(e => e.Trim().StartsWith(".") ? e.Trim() : "." + e.Trim())
                                                 .ToArray();
            var finder = new FatFileFinder(opts.Directory, minSize, fileExtensions, opts.Pattern);
            finder.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"An error occurred during command execution: {ex.Message}\n{ex.StackTrace}");
            return 1;
        }
    }
}
