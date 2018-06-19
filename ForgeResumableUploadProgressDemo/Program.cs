using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Forge;
using System.IO;
using Autodesk.Forge.Client;

namespace ForgeResumableUploadProgressDemo
{
    class Program
    {
        private static string FORGE_CLIENT_ID = Environment.GetEnvironmentVariable("FORGE_CLIENT_ID") ?? "your_client_id";
        private static string FORGE_CLIENT_SECRET = Environment.GetEnvironmentVariable("FORGE_CLIENT_SECRET") ?? "your_client_secret";
        private static string BUCKET_KEY = "forge-csharp-sample-app-" + FORGE_CLIENT_ID.ToLower();
        private static string FILE_NAME = "rme_basic_sample_project.rvt";
        private static string FILE_PATH = @".\Models\rme_basic_sample_project.rvt";

        // Initialize the relevant clients; in this example, the Objects, Buckets and Derivatives clients, which are part of the Data Management API and Model Derivatives API
        private static ObjectsApi objectsApi = new ObjectsApi();

        private static TwoLeggedApi oauth2TwoLegged;
        private static dynamic twoLeggedCredentials;

        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Initialize the 2-legged OAuth 2.0 client, and optionally set specific scopes.
        private static void initializeOAuth()
        {
            // You must provide at least one valid scope
            Scope[] scopes = new Scope[] { Scope.DataRead, Scope.DataWrite, Scope.BucketCreate, Scope.BucketRead };

            oauth2TwoLegged = new TwoLeggedApi();
            twoLeggedCredentials = oauth2TwoLegged.Authenticate(FORGE_CLIENT_ID, FORGE_CLIENT_SECRET, oAuthConstants.CLIENT_CREDENTIALS, scopes);
            objectsApi.Configuration.AccessToken = twoLeggedCredentials.access_token;
        }

        private static void resumableUploadFile()
        {
            Console.WriteLine("*****Start uploading file to the OSS");
            string path = FILE_PATH;
            if (!File.Exists(path))
                path = @"..\..\..\" + FILE_PATH;

            //File Total size        
            long fileSize = new System.IO.FileInfo(path).Length;
            //Chunk size for separting file into several parts.
            //2MB chuck size is used in this sample.
            long chunkSize = 2 * 1024 * 1024;
            //Total amounts of chunks in 2MB size.
            long nbChunks = (long)Math.Round(0.5 + (double)fileSize / (double)chunkSize);

            Console.WriteLine(string.Format("nbChunks: {0}", nbChunks));

            using (FileStream streamReader = new FileStream(path, FileMode.Open))
            {
                //Unique id for resumable uploading.
                string sessionId = RandomString(12);
                Console.WriteLine(string.Format("sessionId: {0}", sessionId));
                for (int i = 0; i < nbChunks; i++)
                {
                    //Start position in bytes of a chunk
                    long start = i * chunkSize;
                    //End position in bytes of a chunk
                    //(End posistion of the latest chuck is the total file size in bytes)
                    long end = Math.Min(fileSize, (i + 1) * chunkSize) - 1;

                    //Identify chunk info. to the Forge
                    string range = "bytes " + start + "-" + end + "/" + fileSize;
                    //Steam size for this chunk
                    long length = end - start + 1;

                    Console.WriteLine("Uploading range： " + range);

                    //Read content stream into a meomery stream for this chunk
                    byte[] buffer = new byte[length];
                    MemoryStream memoryStream = new MemoryStream(buffer);

                    int nb = streamReader.Read(buffer, 0, (int)length);
                    memoryStream.Write(buffer, 0, nb);
                    memoryStream.Position = 0;

                    //Upload file to the Forge OSS Bucket
                    var asyncResult = objectsApi.UploadChunkAsyncWithHttpInfo(
                                                        BUCKET_KEY,
                                                        FILE_NAME,
                                                        (int)length,
                                                        range,
                                                        sessionId,
                                                        memoryStream
                                                    );

                    var response = asyncResult.Result;

                    var progressVal = (double)(i + 1) / nbChunks * 100;
                    Console.WriteLine(string.Format("Current Progress: {0}%", progressVal));

                    if (response.StatusCode == 202)
                    {
                        Console.WriteLine("One chunk uploaded successfully\n");
                        continue;
                    }
                    else if (response.StatusCode == 200)
                    {
                        Console.WriteLine("Final chunk uploaded successfully\n");
                        Console.WriteLine(response.Data);
                    }
                    else
                    {
                        //Some error occurred here
                        Console.WriteLine(response.StatusCode);
                        break;
                    }
                }

            }
        }

        static void Main(string[] args)
        {
            initializeOAuth();
            resumableUploadFile();
            Console.ReadLine();
        }
    }
}
