using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpticalRenderFormLibrary;
using System.Diagnostics;

namespace OpticalRenderFormUseCase
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Stopwatch stopwatch = new Stopwatch();
            List<List<string>>? allProcessingResults = new List<List<string>>();
            int totalErrorCount = 0;
            int totalDocumentHaveError = 0;
            float minimumConfidence = (float)0.95;
            //replace the directory path
            string directoryPath = @"C:\Users\sebas\Documents\GitHub\OpticalOrderFormTest\OpticalRenderFormUseCase\OpticalRenderFormUseCase\optic_order_form_test_set";
            string[] files = Directory.GetFiles(directoryPath);

            Console.WriteLine("Document name: ao1_20210102184641_fax_340319_20210102_184531_00128.pdf");
            Console.WriteLine();
            //compute running time 
            stopwatch.Start();

            //this is for just one document. replace directory path and the name of the document
            await DisplayForDetails(directoryPath + @"\ao1_20210102184641_fax_340319_20210102_184531_00128.pdf", minimumConfidence);
            
            //call API library
            ApiProcessing library = new ApiProcessing(minimumConfidence);
            //for 0 or many documents in a directory
            allProcessingResults = await library.EssentialInformationsRenderForm(directoryPath);

            //stop compute running time 
            stopwatch.Stop();

            //display result of processing API library
            foreach (List<string> processingResult in allProcessingResults)
            {
                Int32.TryParse(processingResult.Last(), out var errorNumber);
                totalErrorCount += errorNumber;

                if(errorNumber != 0)
                    totalDocumentHaveError++;

                foreach (string res in processingResult)
                {
                    Console.WriteLine(res);
                }
                Console.WriteLine("+++++++++++++++++++++++++++++");
            }
            Console.WriteLine("Total number of errors: " + totalErrorCount.ToString());
            Console.WriteLine("Total number of documents: " + files.Length.ToString());
            Console.WriteLine("Total number of documents wich have at least one missing value: " + totalDocumentHaveError);
            Console.WriteLine("Average number of errors per document: " + (totalErrorCount/ files.Length).ToString());
            Console.WriteLine("Running time: " + stopwatch.Elapsed.TotalSeconds.ToString() + "s");
            Console.WriteLine();
        }

        /// <summary>
        /// function for display details result
        /// </summary>
        /// <param name="filesPath"> file path </param>
        /// <param name="minimumConfidence">minimum confidence</param>
        /// <returns></returns>
        public static async Task DisplayForDetails(string filesPath, float minimumConfidence)
        {

            List<List<string>>? lists = new List<List<string>>();

            ApiProcessing library = new ApiProcessing(minimumConfidence);
            //for 0 or many documents in a directory
            lists = await library.DetailsRenderForm(filesPath);

            //can be change
            string errorMessage = "Need to be filled in manually";
            int indexRaw = 0;
            int index = 0;

            //browse lists for display
            foreach (KeyValuePair<string, string> pair in library._fields)
            {
                Console.WriteLine("Feature's name: " + pair.Key);
                if (Int32.Parse(lists[2][indexRaw]) != 0) 
                {
                    if (Int32.Parse(lists[2][indexRaw]) > 1)
                    {
                        for (int i = 0; i < Int32.Parse(lists[2][indexRaw]); i++)
                        {
                            if (lists[0][index].ToString() != errorMessage)
                            {
                                Console.WriteLine("Prediction " + (i + 1).ToString() + ":");
                                Console.Write("Confidence: " + lists[0][index].ToString() + "  ");
                                Console.Write("Content: " + lists[1][index]);
                                Console.WriteLine(" ");

                            }
                            index++;
                        }
                    }
                    else
                    {
                        if (lists[0][index].ToString() != errorMessage)
                        {
                            Console.WriteLine("Confidence: " + lists[0][index]);
                            Console.WriteLine("Content: " + lists[1][index]);
                            Console.WriteLine(" ");
                        }
                        index++;
                    }
                }
                else
                {
                    Console.WriteLine(errorMessage);
                }
                Console.WriteLine("********************************************");
                indexRaw++;
            }


        }
    }
}