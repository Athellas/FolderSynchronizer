using System;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Linq;
using System.Globalization;

internal class Program
{
    private static void Main(string[] args)
    {

        if (args.Length != 4)
        {
            System.Console.WriteLine("Error: Progam is started with incorrect number of arguments. Launch the program with 4 arguments: SourceFolderPath, ReplicaFolderPath, LogFilePath and timeBetweenSynchronizationsInSeconds.");
            return;
        }
        Console.WriteLine("Arguments:");
        foreach (var arg in args)
        {
            Console.WriteLine(arg);
        }

        string sourcePath = args[0];
        string replicaPath = args[1];
        // this could cause problems:
        if (replicaPath.Contains(sourcePath))
        {
            System.Console.WriteLine("Error: the replica folder can't be located inside source folder!");
            return;
        }
        string logFilePath = args[2];
        int synchronizationIntervalInSeconds = int.Parse(args[3]);

        SynchronizeArguments arguments = new SynchronizeArguments(sourcePath, replicaPath, logFilePath);

        Timer timer = new Timer(Synchronize, arguments, 0, synchronizationIntervalInSeconds * 1000);

        Console.ReadLine();
    }

    class SynchronizeArguments
    {
        public string SourcePath { get; set; }
        public string ReplicaPath { get; set; }
        public string LogFilePath { get; set; }

        public SynchronizeArguments(string sourcePath, string replicaPath, string logFilePath)
        {
            SourcePath = sourcePath;
            ReplicaPath = replicaPath;
            LogFilePath = logFilePath;
            System.Console.WriteLine("SynchronizeArguments class object created");
        }
    }


    static void Synchronize(object? state)
    {
        System.Console.WriteLine("Starting Synchronize method");
        var data = (SynchronizeArguments)state!;
        string rootSourceDirectory = data.SourcePath;
        string rootReplicaDirectory = data.ReplicaPath;
        string logFile = data.LogFilePath;
        string currentSourceSubDirectory = data.SourcePath;
        string currentReplicaSubDirectory = data.ReplicaPath;

        List<string> listOfDirectories = new List<string>();
        GetAllFolders(rootSourceDirectory, listOfDirectories);

        foreach (string folder in listOfDirectories)
        {
            currentSourceSubDirectory = folder;
            currentReplicaSubDirectory = rootReplicaDirectory + folder.Substring(rootSourceDirectory.Length);

            //get a list of files from source folder
            string[] sourceFiles = Directory.GetFiles(currentSourceSubDirectory);

            //create a dictionary of file-hash of source folder
            Dictionary<string, byte[]> sourceFilesDictionary = new Dictionary<string, byte[]>();
            //foreach file generate hash and add it as a value to dictionary
            foreach (string file in sourceFiles)
            {
                byte[] hash;
                using (Stream input = File.OpenRead(file))
                {
                    hash = MD5.Create().ComputeHash(input);
                }
                if (hash != null)
                    sourceFilesDictionary.Add(file, hash);
            }
            foreach (var keyvalue in sourceFilesDictionary)
            {
                string hashString = BitConverter.ToString(keyvalue.Value).Replace("-", "").ToLowerInvariant();
                //System.Console.WriteLine($"Source Key: {keyvalue.Key}, Value: {hashString}");
            }

            //if replica path folder does not exist - create it (log creation)
            if (!Directory.Exists(currentReplicaSubDirectory))
            {
                Directory.CreateDirectory(currentReplicaSubDirectory);
                File.AppendAllText(logFile, $"{DateTime.Now}: Creating folder {currentReplicaSubDirectory}{Environment.NewLine}");
                System.Console.WriteLine($"{DateTime.Now}: Creating folder {currentReplicaSubDirectory}");
            }

            //else get a list of files from the replica folder

            string[] replicaFiles = Directory.GetFiles(currentReplicaSubDirectory);
            //create dictionary
            //create a dictionary of file-hash of source folder
            Dictionary<string, byte[]> replicaFilesDictionary = new Dictionary<string, byte[]>();
            //foreach file generate hash and add it as a value to dictionary
            foreach (string file in replicaFiles)
            {
                byte[] hash;
                using (Stream input = File.OpenRead(file))
                {
                    hash = MD5.Create().ComputeHash(input);
                }
                if (hash != null)
                    replicaFilesDictionary.Add(file, hash);
            }
            foreach (var keyvalue in replicaFilesDictionary)
            {
                string hashString = BitConverter.ToString(keyvalue.Value).Replace("-", "").ToLowerInvariant();
                //System.Console.WriteLine($"Replica Key: {keyvalue.Key}, Value: {hashString}");
            }

            //compare lists

            //foreach element in source dictionary
            foreach (var keyValuePair in sourceFilesDictionary)
            {
                //remove folder path to compare only file name
                string fileName = keyValuePair.Key.Substring(currentSourceSubDirectory.Length);

                //{check if there is the same file in the replica list
                if (!replicaFilesDictionary.ContainsKey(currentReplicaSubDirectory + fileName))
                {

                    File.Copy(keyValuePair.Key, currentReplicaSubDirectory + fileName);
                    System.Console.WriteLine($"Copying file: {keyValuePair.Key}");
                    File.AppendAllText(logFile, $"{DateTime.Now}: Copying file: {keyValuePair.Key}{Environment.NewLine}");
                }
                else
                {
                    //System.Console.WriteLine($"Found the same filename: {fileName}");
                    //if there is - check if hash is the same
                    if (keyValuePair.Value.SequenceEqual(replicaFilesDictionary[currentReplicaSubDirectory + fileName]))
                    {
                        //if the hash is the same - do nothing
                        //System.Console.WriteLine("File hash is the same");
                    }
                    else
                    {
                        //if the hash is different
                        //System.Console.WriteLine("File hash is different");
                        //delete file from replica folder
                        System.Console.WriteLine($"Deleting file: {currentReplicaSubDirectory + fileName}");
                        File.AppendAllText(logFile, $"{DateTime.Now}: Deleting file: {currentReplicaSubDirectory + fileName}{Environment.NewLine}");
                        File.Delete(currentReplicaSubDirectory + fileName);
                        //copy file from source to replica
                        System.Console.WriteLine($"Copying file: {keyValuePair.Key}");
                        File.AppendAllText(logFile, $"{DateTime.Now}: Copying file: {keyValuePair.Key}{Environment.NewLine}");
                        File.Copy(keyValuePair.Key, currentReplicaSubDirectory + fileName);
                    }
                }
            }
            //foreach element in replica folder check if there is the same file in the source folder, if not delete it as it was deleted from the source folder

            //first regenerate array of files in the replica folder, after copying 
            string[] secondaryReplicaFilesList = Directory.GetFiles(currentReplicaSubDirectory);

            foreach (string replicaFile in secondaryReplicaFilesList)
            {
                string replicaFileName = replicaFile.Substring(currentReplicaSubDirectory.Length);
                if (!sourceFiles.Contains<string>(currentSourceSubDirectory + replicaFileName))
                {
                    System.Console.WriteLine($"File {replicaFile} not found in source folder, deleting");
                    File.AppendAllText(logFile, $"{DateTime.Now}: File {replicaFile} not found in source folder, deleting{Environment.NewLine}");
                    File.Delete(replicaFile);
                }
            }
        }
    }

    static void GetAllFolders(string path, List<string> folders)
    {
        try
        {
            folders.Add(path);

            foreach (string dir in Directory.GetDirectories(path))
            {
                GetAllFolders(dir, folders);
            }
        }
        catch { }
    }
}