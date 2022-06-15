using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpticalRenderFormLibrary
{
    /// <summary>
    ///Class used to use the recognition API on optical render forms and to process the return to
    ///extract some variables (errors, time, etc).
    /// This library class was made according to the MVP concept.Everything is modular according to the customer's needs.
    /// Can be easly transform to a package.
    /// </summary>
    public class ApiProcessing
    {
        public readonly Dictionary<string, string> _fields;
        public readonly List<List<string>> _allProcessingResults;
        public readonly float _minimumConfidence;
        public ApiProcessing(float minimumConfidence)
        {
            //specifique to this API (everything can be added or removed aftert)
            _fields = new Dictionary<string, string>()
            {
                {"a", "integer" },
                {"b", "integer" },
                {"nose" , "integer"},
                {"cylinder" , "float"},
                {"client_account" , "integer"},
                {"fullname", "string"},
                {"index", "integer"},
                {"sphere", "float"},
                {"axe", "float"}
            };
            _minimumConfidence = minimumConfidence;
            _allProcessingResults = new List<List<string>>();
        }

        /// <summary>
        /// this function sends all documents in a directory to the API and calls calculation function.
        /// The file must be pdf/webp/png/jpeg/jpg file, max size 10MB and for max 5 pages for pdf files.
        /// </summary>
        /// <param name="DirectoryPath"> directory path</param>
        /// <returns> List<List<string>> or null if files not passed tests </returns>
        public async Task<List<List<string>>?> EssentialInformationsRenderForm(string DirectoryPath)
        {

            //get all directory's files
            string[] files = Directory.GetFiles(DirectoryPath);

            //file tests
            foreach(string file in files)
            {
                if(FileTest(file) == false)
                    return null;
            }

            for (int i = 0; i < files.Length; i++)
            {
                //essential informations function call with response string with json format
                //return all of essential informations of each documents
                _allProcessingResults.Add(ProcessingEssentialInformations(await Connexion(files[i])));
            }
            return _allProcessingResults;
        }

        /// <summary>
        /// checks for the files.
        /// The file must be pdf/webp/png/jpeg/jpg file, max size 10MB and for max 5 pages for pdf files.
        /// </summary>
        /// <param name="filePath">file path</param>
        /// <returns>true or false</returns>
        private bool FileTest(string filePath)
        {
            // check nature of file
            var res = filePath.Split('.');
            if (res[res.Length - 1] == "pdf" || res[res.Length - 1] == "png" ||
                res[res.Length - 1] == "jpg" || res[res.Length - 1] == "jpeg"
                || res[res.Length - 1] == "webp")
            {
                // check file length
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 10485760)
                {
                    return false;
                }
                //check number of page for pdf file
                if (res[res.Length - 1] != "pdf")
                {
                    PdfReader pdfReader = new PdfReader(filePath);
                    if (pdfReader.NumberOfPages > 5)
                    {
                        return false;
                    }
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Return details results from API recognition.
        /// </summary>
        /// <param name="filePath">file path</param>
        /// <returns> List<List<string>> or null if the file not passed tests </returns>
        public async Task<List<List<string>>?> DetailsRenderForm(string filePath )
        {
            //file tests
            if (FileTest(filePath) == false)
                return null;
            

            return ProcessingDetails(await Connexion(filePath));
        }

        /// <summary>
        /// function for api test connexion.
        /// </summary>
        /// <param name="fileName"> file name to send to the API</param>
        /// <returns>response string of the API. string is indented (based Json pattern) </returns>
        private async Task<string> Connexion(string fileName)
        {
            //API setup connexion
            var url = "https://api.mindee.net/v1/products/solution-eng-sandbox/optic_order_form/v1/predict";
            var authToken = "cc109780d48d6d427dd2a302392976a5";

            var file = File.OpenRead(fileName);
            var streamContent = new StreamContent(file);
            var imageContent = new ByteArrayContent(await streamContent.ReadAsByteArrayAsync());
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

            var form = new MultipartFormDataContent();
            form.Add(imageContent, "document", Path.GetFileName(fileName));

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", authToken);

            //api POST forme
            var response = await httpClient.PostAsync(url, form);

            //transformation response string to JsonNode
            string reponseString = await response.Content.ReadAsStringAsync();
            JsonNode forecastNode = JsonNode.Parse(reponseString)!;
            var options = new JsonSerializerOptions { WriteIndented = true };
            return forecastNode!.ToJsonString(options);
        }

        /// <summary>
        /// function for calculating details for one document.
        /// number of prediction  the number of prediction who has one or many correct field.
        /// list of confidence  for all predicted content .
        /// list of content  for all predicted content.
        /// </summary>
        /// <param name="jsonText">API response string with indentation (json format)</param>
        /// <returns>List<List<string>> (List number Of Predictions per fields,
        ///     List list Of Confidences per predictions,
        ///     List list Of Contents per predictions)
        /// </returns>
        private List<List<string>> ProcessingDetails(string jsonText)
        {
            List<List<string>> result = new List<List<string>>();

            //parsing response string to Json Object
            var jsonobject = JObject.Parse(jsonText);

            List<string> numberOfPredictions = new List<string>();
            List<string> listOfConfidences = new List<string>();
            List<string> listOfContents = new List<string>();

            int predictionCount = 0;
            int fieldErrorCount = 0;

            /*
            * searches all fields in the json object.
            * calculate the errors in the json object when extracting the field
           */
            foreach (KeyValuePair<string, string> pair in _fields)
            {
                IEnumerable<JToken>? jsonValue = jsonobject["document"]?["inference"]?["prediction"]?[pair.Key]?["values"]?.ToList();
                if (jsonValue != null)
                {
                    if (jsonValue.Count() == 0)
                    {
                        fieldErrorCount++;
                    }
                    foreach (var token in jsonValue)
                    {
                        //extract the confidence and the content of fields prediction
                        string? confidence = token["confidence"]?.ToString();
                        string? content = token["content"]?.ToString();
                        //regex to remove the ':' and to remove the part before the ':' if it exists 
                        content = Regex.Replace(content, @"\p{L}*\:", "");
                        if (confidence != null || confidence != "" || content != null || content != "")
                        {
                            predictionCount++;
                            //minimum confidence to have
                            if (Single.TryParse(confidence, out float confidenceFloat) && confidenceFloat >= _minimumConfidence)
                            {
                                //check the nature of our reseach
                                if (pair.Value == "integer")
                                {
                                    if (Int32.TryParse(content, out int res))
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
                    if (predictionCount - fieldErrorCount > 0)
                    {
                        numberOfPredictions.Add((predictionCount - fieldErrorCount).ToString());
                    }
                    else
                    {
                        numberOfPredictions.Add("0");
                    }
                    fieldErrorCount = 0;
                    predictionCount = 0;
                }
            }
            result.Add(listOfConfidences);
            result.Add(listOfContents);
            result.Add(numberOfPredictions);
            return result;
        }

        /// <summary>
        /// This function calculates the number of errors returned by the api and 
        /// extracts data such as process time, response status, etc.
        /// </summary>
        /// <param name="jsonText"> API response string with indentation (json format)</param>
        /// <returns> a list of the results of the document :
        /// list {
        ///     status (response)
        ///     document ID (from API)
        ///     document Name
        ///     processing Time (API)
        ///     error number
        ///     }
        /// </returns>
        private List<string> ProcessingEssentialInformations(string jsonText)
        {
            int predictionCount = 0;
            int fieldErrorCount = 0;
            int numberError = 0;

            //parsing response string to Json Object
            var jsonobject = JObject.Parse(jsonText);

            //extract some essential informations from api's response
            string? predictionStatus = jsonobject["api_request"]?["status"]?.ToString();
            string? documentID = jsonobject["document"]?["id"]?.ToString();
            string? documentName = jsonobject["document"]?["name"]?.ToString();
            string? processingTime = jsonobject["document"]?["inference"]?["processing_time"]?.ToString();

            //stock essential informations in list of string
            List<string?> infosApiResponse = new List<string?>()
            {
                documentName,
                documentID,
                predictionStatus,
                processingTime
            };

            /*
             * searches all fields in the json object.
             * calculate the errors in the json object when extracting the field
            */
            foreach (KeyValuePair<string, string> pair in _fields)
            {
                IEnumerable<JToken>? jsonValue = jsonobject["document"]?["inference"]?["prediction"]?[pair.Key]?["values"]?.ToList();
                if (jsonValue != null)
                {
                    if (jsonValue.Count() == 0)
                    {
                        fieldErrorCount++;
                    }
                    foreach (var token in jsonValue)
                    {
                        //extract the confidence and the content of fields prediction
                        string? confidence = token["confidence"]?.ToString();
                        string? content = token["content"]?.ToString();
                        //regex to remove the ':' and to remove the part before the ':' if it exists 
                        content = Regex.Replace(content, @"\p{L}*\:", "");
                        if (confidence != null || confidence != "" || content != null || content != "")
                        {
                            predictionCount++;
                            //minimum confidence to have
                            if (Single.TryParse(confidence, out float confidenceFloat) && confidenceFloat >= _minimumConfidence)
                            {
                                //check the nature of our reseach
                                if (pair.Value == "integer")
                                {
                                    if (!Int32.TryParse(content, out int res))
                                    {
                                        fieldErrorCount++;
                                    }
                                }
                                if (pair.Value == "float")
                                {
                                    if (!Single.TryParse(content, out float res))
                                    {
                                        fieldErrorCount++;
                                    }
                                }
                                if (pair.Value == "string")
                                {
                                    if (!content.All(Char.IsLetter))
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
                    //allows to know if a field has at least one valid prediction. If not, add to the error number count
                    if (predictionCount - fieldErrorCount <= 0)
                    {
                        numberError++;
                    }
                    fieldErrorCount = 0;
                    predictionCount = 0;
                }
            }
            //add the number of errors to the essential fields
            infosApiResponse.Add(numberError.ToString());

            return infosApiResponse;
        }
    }
}