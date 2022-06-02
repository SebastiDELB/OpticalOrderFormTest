using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpticalRenderFormUseCase
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(Connexion());
        }
        static string Connexion()
        {
            /*
            var url = "https://api.mindee.net/v1/products/solution-eng-sandbox/optic_order_form/v1/predict";
            var filePath = @"C:\Users\sebas\Documents\optic_order_form_test_set\ao1_20210102184641_fax_340319_20210102_184531_00128.pdf";
            var token = "cc109780d48d6d427dd2a302392976a5";

            var file = File.OpenRead(filePath);
            var streamContent = new StreamContent(file);
            var imageContent = new ByteArrayContent(streamContent.ReadAsByteArrayAsync().Result);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

            var form = new MultipartFormDataContent();
            form.Add(imageContent, "document", Path.GetFileName(filePath));

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);
            var response = httpClient.PostAsync(url, form).Result;

            JsonNode forecastNode = JsonNode.Parse(response.Content.ReadAsStringAsync().Result)!;

            
            var options = new JsonSerializerOptions { WriteIndented = true };
            Console.WriteLine(forecastNode!.ToJsonString(options));
            var d = forecastNode!.ToJsonString(options);
            var pe = forecastNode.AsObject();
            var arf = pe.TryGetPropertyValue("a", out var value);
            */


            Dictionary<string, string> fields = new Dictionary<string, string>()
            {
                {"a", "integer" },
                {"b", "integer" },
                {"nose" , "integer" },
                {"cylinder" , "float"},
                {"client_account" , "integer" },
                { "fullname", "string"},
                { "index", "integer"},
                {"sphere", "float" },
                {"axe", "float" }
            };
            List<int> numberOfPredictions = new List<int>();
            List<string> listOfConfidences = new List<string>();
            List<string> listOfContents = new List<string>();
            List<string> errors = new List<string>();
            float minimumConfidence = (float)0.95;
            int predictionCount = 0;
            int fieldErrorCount = 0;
            int numberTotalError = 0;
            string errorMessage = "Need to be filled in manually";


            var jsonText = File.ReadAllText(@"C:\Users\sebas\source\repos\OpticalRenderFormUseCase\OpticalRenderFormUseCase\bin\Debug\net6.0\tst1.json");
            var jsonobject = JObject.Parse(jsonText);

            string? predictionStatus = jsonobject["api_request"]?["status"]?.ToString();
            string? numberOfPages = jsonobject["document"]?["n_pages"]?.ToString();
            string? documentName = jsonobject["document"]?["name"]?.ToString();
            string? processingTime = jsonobject["document"]?["inference"]?["processing_time"]?.ToString();


            Console.WriteLine("prediction status: " + predictionStatus);
            Console.WriteLine("number of pages: " + numberOfPages);
            Console.WriteLine("document's name: " + documentName);
            Console.WriteLine("processing time: " + processingTime + "s");
            Console.WriteLine("*********************//////////////////*******************");

            foreach (KeyValuePair<string, string> pair in fields)
            {
                IEnumerable<JToken>? jsonValue = jsonobject["document"]?["inference"]?["prediction"]?[pair.Key]?["values"]?.ToList();
                if (jsonValue != null)
                {
                    if(jsonValue.Count() == 0)
                    {
                        fieldErrorCount++;
                    }
                    foreach (var token in jsonValue)
                    {
                        predictionCount++;
                        string? confidence = token["confidence"]?.ToString();
                        string? content = token["content"]?.ToString();
                        if (confidence != null || confidence != "" || content != null || content != "")
                        {
                            if (Single.TryParse(confidence, out float confidenceFloat) && confidenceFloat >= minimumConfidence)
                            {
                                if (pair.Value == "integer")
                                {
                                    if(Int32.TryParse(content, out int res))
                                    {
                                        listOfConfidences.Add(confidenceFloat.ToString());
                                        listOfContents.Add(content);
                                    }
                                    else
                                    {
                                        fieldErrorCount++;
                                    }
                                }
                                if (pair.Value == "float")
                                {
                                    if (Single.TryParse(content, out float res))
                                    {
                                        listOfConfidences.Add(confidenceFloat.ToString());
                                        listOfContents.Add(content);
                                    }
                                    else
                                    {
                                        fieldErrorCount++;
                                    }
                                }
                                if (pair.Value == "string")
                                {
                                    if (content.All(Char.IsLetter))
                                    {
                                        listOfConfidences.Add(confidenceFloat.ToString());
                                        listOfContents.Add(content);
                                    }
                                    else
                                    {
                                        fieldErrorCount++;
                                    }
                                }

                            }
                            else
                            {
                                fieldErrorCount++;
                            }
                        }
                    }
                    if(predictionCount - fieldErrorCount > 0)
                    {
                        numberOfPredictions.Add(predictionCount - fieldErrorCount);
                    }
                    else
                    {
                        numberOfPredictions.Add(0);
                        numberTotalError++;
                    }
                    fieldErrorCount = 0;
                    predictionCount = 0;
                }
            }






            
            int indexRaw = 0;
            int index = 0;
            foreach(KeyValuePair<string, string> pair in fields)
            {
                Console.WriteLine("feature's name: "+pair.Key);
                if( numberOfPredictions[indexRaw] != 0)
                {
                    if (numberOfPredictions[indexRaw] > 1)
                    {
                        for (int i = 0; i < numberOfPredictions[indexRaw]; i++)
                        {
                            if (listOfConfidences[index].ToString() != errorMessage)
                            {
                                Console.WriteLine("preidiction " + (i + 1).ToString() + ":");
                                Console.Write("confience: " + listOfConfidences[index].ToString() + "  ");
                                Console.Write("content: " + listOfContents[index]);
                                Console.WriteLine(" ");

                            }
                            index++;
                        }
                    }
                    else
                    {
                        if (listOfConfidences[index].ToString() != errorMessage)
                        {
                            Console.WriteLine("confience: " + listOfConfidences[index]);
                            Console.WriteLine("content: " + listOfContents[index]);
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
            
            



            return "number of filed's error = " + numberTotalError.ToString();
        }
    }
}